﻿using GamestatsBase;
using Microsoft.AspNetCore.Mvc;
using PkmnFoundations.Data;
using PkmnFoundations.Wfc;

namespace gtsCore.Controllers.Pokemondpds;

[Route("pokemondpds/web/enc/lobby")]
[ApiController]
[GamestatsConfig("uLMOGEiiJogofchScpXb000244fd00006015100000005b440e7epokemondpds", GamestatsRequestVersions.Version3, GamestatsResponseVersions.Version2, encryptedRequest: true, requireSession: true)]
public class WebController : ControllerBase
{
    [HttpGet("checkProfile.asp")]
    public async Task<IActionResult> CheckProfile(int pid, string data)
    {
        var request = Convert.FromBase64String(data);
        if (request.Length != 168)
        {
            return BadRequest();
        }

        // I am going to guess that the PID provided second is the
        // one whose data should appear in the response.
        int requestedPid = BitConverter.ToInt32(request, 0);
        byte[] requestDataPrefix = new byte[12];
        byte[] requestData = new byte[152];

        Array.Copy(request, 4, requestDataPrefix, 0, 12);
        Array.Copy(request, 16, requestData, 0, 152);

        TrainerProfilePlaza requestProfile = new(pid, requestDataPrefix, requestData);
        Database.Instance.PlazaSetProfile(requestProfile);

        TrainerProfilePlaza responseProfile = Database.Instance.PlazaGetProfile(requestedPid);
        Response.Body.Write(responseProfile.Data, 0, 152);

        return Ok();
    }

    [HttpGet("getSchedule.asp")]
    public IActionResult GetSchedule()
    {
        // This is a replayed response from a game I had with Pipian.
        // It appears to be 49 ints.
        // todo(mythra): A real implementation
        // - we can generate events manually now, but we have a few
        // missing fields, so more research will need to be done before
        // that implementation.

        // note(mythra): this response is usually overwritten by the
        // peerchat server (through GETCHANKEY `b_lib_c_lobby`).
        // this is only taken if that channel key returns an
        // "empty" response.

        Random room_choice = Random.Shared;
        if (room_choice.Next() % 2 == 0)
        {
            // Mew Room w/ Arceus Footprint.
            Response.Body.Write(
            [
                            0x00, 0x00, 0x00, 0x00, 0xb0, 0x04, 0x00, 0x00,
                            0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
                            0x04, 0x00, 0x16, 0x00, 0x00, 0x00, 0x00, 0x00,
                            0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                            0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                            0x0b, 0x00, 0x00, 0x00, 0x0c, 0x03, 0x00, 0x00,
                            0x08, 0x00, 0x00, 0x00, 0x48, 0x03, 0x00, 0x00,
                            0x02, 0x00, 0x00, 0x00, 0x48, 0x03, 0x00, 0x00,
                            0x09, 0x00, 0x00, 0x00, 0x84, 0x03, 0x00, 0x00,
                            0x03, 0x00, 0x00, 0x00, 0x84, 0x03, 0x00, 0x00,
                            0x0a, 0x00, 0x00, 0x00, 0x84, 0x03, 0x00, 0x00,
                            0x0c, 0x00, 0x00, 0x00, 0xc0, 0x03, 0x00, 0x00,
                            0x04, 0x00, 0x00, 0x00, 0xc0, 0x03, 0x00, 0x00,
                            0x09, 0x00, 0x00, 0x00, 0xc0, 0x03, 0x00, 0x00,
                            0x0d, 0x00, 0x00, 0x00, 0xc0, 0x03, 0x00, 0x00,
                            0x0f, 0x00, 0x00, 0x00, 0xfc, 0x03, 0x00, 0x00,
                            0x05, 0x00, 0x00, 0x00, 0xfc, 0x03, 0x00, 0x00,
                            0x0e, 0x00, 0x00, 0x00, 0xfc, 0x03, 0x00, 0x00,
                            0x10, 0x00, 0x00, 0x00, 0x33, 0x04, 0x00, 0x00,
                            0x12, 0x00, 0x00, 0x00, 0x38, 0x04, 0x00, 0x00,
                            0x06, 0x00, 0x00, 0x00, 0x38, 0x04, 0x00, 0x00,
                            0x0d, 0x00, 0x00, 0x00, 0x38, 0x04, 0x00, 0x00,
                            0x11, 0x00, 0x00, 0x00, 0x74, 0x04, 0x00, 0x00,
                            0x0b, 0x00, 0x00, 0x00, 0xb0, 0x04, 0x00, 0x00,
                            0x13, 0x00, 0x00, 0x00
            ]);
        }
        else
        {
            // Grass Room without Arceus Footprint.
            Response.Body.Write(
            [
                            0x00, 0x00, 0x00, 0x00,
                            0xb0, 0x04, 0x00, 0x00, // Duration the room remains open for (seconds)
                            0x9e, 0xc4, 0x70, 0xa7, // Unknown, Mythra thinks it may be a random seed
                            0x00, 0x00, 0x00, 0x00, // Arceus footprint flag. 0 for disabled, 1 for enabled.
                            0x03, // Room type (0x03 = grass)
                            0x00, // "Season" tbd
                            0x16, 0x00, // Number of timed events (22)
                            // List of 22 events.
                            // Each event has an int for time and an int for what to do.
                            // Events are sorted according to time.
                            0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
                            0x00, 0x00, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00,
                            0x00, 0x00, 0x00, 0x00, 0x0b, 0x00, 0x00, 0x00,
                            0x0c, 0x03, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00,
                            0x48, 0x03, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00,
                            0x48, 0x03, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00,
                            0x84, 0x03, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00,
                            0x84, 0x03, 0x00, 0x00, 0x0a, 0x00, 0x00, 0x00,
                            0x84, 0x03, 0x00, 0x00, 0x0c, 0x00, 0x00, 0x00,
                            0xc0, 0x03, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00,
                            0xc0, 0x03, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00,
                            0xc0, 0x03, 0x00, 0x00, 0x0d, 0x00, 0x00, 0x00,
                            0xc0, 0x03, 0x00, 0x00, 0x0f, 0x00, 0x00, 0x00,
                            0xfc, 0x03, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00,
                            0xfc, 0x03, 0x00, 0x00, 0x0e, 0x00, 0x00, 0x00,
                            0xfc, 0x03, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00,
                            0x33, 0x04, 0x00, 0x00, 0x12, 0x00, 0x00, 0x00,
                            0x38, 0x04, 0x00, 0x00, 0x06, 0x00, 0x00, 0x00,
                            0x38, 0x04, 0x00, 0x00, 0x0d, 0x00, 0x00, 0x00,
                            0x38, 0x04, 0x00, 0x00, 0x11, 0x00, 0x00, 0x00,
                            0x74, 0x04, 0x00, 0x00, 0x0b, 0x00, 0x00, 0x00,
                            0xb0, 0x04, 0x00, 0x00, 0x13, 0x00, 0x00, 0x00
            ]);
        }

        return Ok();
    }

