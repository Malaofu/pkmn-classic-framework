using GamestatsBase;
using gtsCore.Helpers;
using Microsoft.AspNetCore.Mvc;
using PkmnFoundations.Data;
using PkmnFoundations.Pokedex;
using PkmnFoundations.Structures;
using PkmnFoundations.Support;
using PkmnFoundations.Wfc;

namespace gtsCore.Controllers.Pokemondpds;

[Route("pokemondpds/worldexchange")]
[ApiController]
[GamestatsConfig("sAdeqWo3voLeC5r16DYv", 0x45, 0x1111, 0x80000000, 0x4a3b2c1d, "pokemondpds", GamestatsRequestVersions.Version2, GamestatsResponseVersions.Version1, encryptedRequest: true, requireSession: true)]
[BanMiddleware(Generations.Generation4)]
public class WorldexchangeController : ControllerBase
{
    private readonly GamestatsSessionManager _sessionManager;
    private readonly IpAddressHelper _ipAddressHelper;
    private readonly Pokedex _pokedex;

    public WorldexchangeController(GamestatsSessionManager sessionManager, IpAddressHelper ipAddressHelper, Pokedex pokedex)
    {
        _sessionManager = sessionManager;
        _ipAddressHelper = ipAddressHelper;
        _pokedex = pokedex;
    }

    [HttpGet("info.asp")]
    public async Task<IActionResult> Info(int pid)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        // todo: find out the meaning of this request.
        // is it simply done to check whether the GTS is online?

        var ip = _ipAddressHelper.GetIpAddress(Request);
        Database.Instance.GamestatsBumpProfile4(pid, ip);

        Response.Body.Write([0x01, 0x00]);

