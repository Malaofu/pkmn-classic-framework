using GamestatsBase;
using gtsCore.Helpers;
using Microsoft.AspNetCore.Mvc;
using PkmnFoundations.Data;
using PkmnFoundations.Structures;
using PkmnFoundations.Wfc;

namespace gtsCore.Controllers.Syachi2ds;

[Route("syachi2ds/web/common")]
[ApiController]
[GamestatsConfig("HZEdGCzcGGLvguqUEKQN0001d93500002dd5000000082db842b2syachi2ds", GamestatsRequestVersions.Version3, GamestatsResponseVersions.Version2, encryptedRequest: false, requireSession: true)]
[BanMiddleware(Generations.Generation5)]
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
            // this blob appears to share the same format with GenIV only with (obviously) a GenV string for the trainer name
            // and the email-related fields dummied out.
            // Specifically, email, notification status, and the two secrets appear to always be 0.
            byte[] profileBinary = new byte[100];
            Array.Copy(request, 0, profileBinary, 0, 100);
            TrainerProfile5 profile = new(pid, profileBinary, _ipAddressHelper.GetIpAddress(Request));
            Database.Instance.GamestatsSetProfile5(profile);
#if !DEBUG
        }
        catch { }
#endif

        Response.Body.Write([0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

        return Ok();
    }
    
}
