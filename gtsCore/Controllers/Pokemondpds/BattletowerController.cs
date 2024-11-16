using GamestatsBase;
using gtsCore.Helpers;
using Microsoft.AspNetCore.Mvc;
using PkmnFoundations.Data;
using PkmnFoundations.Pokedex;
using PkmnFoundations.Structures;
using PkmnFoundations.Support;
using PkmnFoundations.Wfc;

namespace gtsCore.Controllers.Pokemondpds;

[Route("pokemondpds/battletower")]
[ApiController]
[GamestatsConfig("sAdeqWo3voLeC5r16DYv", 0x45, 0x1111, 0x80000000, 0x4a3b2c1d, "pokemondpds", GamestatsRequestVersions.Version2, GamestatsResponseVersions.Version1, encryptedRequest: true, requireSession: true)]
[BanMiddleware(Generations.Generation4)]
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
        Database.Instance.GamestatsBumpProfile4(pid, _ipAddressHelper.GetIpAddress(Request));

        // Response codes:
        // 0x00: BSOD
        // 0x01: Continues normally
        // 0x02: BSOD
        // 0x03: The Wi-Fi Battle Tower is currently undergoing maintenance. Please try again later.
        // 0x04: The Wi-Fi Battle Tower is very crowded. Please try again later.
        // 0x05: Unable to connect to the Wi-Fi Battle Tower. Returning to the reception counter.
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

        byte rank = request[0x00];
        byte roomNum = request[0x01];

        if (rank > 9 || roomNum > 49)
        {
            return BadRequest();
        }

        FakeOpponentFactory4 fact = new();
        BattleTowerRecord4[] opponents = Database.Instance.BattleTowerGetOpponents4(_pokedex, pid, rank, roomNum);
        BattleTowerProfile4[] leaders = Database.Instance.BattleTowerGetLeaders4(_pokedex, rank, roomNum);
        BattleTowerRecordBase[] fakeOpponents = _opponentGenerator.GenerateFakeOpponents(fact, 7 - opponents.Length);

        foreach (BattleTowerRecord4 record in fakeOpponents)
        {
            Response.Body.Write(record.Save(), 0, 228);
        }

        foreach (BattleTowerRecord4 record in opponents)
        {
            Response.Body.Write(record.Save(), 0, 228);
        }

        foreach (BattleTowerProfile4 leader in leaders)
        {
            Response.Body.Write(leader.Save(), 0, 34);
        }

        if (leaders.Length < 30)
        {
            byte[] fakeLeader = new BattleTowerProfile4
            (
                new EncodedString4("-----", 16),
                Versions.Platinum, Languages.English,
                0, 0, 0x00000000, new TrendyPhrase4(5, 0, 0, 0), 0, 0
            ).Save();

            for (int x = leaders.Length; x < 30; x++)
            {
                Response.Body.Write(fakeLeader, 0, 34);
            }
        }

        // This is completely insane. The game crashes when you
        // use Check Leaders if the response arrives too fast,
        // so we artificially delay it.
        // todo: This is slower than it needs to be if the
        // database is slow to respond. We should sleep for a
        // variable time based on when the request was received.
        Thread.Sleep(500);

        return Ok();
    }

    [HttpGet("upload.asp")]
    public async Task<IActionResult> Upload(int pid, string data)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        var request = Convert.FromBase64String(data);
        if (request.Length != 239)
        {
            return BadRequest();
        }

        BattleTowerRecord4 record = new(_pokedex, request, 0)
        {
            Rank = request[0xe4],
            RoomNum = request[0xe5],
            BattlesWon = request[0xe6],
            Unknown5 = BitConverter.ToUInt64(request, 0xe7),
            PID = pid
        };

        foreach (var p in record.Party)
        {
            // todo: add battle tower specific checks:
            // item clause, species clause, banned species, banned items
            // https://bulbapedia.bulbagarden.net/wiki/Battle_Tower_(Sinnoh)#Restrictions
            if (!p.Validate().IsValid)
            {
                // Tell the client it was successful so they don't keep retrying.
                Response.Body.Write([0x01, 0x00]);
                return Ok();
            }
        }

        // todo: Do we want to store their record anyway if they lost the first round?
        if (record.BattlesWon > 0)
            Database.Instance.BattleTowerUpdateRecord4(record);
        if (record.BattlesWon == 7)
            Database.Instance.BattleTowerAddLeader4(record);

        // List of responses:
        // 0x00: BSOD
        // 0x01: Uploads successfully
        // 0x02: That number cannot be specified for the Wi-Fi Battle Tower.
        // 0x03: BSOD
        // 0x04: The Wi-Fi Battle Tower is very crowded. Please try again later.
        // 0x05: Unable to connect to the Wi-Fi Battle Tower. Returning to the reception counter.
        // 0x06: BSOD
        // 0x07: BSOD
        // 0x08: BSOD
        Response.Body.Write([0x01, 0x00]);

        return Ok();
    }
}
