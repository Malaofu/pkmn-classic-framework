using GamestatsBase;
using Microsoft.AspNetCore.Mvc;

namespace gtsCore.Controllers.admin;

[Route("admin")]
[ApiController]
public class SessionsController : ControllerBase
{
    private readonly GamestatsSessionManager _sessionManager;

    public SessionsController(GamestatsSessionManager sessionManager)
    {
        _sessionManager = sessionManager;
    }

    [HttpGet("Sessions")]
    public async Task<IActionResult> Sessions()
    {
        var sessions = _sessionManager.Sessions;
        return Ok(sessions);
    }

    [HttpGet("Session")]
    public async Task<IActionResult> Session(int pid, string url)
    {
        var session = _sessionManager.FindSession(pid, url);
        if (session == null)
            return NoContent();
        return Ok(session);
    }
}
