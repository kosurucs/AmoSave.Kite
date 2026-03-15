using AmoSave.Kite.API.Data;
using AmoSave.Kite.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AmoSave.Kite.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AlertController : ControllerBase
{
    private readonly KiteDbContext _db;
    private readonly ILogger<AlertController> _logger;

    public AlertController(KiteDbContext db, ILogger<AlertController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Returns all alerts for a user.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<Alert>>>> GetAlerts([FromHeader(Name = "X-User-Id")] string userId)
    {
        var alerts = await _db.Alerts.Where(a => a.UserId == userId).ToListAsync();
        return Ok(ApiResponse<List<Alert>>.Success(alerts));
    }

    /// <summary>Returns a specific alert by ID.</summary>
    [HttpGet("{alertId}")]
    public async Task<ActionResult<ApiResponse<Alert>>> GetAlert(
        string alertId,
        [FromHeader(Name = "X-User-Id")] string userId)
    {
        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.AlertId == alertId && a.UserId == userId);
        if (alert == null)
            return NotFound(ApiResponse<Alert>.Error("Alert not found"));
        return Ok(ApiResponse<Alert>.Success(alert));
    }

    /// <summary>Creates a new price alert (stored locally).</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<Alert>>> CreateAlert(
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromBody] CreateAlertRequest request)
    {
        var alert = new Alert
        {
            AlertId = Guid.NewGuid().ToString(),
            UserId = userId,
            TradingSymbol = request.TradingSymbol,
            Exchange = request.Exchange,
            Condition = request.Condition,
            TriggerValue = request.TriggerValue,
            Notes = request.Notes,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Alert created for user {UserId}: {AlertId}", userId, alert.AlertId);
        return CreatedAtAction(nameof(GetAlert), new { alertId = alert.AlertId }, ApiResponse<Alert>.Success(alert));
    }

    /// <summary>Deletes an alert.</summary>
    [HttpDelete("{alertId}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAlert(
        string alertId,
        [FromHeader(Name = "X-User-Id")] string userId)
    {
        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.AlertId == alertId && a.UserId == userId);
        if (alert == null)
            return NotFound(ApiResponse<object>.Error("Alert not found"));

        _db.Alerts.Remove(alert);
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<object>.Success(new { message = "Alert deleted" }));
    }

    /// <summary>
    /// Postback endpoint for Kite Connect order update notifications.
    /// Configure this URL in the Kite Connect developer console as the postback URL.
    /// </summary>
    [HttpPost("postback")]
    public async Task<ActionResult> HandlePostback([FromBody] AlertWebhookPayload payload)
    {
        _logger.LogInformation("Postback received: OrderId={OrderId}, Status={Status}", payload.OrderId, payload.Status);
        payload.ReceivedAt = DateTime.UtcNow;
        _db.AlertWebhookPayloads.Add(payload);

        // Mark matching active alert as triggered
        var matchingAlert = await _db.Alerts
            .FirstOrDefaultAsync(a => a.UserId == payload.UserId
                && a.TradingSymbol == payload.TradingSymbol
                && a.Exchange == payload.Exchange
                && a.Status == "active");

        if (matchingAlert != null)
        {
            matchingAlert.Status = "triggered";
            matchingAlert.TriggeredAt = DateTime.UtcNow;
            matchingAlert.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>Returns postback (order update) history for a user.</summary>
    [HttpGet("postbacks")]
    public async Task<ActionResult<ApiResponse<List<AlertWebhookPayload>>>> GetPostbacks(
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var payloads = await _db.AlertWebhookPayloads
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.ReceivedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(ApiResponse<List<AlertWebhookPayload>>.Success(payloads));
    }
}
