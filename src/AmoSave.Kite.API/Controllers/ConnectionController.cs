using System.Text.Json;
using AmoSave.Kite.API.Models;
using AmoSave.Kite.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace AmoSave.Kite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ConnectionController : ControllerBase
{
    private readonly IKiteConnectService _kite;
    private readonly ISessionService _sessionService;
    private readonly ILogger<ConnectionController> _logger;

    public ConnectionController(IKiteConnectService kite, ISessionService sessionService, ILogger<ConnectionController> logger)
    {
        _kite = kite;
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>Returns the Kite Connect login URL to initiate OAuth flow.</summary>
    [HttpGet("login-url")]
    public ActionResult<ApiResponse<object>> GetLoginUrl([FromQuery] string? redirectUrl = null)
    {
        var url = _kite.GetLoginUrl(redirectUrl);
        return Ok(ApiResponse<object>.Success(new { loginUrl = url }));
    }

    /// <summary>Generates a session (access token) using the request token obtained after Kite login.</summary>
    [HttpPost("session")]
    public async Task<ActionResult<ApiResponse<object>>> GenerateSession([FromBody] GenerateSessionRequest request)
    {
        try
        {
            var result = await _kite.GenerateSessionAsync(request.RequestToken);

            if (result.TryGetProperty("status", out var status) && status.GetString() == "success")
            {
                var data = result.GetProperty("data");
                var userId = data.TryGetProperty("user_id", out var uid) ? uid.GetString() ?? "" : "";
                var accessToken = data.TryGetProperty("access_token", out var at) ? at.GetString() ?? "" : "";
                var publicToken = data.TryGetProperty("public_token", out var pt) ? pt.GetString() ?? "" : "";
                var refreshToken = data.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";
                var loginTime = data.TryGetProperty("login_time", out var lt) ? lt.GetDateTime() : DateTime.UtcNow;

                var session = await _sessionService.SaveSessionAsync(userId, accessToken, publicToken, refreshToken, loginTime);
                _logger.LogInformation("Session generated for user {UserId}", userId);

                return Ok(ApiResponse<object>.Success(new
                {
                    userId,
                    accessToken,
                    publicToken,
                    loginTime = session.LoginTime,
                    tokenExpiry = session.TokenExpiry
                }));
            }

            var errorMsg = result.TryGetProperty("message", out var msg) ? msg.GetString() : "Session generation failed";
            return BadRequest(ApiResponse<object>.Error(errorMsg ?? "Session generation failed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate session");
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>Invalidates the current session (logout).</summary>
    [HttpDelete("session")]
    public async Task<ActionResult<ApiResponse<object>>> InvalidateSession(
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromHeader(Name = "X-Access-Token")] string accessToken)
    {
        try
        {
            await _kite.InvalidateSessionAsync(accessToken);
            await _sessionService.InvalidateSessionAsync(userId);
            return Ok(ApiResponse<object>.Success(new { message = "Session invalidated successfully" }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate session for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>Checks if the session for a user is still valid.</summary>
    [HttpGet("session/status")]
    public async Task<ActionResult<ApiResponse<object>>> GetSessionStatus([FromHeader(Name = "X-User-Id")] string userId)
    {
        var session = await _sessionService.GetActiveSessionAsync(userId);
        var isValid = session != null;

        return Ok(ApiResponse<object>.Success(new
        {
            isValid,
            userId,
            loginTime = session?.LoginTime,
            tokenExpiry = session?.TokenExpiry
        }));
    }
}

public class GenerateSessionRequest
{
    public string RequestToken { get; set; } = string.Empty;
}
