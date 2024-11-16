using GamestatsBase;
using gtsCore.Helpers;
using Microsoft.AspNetCore.Mvc;
using PkmnFoundations.Data;
using PkmnFoundations.Pokedex;
using PkmnFoundations.Structures;
using PkmnFoundations.Support;
using PkmnFoundations.Wfc;

namespace gtsCore.Controllers.Syachi2ds;

[Route("syachi2ds/web/worldexchange")]
[ApiController]
[GamestatsConfig("HZEdGCzcGGLvguqUEKQN0001d93500002dd5000000082db842b2syachi2ds", GamestatsRequestVersions.Version3, GamestatsResponseVersions.Version2, encryptedRequest: false, requireSession: true)]
[BanMiddleware(Generations.Generation5)]
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
    public async Task<IActionResult> Info()
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        // todo: find out the meaning of this request.
        // is it simply done to check whether the GTS is online?
        Response.Body.Write([0x01, 0x00]);

        return Ok();
    }

    [HttpGet("result.asp")]
    public async Task<IActionResult> Result(int pid)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        // todo: more fun stuff is contained in this blob on genV.
        // my guess is that it's trainer profile info like setProfile.asp
        // There's a long string of 0s which could be a trainer card signature raster

        GtsRecord5 record = Database.Instance.GtsDataForUser5(_pokedex, pid);

        if (record == null)
        {
            // No pokemon in the system
            Response.Body.Write([0x05, 0x00]);
        }
        else if (record.IsExchanged > 0)
        {
            // traded pokemon arriving!!!
            Response.Body.Write(record.Save());
        }
        else
        {
            // my existing pokemon is in the system, untraded
            Response.Body.Write([0x04, 0x00]);
        }

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
        // todo: the same big blob of stuff from result.asp is sent here too.

        GtsRecord5 record = Database.Instance.GtsDataForUser5(_pokedex, pid);

        if (record == null)
        {
            // No pokemon in the system
            Response.Body.Write([0x05, 0x00]);
        }
        else
        {
            // just write the record whether traded or not...
            // todo: confirm that writing a traded record here will allow the trade to conclude
            Response.Body.Write(record.Save());
        }

        return Ok();
    }

    [HttpGet("delete.asp")]
    public async Task<IActionResult> Delete(int pid)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        // todo: the same big blob of stuff from result.asp is sent here too.

        GtsRecord5 record = Database.Instance.GtsDataForUser5(_pokedex, pid);
        if (record == null)
        {
            Response.Body.Write([0x05, 0x00]);
        }
        else if (record.IsExchanged > 0)
        {
            // Responses:
            // 0x03: BSOD
            // 0x05: 13263

            // delete the arrived pokemon from the system
            // todo: add transactions
            // todo: log the successful trade?
            // (either here or when the trade is done)
            bool success = Database.Instance.GtsDeletePokemon5(pid);
            if (success)
                Response.Body.Write([0x01, 0x00]);
            else
                Response.Body.Write([0x05, 0x00]);
        }
        else
        {
            // own pokemon is there, fail. Use return.asp instead.
            Response.Body.Write([0x05, 0x00]);
        }

        return Ok();
    }

    [HttpGet("return.asp")]
    public async Task<IActionResult> Return(int pid)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        GtsRecord5 record = Database.Instance.GtsDataForUser5(_pokedex, pid);
        if (record == null || // no pokemon in the system
            record.IsExchanged > 0 || // a traded pokemon is there, fail. Use delete.asp instead.
            !Database.Instance.GtsCheckLockStatus5(record.TradeId, pid)) // someone else is in the process of trading for this
        {
            Response.Body.Write([0x02, 0x00]);
        }
        else
        {
            // delete own pokemon
            // todo: add transactions
            bool success = Database.Instance.GtsDeletePokemon5(pid);
            if (success)
            {
                Response.Body.Write([0x01, 0x00]);
                // todo: invalidate cache
                //manager.RefreshStats();
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

        if (request.Length != 432)
        {
            _sessionManager.Remove(session);
            return BadRequest();
        }

        // todo: add transaction
        if (Database.Instance.GtsDataForUser5(_pokedex, pid) != null)
        {
            // there's already a pokemon inside
            // Force the player out so they'll recheck its status.
            _sessionManager.Remove(session);
            Response.Body.Write([0x0e, 0x00]);
            return Ok();
        }

        // keep the record in memory while we wait for post_finish.asp request
        byte[] recordBinary = new byte[296];
        Array.Copy(request, 0, recordBinary, 0, 296);
        GtsRecord5 record = new(_pokedex, recordBinary)
        {
            IsExchanged = 0
        };

        // todo: figure out what bytes 296-431 do:
        // appears to be 4 bytes of 00, 128 bytes of stuff, 4 bytes of 80 00 00 00
        // probably a pkvldtprod signature

        if (!record.Validate())
        {
            // hack check failed
            _sessionManager.Remove(session);

            // responses:
            // 0x00: bsod
            // 0x01: successful deposit
            // 0x02: Communication error 13265
            // 0x03: Communication error 13264
            // 0x04-0x06: bsod
            // 0x07: The GTS is very crowded now. Please try again later (13261). (and it boots you)
            // 0x08: That Pokémon may not be offered for trade (13268)!
            // 0x09: That Pokémon may not be offered for trade (13269)!
            // 0x0a: That Pokémon may not be offered for trade (13270)!
            // 0x0b: That Pokémon may not be offered for trade (13271)!
            // 0x0c: That Pokémon may not be offered for trade (13266)!
            // 0x0d: That Pokémon may not be offered for trade (13267)!
            // 0x0e: You were disconnected from the GTS. Error code: 13262 (and it boots you)
            // 0x0f: bsod
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

        // find a matching session which contains our record
        GamestatsSession? prevSession = _sessionManager.FindSession(pid, "/syachi2ds/web/worldexchange/post.asp");
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
        AssertHelper.Assert(prevSession.Tag is GtsRecord5);
        GtsRecord5 record = (GtsRecord5)prevSession.Tag;

        if (Database.Instance.GtsDepositPokemon5(record))
        {
            // todo: invalidate cache
            //manager.RefreshStats();
            Response.Body.Write([0x01, 0x00]);
        }
        else
            Response.Body.Write([0x02, 0x00]);


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

        int resultsCount = (int)request[6];

        ushort species = BitConverter.ToUInt16(request, 0);
        if (species < 1)
        {
            return BadRequest();
        }

        Response.Body.Write([0x01, 0x00]);

        if (resultsCount < 1) return Ok(); // optimize away requests for no rows

        Genders gender = (Genders)request[2];
        byte minLevel = request[3];
        byte maxLevel = request[4];
        // byte 5 unknown
        byte country = 0;
        if (request.Length > 7) country = request[7];

        if (resultsCount > 7) resultsCount = 7; // stop DDOS
        GtsRecord5[] records = Database.Instance.GtsSearch5(_pokedex, pid, species, gender, minLevel, maxLevel, country, resultsCount);
        foreach (GtsRecord5 record in records)
        {
            Response.Body.Write(record.Save());
        }

        Database.Instance.GtsSetLastSearch5(pid);

        return Ok();
    }

    [HttpGet("exchange.asp")]
    public async Task<IActionResult> Exchange(int pid, string data)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        var request = Convert.FromBase64String(data);

        if (request.Length != 432)
        {
            _sessionManager.Remove(session);
            return BadRequest();
        }

        byte[] uploadData = new byte[296];
        Array.Copy(request, 0, uploadData, 0, 296);
        GtsRecord5 upload = new(_pokedex, uploadData)
        {
            IsExchanged = 0
        };
        int targetPid = BitConverter.ToInt32(request, 296);
        GtsRecord5 result = Database.Instance.GtsDataForUser5(_pokedex, targetPid);
        DateTime? searchTime = Database.Instance.GtsGetLastSearch5(pid);

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
            // 0x08: That Pokémon may not be offered for trade (13268)!
            // 0x09: That Pokémon may not be offered for trade (13269)!
            // 0x0a: That Pokémon may not be offered for trade (13270)!
            // 0x0b: That Pokémon may not be offered for trade (13271)!
            // 0x0c: That Pokémon may not be offered for trade (13266)!
            // 0x0d: That Pokémon may not be offered for trade (13267)!
            // 0x0e: You were disconnected from the GTS. Error code: 13262
            // 0x0f: bsod
            Response.Body.Write([0x0c, 0x00]);
            return Ok();
        }

        if (!Database.Instance.GtsLockPokemon5(result.TradeId, pid))
        {
            // failed to acquire lock, implying someone else beat us here. Say already traded.
            _sessionManager.Remove(session);
            Response.Body.Write([0x02, 0x00]);
            return Ok();
        }

        GtsRecord5[] tag = [upload, result];
        session.Tag = tag;

        GtsRecord5 tradedResult = result.Clone();
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

        Response.Body.Write(result.Save());

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
        GamestatsSession? prevSession = _sessionManager.FindSession(pid, "/syachi2ds/web/worldexchange/exchange.asp");
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
        AssertHelper.Assert(prevSession.Tag is GtsRecord5[]);
        GtsRecord5[] tag = (GtsRecord5[])prevSession.Tag;
        AssertHelper.Assert(tag.Length == 2);

        GtsRecord5 upload = tag[0];
        GtsRecord5 result = tag[1];

        if (Database.Instance.GtsTradePokemon5(upload, result, pid))
            Response.Body.Write([0x01, 0x00]);
        else
            Response.Body.Write([0x02, 0x00]);

        return Ok();
    }

}
