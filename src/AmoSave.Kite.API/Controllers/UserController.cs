using System.Text.Json;
using AmoSave.Kite.API.Data;
using AmoSave.Kite.API.Models;
using AmoSave.Kite.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmoSave.Kite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UserController : ControllerBase
{
    private readonly IKiteConnectService _kite;
    private readonly KiteDbContext _db;
    private readonly KiteConnectSettings _settings;
    private readonly ILogger<UserController> _logger;

    public UserController(IKiteConnectService kite, KiteDbContext db, IOptions<KiteConnectSettings> settings, ILogger<UserController> logger)
    {
        _kite = kite;
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>Returns the user profile. Served from cache if available and fresh.</summary>
    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<object>>> GetProfile(
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromHeader(Name = "X-Access-Token")] string accessToken)
    {
        try
        {
            var expiry = DateTime.UtcNow.AddMinutes(-_settings.CacheExpiryMinutes);
            var cached = await _db.UserProfiles
                .FirstOrDefaultAsync(u => u.UserId == userId && u.CachedAt > expiry);

            if (cached != null)
            {
                _logger.LogDebug("Profile served from cache for user {UserId}", userId);
                return Ok(ApiResponse<object>.Success(cached));
            }

            var result = await _kite.GetProfileAsync(accessToken);
            if (!IsSuccess(result, out var data))
                return BadRequest(ApiResponse<object>.Error(GetErrorMessage(result)));

            // Persist to cache
            await _db.UserProfiles.Where(u => u.UserId == userId).ExecuteDeleteAsync();
            var profile = new UserProfile
            {
                UserId = userId,
                UserName = GetString(data, "user_name"),
                UserShortname = GetString(data, "user_shortname"),
                Email = GetString(data, "email"),
                UserType = GetString(data, "user_type"),
                Broker = GetString(data, "broker"),
                AvatarUrl = GetString(data, "avatar_url"),
                Meta = data.ToString(),
                CachedAt = DateTime.UtcNow
            };

            if (data.TryGetProperty("exchanges", out var exchanges))
                profile.Exchanges = exchanges.EnumerateArray().Select(e => e.GetString() ?? "").ToList();
            if (data.TryGetProperty("products", out var products))
                profile.Products = products.EnumerateArray().Select(e => e.GetString() ?? "").ToList();
            if (data.TryGetProperty("order_types", out var orderTypes))
                profile.OrderTypes = orderTypes.EnumerateArray().Select(e => e.GetString() ?? "").ToList();

            _db.UserProfiles.Add(profile);
            await _db.SaveChangesAsync();

            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get profile for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>Returns the user's available margins. Cached briefly.</summary>
    [HttpGet("margins")]
    public async Task<ActionResult<ApiResponse<object>>> GetMargins(
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromHeader(Name = "X-Access-Token")] string accessToken,
        [FromQuery] string? segment = null)
    {
        try
        {
            var expiry = DateTime.UtcNow.AddMinutes(-_settings.CacheExpiryMinutes);
            if (!string.IsNullOrEmpty(segment))
            {
                var cached = await _db.UserMargins
                    .FirstOrDefaultAsync(m => m.UserId == userId && m.Segment == segment && m.CachedAt > expiry);
                if (cached != null)
                    return Ok(ApiResponse<object>.Success(cached));
            }

            var result = await _kite.GetMarginsAsync(accessToken, segment);
            if (!IsSuccess(result, out var data))
                return BadRequest(ApiResponse<object>.Error(GetErrorMessage(result)));

            await UpsertMarginsAsync(userId, segment, data);
            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get margins for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    private async Task UpsertMarginsAsync(string userId, string? segment, JsonElement data)
    {
        var segments = segment != null
            ? new[] { segment }
            : new[] { "equity", "commodity" };

        foreach (var seg in segments)
        {
            if (!data.TryGetProperty(seg, out var segData)) continue;
            await _db.UserMargins.Where(m => m.UserId == userId && m.Segment == seg).ExecuteDeleteAsync();
            _db.UserMargins.Add(new UserMargin
            {
                UserId = userId,
                Segment = seg,
                Enabled = segData.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean(),
                Net = segData.TryGetProperty("net", out var net) ? net.GetDecimal() : 0,
                RawData = segData.ToString(),
                CachedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }

    private static bool IsSuccess(JsonElement element, out JsonElement data)
    {
        if (element.TryGetProperty("status", out var status) && status.GetString() == "success"
            && element.TryGetProperty("data", out data))
            return true;
        data = default;
        return false;
    }

    private static string GetErrorMessage(JsonElement element)
    {
        return element.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Unknown error" : "Unknown error";
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var prop) ? prop.GetString() ?? "" : "";
    }
}
