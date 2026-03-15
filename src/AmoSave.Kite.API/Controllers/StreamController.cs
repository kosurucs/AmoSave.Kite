using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AmoSave.Kite.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AmoSave.Kite.API.Controllers;

/// <summary>
/// WebSocket Streaming endpoint for Kite Ticker market data.
/// Connect via: ws://&lt;host&gt;/api/stream/ticker?token=&lt;access-token&gt;&amp;instruments=408065,738561
/// </summary>
[ApiController]
[Route("api/stream")]
public class StreamController : ControllerBase
{
    private readonly KiteConnectSettings _settings;
    private readonly ILogger<StreamController> _logger;

    public StreamController(IOptions<KiteConnectSettings> settings, ILogger<StreamController> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Proxies a WebSocket connection to the Kite Ticker.
    /// Query parameters:
    ///   - token: access token
    ///   - instruments: comma-separated instrument tokens to subscribe
    ///   - mode: quote (default), full, or ltp
    /// </summary>
    [HttpGet("ticker")]
    public async Task ConnectTicker(
        [FromQuery] string token,
        [FromQuery] string instruments,
        [FromQuery] string mode = "quote")
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("WebSocket connection required");
            return;
        }

        using var clientWs = await HttpContext.WebSockets.AcceptWebSocketAsync();
        _logger.LogInformation("WebSocket client connected, subscribing instruments: {Instruments}", instruments);

        var kiteWsUrl = $"{_settings.WebSocketUrl}?api_key={_settings.ApiKey}&access_token={token}";

        using var kiteWs = new ClientWebSocket();
        kiteWs.Options.SetRequestHeader("X-Kite-Version", "3");

        try
        {
            await kiteWs.ConnectAsync(new Uri(kiteWsUrl), CancellationToken.None);
            _logger.LogInformation("Connected to Kite Ticker WebSocket");

            // Subscribe to instruments after connection
            var instrumentTokens = instruments.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s =>
                {
                    if (long.TryParse(s.Trim(), out var t)) return t;
                    _logger.LogWarning("Invalid instrument token (not a number): '{Token}'", s.Trim());
                    return 0L;
                })
                .Where(t => t > 0)
                .ToArray();

            if (instrumentTokens.Length > 0)
            {
                await SendSubscriptionAsync(kiteWs, instrumentTokens, mode);
            }

            using var cts = new CancellationTokenSource();

            // Relay data from Kite → Client and Client → Kite
            var kiteToClient = RelayAsync(kiteWs, clientWs, "Kite→Client", cts.Token);
            var clientToKite = RelayAsync(clientWs, kiteWs, "Client→Kite", cts.Token);

            var completed = await Task.WhenAny(kiteToClient, clientToKite);
            cts.Cancel();

            if (completed.IsFaulted)
                _logger.LogWarning(completed.Exception, "WebSocket relay faulted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket streaming error");

            if (clientWs.State == WebSocketState.Open)
            {
                var errorMsg = JsonSerializer.Serialize(new { error = ex.Message });
                await clientWs.SendAsync(
                    Encoding.UTF8.GetBytes(errorMsg),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
                await clientWs.CloseAsync(WebSocketCloseStatus.InternalServerError, ex.Message, CancellationToken.None);
            }
        }
    }

    private static async Task SendSubscriptionAsync(ClientWebSocket ws, long[] instrumentTokens, string mode)
    {
        // Kite Ticker binary subscription protocol:
        // Subscribe message format (JSON text frame)
        var subscribeMsg = JsonSerializer.Serialize(new
        {
            a = "subscribe",
            v = instrumentTokens
        });

        await ws.SendAsync(
            Encoding.UTF8.GetBytes(subscribeMsg),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);

        // Set mode
        var modeMsg = JsonSerializer.Serialize(new
        {
            a = "mode",
            v = new object[] { mode, instrumentTokens }
        });

        await ws.SendAsync(
            Encoding.UTF8.GetBytes(modeMsg),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }

    private static async Task RelayAsync(WebSocket source, WebSocket destination, string direction, CancellationToken ct)
    {
        var buffer = new byte[8192];
        while (source.State == WebSocketState.Open && destination.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await source.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await destination.CloseAsync(WebSocketCloseStatus.NormalClosure, "Upstream closed", CancellationToken.None);
                break;
            }

            await destination.SendAsync(
                new ArraySegment<byte>(buffer, 0, result.Count),
                result.MessageType,
                result.EndOfMessage,
                CancellationToken.None);
        }
    }
}