    [HttpGet("getVIP.asp")]
    public IActionResult GetVIP()
    {
        Response.Body.Write([0x00, 0x00, 0x00, 0x00]);

        foreach (var i in new[] { 600403373, 601315647, 601988829 })
        {
            Response.Body.Write(BitConverter.GetBytes(i));
            Response.Body.Write([0x00, 0x00, 0x00, 0x00]);
        }

        return Ok();
    }

    [HttpGet("getQuestionnaire.asp")]
    public IActionResult GetQuestionnaire()
    {
        Response.Body.Write([
                        0x00, 0x00, 0x00, 0x00, 0x2a, 0x01, 0x00,
                        0x00, 0x2d, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01,
                        0x01, 0x01, 0x01, 0x00, 0x01, 0x01, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x29, 0x01, 0x00,
                        0x00, 0x2c, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01,
                        0x01, 0x01, 0x01, 0x00, 0x01, 0x01, 0x00, 0x00,
                        0x00, 0x00, 0x00, 0x00, 0x00, 0x7e, 0x00, 0x00,
                        0x00, 0x46, 0x00, 0x00, 0x00, 0x33, 0x00, 0x00,
                        0x00, 0x64, 0x01, 0x00, 0x00, 0x11, 0x01, 0x00,
                        0x00, 0x83, 0x00, 0x00, 0x00
                    ]);

        return Ok();
    }

    [HttpGet("submitQuestionnaire.asp")]
    public IActionResult SubmitQuestionnaire()
    {
        // literally 'thx' in ascii... lol
        Response.Body.Write([0x00, 0x00, 0x00, 0x00, 0x74, 0x68, 0x78, 0x00]);

        return Ok();
    }
}
