using GamestatsBase;
using gtsCore.Helpers;
using Microsoft.AspNetCore.Mvc;
using PkmnFoundations.Data;
using PkmnFoundations.Pokedex;
using PkmnFoundations.Structures;
using PkmnFoundations.Support;
using PkmnFoundations.Wfc;

namespace gtsCore.Controllers.Syachi2ds;

[Route("syachi2ds/web/battletower")]
[ApiController]
[GamestatsConfig("HZEdGCzcGGLvguqUEKQN0001d93500002dd5000000082db842b2syachi2ds", GamestatsRequestVersions.Version3, GamestatsResponseVersions.Version2, encryptedRequest: false, requireSession: true)]
[BanMiddleware(Generations.Generation5)]
public class BattletowerController : ControllerBase
{
    private readonly GamestatsSessionManager _sessionManager;
    private readonly IpAddressHelper _ipAddressHelper;
    private readonly Pokedex _pokedex;
    private readonly FakeOpponentGenerator _opponentGenerator;

    public BattletowerController(GamestatsSessionManager sessionManager, IpAddressHelper ipAddressHelper, Pokedex pokedex, FakeOpponentGenerator opponentGenerator)
    {
        _sessionManager = sessionManager;
        _ipAddressHelper = ipAddressHelper;
        _pokedex = pokedex;
        _opponentGenerator = opponentGenerator;
    }

    [HttpGet("info.asp")]
    public async Task<IActionResult> Info(int pid)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        // Probably an availability/status code.
        // Response codes:
        // 0x00: BSOD
        // 0x01: Continues normally
        // 0x02: BSOD
        // 0x03: Continues normally???
        // 0x04: Continues normally
        // 0x05: Unable to connect to the Wi-Fi Train. Returning to the reception counter. (13262)
        // 0x06: BSOD
        Response.Body.Write([0x01, 0x00]);

        return Ok();
    }

    [HttpGet("roomnum.asp")]
    public async Task<IActionResult> RoomNum()
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        //byte rank = data[0x00];
        Response.Body.Write([0x32, 0x00]);
        return Ok();
    }

    [HttpGet("download.asp")]
    public async Task<IActionResult> Download(int pid, string data)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        var request = Convert.FromBase64String(data);
        if (request.Length != 2)
        {
            return BadRequest();
        }

        byte rank = request[0];
        byte roomNum = request[1];

        if (rank > 9 || roomNum > 49)
        {
            return BadRequest();
        }

        FakeOpponentFactory5 fact = new();
        BattleSubwayRecord5[] opponents = Database.Instance.BattleSubwayGetOpponents5(_pokedex, pid, rank, roomNum);
        BattleSubwayProfile5[] leaders = Database.Instance.BattleSubwayGetLeaders5(_pokedex, rank, roomNum);
        BattleTowerRecordBase[] fakeOpponents = _opponentGenerator.GenerateFakeOpponents(fact, 7 - opponents.Length);

        foreach (BattleSubwayRecord5 record in fakeOpponents)
        {
            Response.Body.Write(record.Save());
        }

        foreach (BattleSubwayRecord5 record in opponents)
        {
            Response.Body.Write(record.Save());
        }

        foreach (BattleSubwayProfile5 leader in leaders)
        {
            Response.Body.Write(leader.Save());
        }

        if (leaders.Length < 30)
        {
            byte[] fakeLeader = new BattleSubwayProfile5
            (
                new EncodedString5("-----", 16),
                Versions.White, Languages.English,
                0, 0, 0x00000000, new TrendyPhrase5(0, 20, 0, 0), 0, 0
            ).Save();

            for (int x = leaders.Length; x < 30; x++)
            {
                Response.Body.Write(fakeLeader);
            }
        }

        return Ok();
    }

    [HttpGet("upload.asp")]
    public async Task<IActionResult> Upload(int pid, string data)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        var request = Convert.FromBase64String(data);
        if (request.Length != 388)
        {
            return BadRequest();
        }

        BattleSubwayRecord5 record = new(_pokedex, request, 0)
        {
            Rank = request[0xf0],
            RoomNum = request[0xf1],
            BattlesWon = request[0xf2],
            Unknown4 = new byte[5]
        };
        Array.Copy(request, 0xf3, record.Unknown4, 0, 5);
        record.Unknown5 = BitConverter.ToUInt64(request, 0xf8);
        record.PID = pid;

        foreach (var p in record.Party)
        {
            // todo: add battle tower specific checks:
            // item clause, species clause, banned species, banned items
            // https://bulbapedia.bulbagarden.net/wiki/Battle_Subway#Restrictions
            if (!p.Validate().IsValid)
            {
                // Tell the client it was successful so they don't keep retrying.
                Response.Body.Write([0x01, 0x00]);
                return Ok();
            }
        }

        // todo: Do we want to store their record anyway if they lost the first round?
        if (record.BattlesWon > 0)
            Database.Instance.BattleSubwayUpdateRecord5(record);
        if (record.BattlesWon == 7)
            Database.Instance.BattleSubwayAddLeader5(record);

        // List of responses:
        // 0x00: BSOD
        // 0x01: Uploads successfully
        // 0x02: That number cannot be specified for the Wi-Fi Train. (13263)
        // 0x03: BSOD
        // 0x04: The Wi-Fi Train is very crowded. Please try again later. (13261)
        // 0x05: Unable to connect to the Wi-Fi Train. Returning to the reception counter. (13262)
        // 0x06: BSOD
        // 0x07: BSOD
        // 0x08: BSOD
        Response.Body.Write([0x01, 0x00]);

        return Ok();
    }
}