        return Ok();
    }

    [HttpGet("result.asp")]
    public async Task<IActionResult> Result(int pid)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        /* After the above step(s) or performing any of 
        * the tasks below other than searching, the game 
        * makes a request to /pokemondpds/worldexchange/result.asp.
        * If the game has had a Pokémon sent to it via a trade, 
        * the server responds with the entire encrypted Pokémon 
        * save struct. Otherwise, if there is a Pokémon deposited 
        * in the GTS, it responds with 0x0004; if not, it responds 
        * with 0x0005. */

        GtsRecord4 record = Database.Instance.GtsDataForUser4(_pokedex, pid);

        if (record == null)
        {
            // No pokemon in the system
            Response.Body.Write([0x05, 0x00]);
        }
        else if (record.IsExchanged > 0)
        {
            // traded pokemon arriving!!!
            Response.Body.Write(record.Save(), 0, 292);
        }
        else
        {
            // my existing pokemon is in the system, untraded
            Response.Body.Write([0x04, 0x00]);
        }

        // other responses:
        // 0-2 causes a BSOD but it flashes siezure. Scary
        // 3 causes it to be "checking GTS's status" forever.
        // 6 is also the flashy BSOD. So probably all invalid values do that.

        return Ok();
    }

    [HttpGet("get.asp")]
    public async Task<IActionResult> Get(int pid)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        // This can be called in 3 circumstances:
        // 1. After result.asp when it says your existing pokemon is in the system
        // 2. When you check the summary of your pokemon (Platinum onward)
        // 3. When you attempt to retract your offer, just before saving.

        GtsRecord4 record = Database.Instance.GtsDataForUser4(_pokedex, pid);

        if (record == null)
        {
            // No pokemon in the system

            // response codes:
            // 0x01: BSOD
            // 0x02: BSOD
            // 0x03, entering GTS: Causes it to show no pokemon in the system, as if result.asp returned 5.
            // 0x03, checking summary: A communication error has occurred. You will be returned to the title screen. Please press the A Button.
            // 0x03, retracting: Communication error... (and it boots you)
            // 0x04: BSOD
            // 0x05, entering GTS: Causes it to show no pokemon in the system, as if result.asp returned 5.
            // 0x05, checking summary: A communication error has occurred. You will be returned to the title screen. Please press the A Button.
            // 0x05, retracting: Communication error... (and it boots you)
            // 0x06: BSOD
            // 0x07: BSOD
            Response.Body.Write([0x05, 0x00]);
        }
        else
        {
            // just write the record whether traded or not...
            // todo: confirm that writing a traded record here will allow the trade to conclude
            Response.Body.Write(record.Save(), 0, 292);
        }

        return Ok();
    }

    [HttpGet("delete.asp")]
    public async Task<IActionResult> Delete(int pid)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        GtsRecord4 record = Database.Instance.GtsDataForUser4(_pokedex, pid);
        if (record == null)
        {
            Response.Body.Write([0x03, 0x00]);
        }
        else if (record.IsExchanged > 0)
        {
            // Responses:
            // 0x00: BSOD
            // 0x01: Success
            // 0x02: BSOD
            // 0x03: A communication error has occurred. You have been disconnected from Nintendo Wi-Fi Connection. You will be returned to wherever you last saved. Please press the A Button.
            // 0x04: BSOD
            // 0x05: Either the GTS is experiencing high traffic volumes or the service is down. Please wait a while and try again. You have been disconnected from Nintendo Wi-Fi Connection, Please press the A Button.
            // 0x06: BSOD
            // 0x07: BSOD

            // delete the arrived pokemon from the system
            // todo: add transactions
            // todo: log the successful trade?
            // (either here or when the trade is done)
            bool success = Database.Instance.GtsDeletePokemon4(pid);
            if (success)
                Response.Body.Write([0x01, 0x00]);
            else
                Response.Body.Write([0x05, 0x00]);
        }
        else
        {
            // own pokemon is there, fail. Use return.asp instead.
            Response.Body.Write([0x03, 0x00]);
        }

        return Ok();
    }

    [HttpGet("return.asp")]
    public async Task<IActionResult> Return(int pid)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        GtsRecord4 record = Database.Instance.GtsDataForUser4(_pokedex, pid);

        if (record == null || // no pokemon in the system
            record.IsExchanged > 0 || // a traded pokemon is there, fail. Use delete.asp instead.
            !Database.Instance.GtsCheckLockStatus4(record.TradeId, pid)) // someone else is in the process of trading for this
        {
            Response.Body.Write([0x02, 0x00]);
        }
        else
        {
            // delete own pokemon
            // todo: add transactions
            bool success = Database.Instance.GtsDeletePokemon4(pid);
            if (success)
            {
                Response.Body.Write([0x01, 0x00]);
            }
            else
            {
                Response.Body.Write([0x02, 0x00]);
            }
        }

        return Ok();
    }

    [HttpGet("post.asp")]
    public async Task<IActionResult> Post(int pid, string data)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        var request = Convert.FromBase64String(data);

        if (request.Length != 292)
        {
            _sessionManager.Remove(session);
            return BadRequest();
        }

        // todo: add transaction
        if (Database.Instance.GtsDataForUser4(_pokedex, pid) != null)
        {
            // there's already a pokemon inside.
            // Force the player out so they'll recheck its status.
            _sessionManager.Remove(session);
            Response.Body.Write([0x0e, 0x00]);
            return Ok();
        }

        // keep the record in memory while we wait for post_finish.asp request
        byte[] recordBinary = new byte[292];
        Array.Copy(request, 0, recordBinary, 0, 292);
        GtsRecord4 record = new(_pokedex, recordBinary)
        {
            IsExchanged = 0
        };
        if (!record.Validate())
        {
            // hack check failed
            _sessionManager.Remove(session);

            // responses:
            // 0x00: Appears to start depositing? todo: test if this code leads to a normal deposit.
            // 0x01: successful deposit
            // 0x02-0x03: Communication error...
            // 0x04-0x06: bsod
            // 0x07: The GTS is very crowded now. Please try again later. (and it boots you!)
            // 0x08-0x0d: That Pokémon may not be offered for trade!
            // 0x0e: You were disconnected from the GTS. Returning to the reception counter.
            // 0x0f: Blue screen of death
            Response.Body.Write([0x0c, 0x00]);
            return Ok();
        }

        // the following two fields are blank in the uploaded record.
        // The server must provide them instead.
        record.TimeDeposited = DateTime.UtcNow;
        record.TimeExchanged = null;
        record.PID = pid;

        session.Tag = record;
        // todo: delete any other post.asp sessions registered under this PID

        Response.Body.Write([0x01, 0x00]);

        return Ok();
    }

    [HttpGet("post_finish.asp")]
    public async Task<IActionResult> PostFinish(int pid, string data)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        var request = Convert.FromBase64String(data);

        if (request.Length != 8)
        {
            return BadRequest();
        }

        // todo: these _finish requests seem to come with a magic number of 4 bytes
        // at offset 0. Find out what this is supposed to do and how to validate it.

        // find a matching session which contains our record
        GamestatsSession? prevSession = _sessionManager.FindSession(pid, "/pokemondpds/worldexchange/post.asp");
        if (prevSession == null)
        {
            Response.Body.Write([0x02, 0x00]);
            return Ok();
        }

        _sessionManager.Remove(prevSession);
        if (prevSession.Tag == null)
        {
            Response.Body.Write([0x02, 0x00]);
            return Ok();
        }
        AssertHelper.Assert(prevSession.Tag is GtsRecord4);
        GtsRecord4 record = (GtsRecord4)prevSession.Tag;

        if (Database.Instance.GtsDepositPokemon4(record))
        {
            // Responses:
            // 0x00: BSOD
            // 0x01: Success
            // 0x02: A communication error has occurred. You have been disconnected from Nintendo Wi-Fi Connection. You will be returned to wherever you last saved. Please press the A Button.
            // 0x03: Communication error... (and it thinks the upload was successful)
            // 0x04: BSOD
            // 0x05: A communication error has occurred. You have been disconnected from Nintendo Wi-Fi Connection. You will be returned to wherever you last saved. Please press the A Button.
            // 0x06: BSOD
            // 0x07: BSOD
            Response.Body.Write([0x01, 0x00]);
        }
        else
            Response.Body.Write([0x05, 0x00]);


        return Ok();
    }

    [HttpGet("search.asp")]
    public async Task<IActionResult> Search(int pid, string data)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        var request = Convert.FromBase64String(data);

        if (request.Length < 7 || request.Length > 8)
        {
            return BadRequest();
        }

        ushort species = BitConverter.ToUInt16(request, 0);
        if (species < 1)
        {
            return BadRequest();
        }

        int resultsCount = (int)request[6];
        if (resultsCount < 1) 
            return Ok(); // optimize away requests for no rows

        Genders gender = (Genders)request[2];
        byte minLevel = request[3];
        byte maxLevel = request[4];
        // byte 5 unknown
        byte country = 0;
        if (request.Length > 7) country = request[7];

        if (resultsCount > 7) resultsCount = 7; // stop DDOS
        GtsRecord4[] records = Database.Instance.GtsSearch4(_pokedex, pid, species, gender, minLevel, maxLevel, country, resultsCount);
        foreach (GtsRecord4 record in records)
        {
            Response.Body.Write(record.Save(), 0, 292);
        }

        Database.Instance.GtsSetLastSearch4(pid);

        return Ok();
    }

    [HttpGet("exchange.asp")]
    public async Task<IActionResult> Exchange(int pid, string data)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        var request = Convert.FromBase64String(data);

        if (request.Length != 296)
        {
            _sessionManager.Remove(session);
            return BadRequest();
        }

        byte[] uploadData = new byte[292];
        Array.Copy(request, 0, uploadData, 0, 292);
        GtsRecord4 upload = new(_pokedex, uploadData)
        {
            IsExchanged = 0
        };
        int targetPid = BitConverter.ToInt32(request, 292);
        GtsRecord4 result = Database.Instance.GtsDataForUser4(_pokedex, targetPid);
        DateTime? searchTime = Database.Instance.GtsGetLastSearch4(pid);

        if (result == null || searchTime == null ||
            result.TimeDeposited > (DateTime)searchTime || // If this condition is met, it means the pokemon in the system is DIFFERENT from the one the user is trying to trade for, ie. it was deposited AFTER the user did their search. The one the user wants was either taken back or traded.
            result.IsExchanged != 0)
        {
            // Pokémon is traded (or was never here to begin with)
            _sessionManager.Remove(session);
            Response.Body.Write([0x02, 0x00]);
            return Ok();
        }

        // enforce request requirements server side
        if (!upload.Validate() || !upload.CanTrade(result))
        {
            // todo: find the correct codes for these
            _sessionManager.Remove(session);

            // responses:
            // 0x00-0x01: bsod
            // 0x02: Unfortunately, it was traded to another Trainer.
            // 0x03-0x07: bsod
            // 0x08-0x0d: That Pokémon may not be offered for trade!
            // 0x0e: You were disconnected from the GTS. Returning to the reception counter.
            // 0x0f: bsod
            Response.Body.Write([0x0c, 0x00]);
            return Ok();
        }

        if (!Database.Instance.GtsLockPokemon4(result.TradeId, pid))
        {
            // failed to acquire lock, implying someone else beat us here. Say already traded.
            _sessionManager.Remove(session);
            Response.Body.Write([0x02, 0x00]);
            return Ok();
        }

        // uncomment these two lines if you're replaying gamestats requests and need to skip the random token
        //session = new GamestatsSession(this.GameId, this.Salt, pid, "/pokemondpds/worldexchange/exchange.asp");
        //SessionManager.Add(session);

        GtsRecord4[] tag = [upload, result];
        session.Tag = tag;

        GtsRecord4 tradedResult = result.Clone();
        tradedResult.FlagTraded(upload); // only real purpose is to generate a proper response

        // todo: we need a mechanism to "reserve" a pokemon being traded at this
        // point in the process, but be able to relinquish it if exchange_finish
        // never happens.
        // Currently, if two people try to take the same pokemon, it will appear
        // to work for both but then fail for the second after they've saved
        // their game. This causes a hard crash and a "save file is corrupt, 
        // "previous will be loaded" error when restarting.
        // the reservation can be done in application state and has no reason
        // to touch the database. (exchange_finish won't work anyway if application
        // state is lost.)

        // I also have a hunch that failure to send the exchange_finish request
        // is what causes the notorious GTS glitch where a pokemon is listed
        // under the wrong species and you can't trade it

        Response.Body.Write(result.Save(), 0, 292);

        return Ok();
    }

    [HttpGet("exchange_finish.asp")]
    public async Task<IActionResult> ExchangeFinish(int pid, string data)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        var request = Convert.FromBase64String(data);

        if (request.Length != 8)
        {
            return BadRequest();
        }

        // find a matching session which contains our record
        GamestatsSession? prevSession = _sessionManager.FindSession(pid, "/pokemondpds/worldexchange/exchange.asp");
        if (prevSession == null)
        {
            // response codes:
            // 0x00: I thought this meant fail but it also sometimes succeeds
            // 0x01: Success (the normal success response)
            // 0x02: Either the GTS is experiencing high traffic volumes or the service is down. Please wait a while and try again.
            // 0x03: Success (apparently)
            // 0x04: Success (apparently)
            // 0x05: Success (apparently)
            // 0x06: Success (apparently)
            // ...
            // 0x0f: Success (apparently)
            // I'm going to reason that responses other than 0x02 will all succeed, at least on platinum
            Response.Body.Write([0x02, 0x00]);
            return Ok();
        }

        _sessionManager.Remove(prevSession);
        if (prevSession.Tag == null)
        {
            Response.Body.Write([0x02, 0x00]);
            return Ok();
        }
        AssertHelper.Assert(prevSession.Tag is GtsRecord4[]);
        GtsRecord4[] tag = (GtsRecord4[])prevSession.Tag;
        AssertHelper.Assert(tag.Length == 2);

        GtsRecord4 upload = tag[0];
        GtsRecord4 result = tag[1];

        if (Database.Instance.GtsTradePokemon4(upload, result, pid))
            Response.Body.Write([0x01, 0x00]);
        else
            Response.Body.Write([0x02, 0x00]);

        return Ok();
    }

}
