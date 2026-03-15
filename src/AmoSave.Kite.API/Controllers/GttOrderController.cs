using System.Text.Json;
using AmoSave.Kite.API.Data;
using AmoSave.Kite.API.Models;
using AmoSave.Kite.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AmoSave.Kite.API.Controllers;

[ApiController]
[Route("api/gtt")]
[Produces("application/json")]
public class GttOrderController : ControllerBase
{
    private readonly IKiteConnectService _kite;
    private readonly KiteDbContext _db;
    private readonly KiteConnectSettings _settings;
    private readonly ILogger<GttOrderController> _logger;

    public GttOrderController(IKiteConnectService kite, KiteDbContext db, IOptions<KiteConnectSettings> settings, ILogger<GttOrderController> logger)
    {
        _kite = kite;
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>Returns all GTT orders. Served from cache if fresh.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> GetGttOrders(
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromHeader(Name = "X-Access-Token")] string accessToken)
    {
        try
        {
            var expiry = DateTime.UtcNow.AddMinutes(-_settings.CacheExpiryMinutes);
            var cached = await _db.GttOrders
                .Where(g => g.UserId == userId && g.CachedAt > expiry)
                .ToListAsync();

            if (cached.Count > 0)
                return Ok(ApiResponse<object>.Success(cached));

            var result = await _kite.GetGttOrdersAsync(accessToken);
            if (!IsSuccess(result, out var data))
                return BadRequest(ApiResponse<object>.Error(GetErrorMessage(result)));

            await SyncGttOrdersAsync(userId, data);
            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get GTT orders for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>Returns a specific GTT order by trigger ID.</summary>
    [HttpGet("{triggerId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> GetGttOrder(
        int triggerId,
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromHeader(Name = "X-Access-Token")] string accessToken)
    {
        try
        {
            var cached = await _db.GttOrders.FirstOrDefaultAsync(g => g.TriggerId == triggerId && g.UserId == userId);
            if (cached != null)
                return Ok(ApiResponse<object>.Success(cached));

            var result = await _kite.GetGttOrderAsync(accessToken, triggerId);
            if (!IsSuccess(result, out var data))
                return NotFound(ApiResponse<object>.Error(GetErrorMessage(result)));

            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get GTT order {TriggerId}", triggerId);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>Places a new GTT (Good Till Triggered) order.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> PlaceGttOrder(
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromHeader(Name = "X-Access-Token")] string accessToken,
        [FromBody] PlaceGttRequest request)
    {
        try
        {
            var gttParams = BuildGttParams(request);
            var result = await _kite.PlaceGttOrderAsync(accessToken, gttParams);
            if (!IsSuccess(result, out var data))
                return BadRequest(ApiResponse<object>.Error(GetErrorMessage(result)));

            _logger.LogInformation("GTT order placed for user {UserId}", userId);
            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to place GTT order for user {UserId}", userId);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>Modifies an existing GTT order.</summary>
    [HttpPut("{triggerId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> ModifyGttOrder(
        int triggerId,
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromHeader(Name = "X-Access-Token")] string accessToken,
        [FromBody] PlaceGttRequest request)
    {
        try
        {
            var gttParams = BuildGttParams(request);
            var result = await _kite.ModifyGttOrderAsync(accessToken, triggerId, gttParams);
            if (!IsSuccess(result, out var data))
                return BadRequest(ApiResponse<object>.Error(GetErrorMessage(result)));

            // Invalidate cache for this GTT
            var cached = await _db.GttOrders.FirstOrDefaultAsync(g => g.TriggerId == triggerId);
            if (cached != null) _db.GttOrders.Remove(cached);
            await _db.SaveChangesAsync();

            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to modify GTT order {TriggerId}", triggerId);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    /// <summary>Deletes a GTT order.</summary>
    [HttpDelete("{triggerId:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteGttOrder(
        int triggerId,
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromHeader(Name = "X-Access-Token")] string accessToken)
    {
        try
        {
            var result = await _kite.DeleteGttOrderAsync(accessToken, triggerId);
            if (!IsSuccess(result, out var data))
                return BadRequest(ApiResponse<object>.Error(GetErrorMessage(result)));

            await _db.GttOrders.Where(g => g.TriggerId == triggerId).ExecuteDeleteAsync();
            return Ok(ApiResponse<object>.Success(data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete GTT order {TriggerId}", triggerId);
            return StatusCode(500, ApiResponse<object>.Error(ex.Message));
        }
    }

    private async Task SyncGttOrdersAsync(string userId, JsonElement data)
    {
        await _db.GttOrders.Where(g => g.UserId == userId).ExecuteDeleteAsync();
        if (data.ValueKind != JsonValueKind.Array) return;

        foreach (var item in data.EnumerateArray())
        {
            _db.GttOrders.Add(new GttOrder
            {
                TriggerId = item.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                UserId = userId,
                Type = GetString(item, "type"),
                Status = GetString(item, "status"),
                TradingSymbol = item.TryGetProperty("condition", out var cond) ? GetString(cond, "tradingsymbol") : "",
                Exchange = item.TryGetProperty("condition", out var exchCond) ? GetString(exchCond, "exchange") : "",
                Condition = item.TryGetProperty("condition", out var condData) ? condData.ToString() : "",
                Orders = item.TryGetProperty("orders", out var orders) ? orders.ToString() : "",
                CachedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }

    private static Dictionary<string, string> BuildGttParams(PlaceGttRequest request)
    {
        return new Dictionary<string, string>
        {
            ["type"] = request.TriggerType,
            ["condition"] = System.Text.Json.JsonSerializer.Serialize(request.Condition),
            ["orders"] = System.Text.Json.JsonSerializer.Serialize(request.Orders)
        };
    }

    private static bool IsSuccess(JsonElement element, out JsonElement data)
    {
        if (element.TryGetProperty("status", out var status) && status.GetString() == "success"
            && element.TryGetProperty("data", out data))
            return true;
        data = default;
        return false;
    }

    private static string GetErrorMessage(JsonElement element) =>
        element.TryGetProperty("message", out var msg) ? msg.GetString() ?? "Unknown error" : "Unknown error";

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var prop) && prop.ValueKind != JsonValueKind.Null
            ? prop.GetString() ?? "" : "";
}
