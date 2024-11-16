using GamestatsBase;
using gtsCore.Helpers;
using Microsoft.AspNetCore.Mvc;
using PkmnFoundations.Data;
using PkmnFoundations.Structures;
using PkmnFoundations.Wfc;

namespace gtsCore.Controllers.Pokemondpds;

[Route("pokemondpds/common")]
[ApiController]
[GamestatsConfig("sAdeqWo3voLeC5r16DYv", 0x45, 0x1111, 0x80000000, 0x4a3b2c1d, "pokemondpds", GamestatsRequestVersions.Version2, GamestatsResponseVersions.Version1, encryptedRequest: true, requireSession: true)]
[BanMiddleware(Generations.Generation4)]
public class CommonController : ControllerBase
{
    private readonly GamestatsSessionManager _sessionManager;
    private readonly IpAddressHelper _ipAddressHelper;

    public CommonController(GamestatsSessionManager sessionManager, IpAddressHelper ipAddressHelper)
    {
        _sessionManager = sessionManager;
        _ipAddressHelper = ipAddressHelper;
    }

    [HttpGet("setProfile.asp")]
    public async Task<IActionResult> SetProfile(string data, int pid)
    {
        var session = HttpContext.Items["session"] as GamestatsSession;
        _sessionManager.Remove(session);

        var request = Convert.FromBase64String(data);
        if (request.Length != 100)
        {
            return BadRequest();
        }

#if !DEBUG
        try
        {
#endif
            byte[] profileBinary = new byte[100];
            Array.Copy(request, 0, profileBinary, 0, 100);
            var profile = new TrainerProfile4(pid, profileBinary, _ipAddressHelper.GetIpAddress(Request));
            Database.Instance.GamestatsSetProfile4(profile);
#if !DEBUG
        }
        catch { }
#endif
        short clientSecret = BitConverter.ToInt16(request, 96);
        short mailSecret = BitConverter.ToInt16(request, 98);

        // response:
        // 4 bytes of response code A
        // 4 bytes of response code B
        // Response code A values:
        // 0: Continues normally.
        // 1: The data was corrupted. It could not be sent.
        // 2: The server is undergoing maintenance. Please connect again later.
        // 3: BSOD
        if (mailSecret == -1)
        {
            // Register wii mail
            // Response code B values:
            // 0: There was a communication error.
            // 1: The Registration Code has been sent to your Wii console. Please enter the Registration Code.
            // 2: There was an error while attempting to send an authentication Wii message.
            // 3: There was a communication error.
            // 4: BSOD
            Response.Body.Write([0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00]);
        }
        else if (mailSecret != 0 || clientSecret != 0)
        {
            // Send wii mail confirmation code OR GTS when mail is configured (we can't tell them apart T__T)
            // (todo: We could use database to tell them apart.
            // If the previously stored profile has mailSecret == -1 then this is a wii mail confirmation.
            // If the previously stored profile has mailSecret == this mailSecret then this is GTS.)
            // Response code B values:
            // 0: Your Wii Number has been registered.
            // 1: There was a communication error.
            // 2: There was a communication error.
            // 3: Incorrect Registration Code.
            // 4: BSOD
            Response.Body.Write([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);
        }
        else
        {
            // GTS
            // Response code B values:
            // 0: Continues normally
            // 1: There was a communication error.
            // 2: There was a communication error.
            // 3: There was a Wii message authentication error.
            // 4: BSOD
            Response.Body.Write([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);
        }

        return Ok();
    }
}
