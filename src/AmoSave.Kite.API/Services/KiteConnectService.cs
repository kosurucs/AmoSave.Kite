using System.Text.Json;
using AmoSave.Kite.API.Models;
using Microsoft.Extensions.Options;
using KC = KiteConnect;

namespace AmoSave.Kite.API.Services;

public interface IKiteConnectService
{
    string GetLoginUrl(string? redirectUrl = null);
    Task<JsonElement> GenerateSessionAsync(string requestToken);
    Task<JsonElement> InvalidateSessionAsync(string accessToken);
    Task<JsonElement> GetProfileAsync(string accessToken);
    Task<JsonElement> GetMarginsAsync(string accessToken, string? segment = null);
    Task<JsonElement> GetOrdersAsync(string accessToken);
    Task<JsonElement> GetOrderHistoryAsync(string accessToken, string orderId);
    Task<JsonElement> GetOrderTradesAsync(string accessToken, string orderId);
    Task<JsonElement> GetTradesAsync(string accessToken);
    Task<JsonElement> PlaceOrderAsync(string accessToken, string variety, Dictionary<string, string> orderParams);
    Task<JsonElement> ModifyOrderAsync(string accessToken, string variety, string orderId, Dictionary<string, string> orderParams);
    Task<JsonElement> CancelOrderAsync(string accessToken, string variety, string orderId, string? parentOrderId = null);
    Task<JsonElement> GetGttOrdersAsync(string accessToken);
    Task<JsonElement> GetGttOrderAsync(string accessToken, int triggerId);
    Task<JsonElement> PlaceGttOrderAsync(string accessToken, Dictionary<string, string> gttParams);
    Task<JsonElement> ModifyGttOrderAsync(string accessToken, int triggerId, Dictionary<string, string> gttParams);
    Task<JsonElement> DeleteGttOrderAsync(string accessToken, int triggerId);
    Task<JsonElement> GetHoldingsAsync(string accessToken);
    Task<JsonElement> GetPositionsAsync(string accessToken);
    Task<JsonElement> ConvertPositionAsync(string accessToken, Dictionary<string, string> positionParams);
    Task<JsonElement> GetQuoteAsync(string accessToken, IEnumerable<string> instruments);
    Task<JsonElement> GetOhlcAsync(string accessToken, IEnumerable<string> instruments);
    Task<JsonElement> GetLtpAsync(string accessToken, IEnumerable<string> instruments);
    Task<JsonElement> GetInstrumentsAsync(string? exchange = null);
    Task<JsonElement> GetHistoricalDataAsync(string accessToken, long instrumentToken, string interval, DateTime from, DateTime to, bool continuous = false, bool oi = false);
}

/// <summary>
/// Kite Connect service backed by the official Tech.Zerodha.KiteConnect library.
/// All API calls are delegated to <see cref="KC.Kite"/> instances created per-request.
/// <para>
/// The <c>Tech.Zerodha.KiteConnect</c> library uses a synchronous <c>HttpClient.Send()</c>
/// internally (it predates async/await). Every public method in this class therefore wraps
/// the synchronous library call in <c>Task.Run</c> so that it executes on a thread-pool
/// thread rather than blocking the ASP.NET Core request thread.
/// </para>
/// </summary>
public class KiteConnectService : IKiteConnectService
{
    private readonly KiteConnectSettings _settings;
    private readonly ILogger<KiteConnectService> _logger;

    public KiteConnectService(IOptions<KiteConnectSettings> settings, ILogger<KiteConnectService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    // ── Factory ──────────────────────────────────────────────────────────────

    /// <summary>Creates a <see cref="Kite"/> instance configured from application settings.</summary>
    private KC.Kite CreateKite(string? accessToken = null)
    {
        var kite = new KC.Kite(
            APIKey: _settings.ApiKey,
            Root: _settings.BaseUrl,
            Debug: _logger.IsEnabled(LogLevel.Debug));

        if (!string.IsNullOrEmpty(accessToken))
            kite.SetAccessToken(accessToken);

        return kite;
    }

    // ── Helper: serialise any object into a success envelope ─────────────────

    private static JsonElement OkEnvelope(object data)
    {
        var json = JsonSerializer.Serialize(new { status = "success", data });
        return JsonDocument.Parse(json).RootElement;
    }

    private static JsonElement ErrorEnvelope(string message, string errorType = "GeneralException")
    {
        var json = JsonSerializer.Serialize(new { status = "error", message, error_type = errorType });
        return JsonDocument.Parse(json).RootElement;
    }

    /// <summary>Serialises a <see cref="Dictionary{String,Object}"/> response returned by
    /// the Kite library (already contains the full <c>{status, data}</c> envelope with
    /// snake_case keys from the Kite API) into a <see cref="JsonElement"/>.</summary>
    private static JsonElement DictToElement(Dictionary<string, dynamic> dict)
    {
        var json = JsonSerializer.Serialize(dict);
        return JsonDocument.Parse(json).RootElement;
    }

    // ── Connection ────────────────────────────────────────────────────────────

    public string GetLoginUrl(string? redirectUrl = null)
    {
        var url = CreateKite().GetLoginURL();
        if (!string.IsNullOrWhiteSpace(redirectUrl))
            url += $"&redirect_params={Uri.EscapeDataString(redirectUrl)}";
        return url;
    }

    public Task<JsonElement> GenerateSessionAsync(string requestToken) =>
        Task.Run(() =>
        {
            try
            {
                var kite = CreateKite();
                KC.User user = kite.GenerateSession(requestToken, _settings.ApiSecret);
                _logger.LogDebug("Session generated for user {UserId}", user.UserId);
                return OkEnvelope(MapUser(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateSession failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> InvalidateSessionAsync(string accessToken) =>
        Task.Run(() =>
        {
            try
            {
                var result = CreateKite(accessToken).InvalidateAccessToken(accessToken);
                return DictToElement(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InvalidateSession failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    // ── User ──────────────────────────────────────────────────────────────────

    public Task<JsonElement> GetProfileAsync(string accessToken) =>
        Task.Run(() =>
        {
            try
            {
                KC.Profile profile = CreateKite(accessToken).GetProfile();
                return OkEnvelope(MapProfile(profile));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetProfile failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> GetMarginsAsync(string accessToken, string? segment = null) =>
        Task.Run(() =>
        {
            try
            {
                var kite = CreateKite(accessToken);
                if (segment != null)
                {
                    KC.UserMargin margin = kite.GetMargins(segment);
                    return OkEnvelope(MapMargin(margin));
                }
                else
                {
                    KC.UserMarginsResponse margins = kite.GetMargins();
                    return OkEnvelope(new
                    {
                        equity = MapMargin(margins.Equity),
                        commodity = MapMargin(margins.Commodity)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetMargins failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    // ── Orders ────────────────────────────────────────────────────────────────

    public Task<JsonElement> GetOrdersAsync(string accessToken) =>
        Task.Run(() =>
        {
            try
            {
                var orders = CreateKite(accessToken).GetOrders();
                return OkEnvelope(orders.Select(MapOrder).ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetOrders failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> GetOrderHistoryAsync(string accessToken, string orderId) =>
        Task.Run(() =>
        {
            try
            {
                var orders = CreateKite(accessToken).GetOrderHistory(orderId);
                return OkEnvelope(orders.Select(MapOrder).ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetOrderHistory failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> GetOrderTradesAsync(string accessToken, string orderId) =>
        Task.Run(() =>
        {
            try
            {
                var trades = CreateKite(accessToken).GetOrderTrades(orderId);
                return OkEnvelope(trades.Select(MapTrade).ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetOrderTrades failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> GetTradesAsync(string accessToken) =>
        Task.Run(() =>
        {
            try
            {
                var trades = CreateKite(accessToken).GetOrderTrades();
                return OkEnvelope(trades.Select(MapTrade).ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTrades failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> PlaceOrderAsync(string accessToken, string variety, Dictionary<string, string> orderParams) =>
        Task.Run(() =>
        {
            try
            {
                var kite = CreateKite(accessToken);
                var result = kite.PlaceOrder(
                    Exchange: orderParams.GetValueOrDefault("exchange", ""),
                    TradingSymbol: orderParams.GetValueOrDefault("tradingsymbol", ""),
                    TransactionType: orderParams.GetValueOrDefault("transaction_type", ""),
                    Quantity: decimal.TryParse(orderParams.GetValueOrDefault("quantity"), out var qty) ? qty : 0,
                    Price: decimal.TryParse(orderParams.GetValueOrDefault("price"), out var price) ? price : null,
                    Product: orderParams.GetValueOrDefault("product"),
                    OrderType: orderParams.GetValueOrDefault("order_type"),
                    Validity: orderParams.GetValueOrDefault("validity", KC.Constants.Validity.Day),
                    DisclosedQuantity: decimal.TryParse(orderParams.GetValueOrDefault("disclosed_quantity"), out var dq) ? dq : null,
                    TriggerPrice: decimal.TryParse(orderParams.GetValueOrDefault("trigger_price"), out var tp) ? tp : null,
                    Tag: orderParams.GetValueOrDefault("tag", ""),
                    Variety: variety);
                return DictToElement(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PlaceOrder failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> ModifyOrderAsync(string accessToken, string variety, string orderId, Dictionary<string, string> orderParams) =>
        Task.Run(() =>
        {
            try
            {
                var kite = CreateKite(accessToken);
                var result = kite.ModifyOrder(
                    OrderId: orderId,
                    Quantity: decimal.TryParse(orderParams.GetValueOrDefault("quantity"), out var qty) ? qty : null,
                    Price: decimal.TryParse(orderParams.GetValueOrDefault("price"), out var price) ? price : null,
                    TriggerPrice: decimal.TryParse(orderParams.GetValueOrDefault("trigger_price"), out var tp) ? tp : null,
                    DisclosedQuantity: decimal.TryParse(orderParams.GetValueOrDefault("disclosed_quantity"), out var dq) ? dq : null,
                    Validity: orderParams.GetValueOrDefault("validity", KC.Constants.Validity.Day),
                    OrderType: orderParams.GetValueOrDefault("order_type"),
                    Variety: variety);
                return DictToElement(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ModifyOrder failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> CancelOrderAsync(string accessToken, string variety, string orderId, string? parentOrderId = null) =>
        Task.Run(() =>
        {
            try
            {
                var result = CreateKite(accessToken).CancelOrder(orderId, variety, parentOrderId);
                return DictToElement(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CancelOrder failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    // ── GTT Orders ────────────────────────────────────────────────────────────

    public Task<JsonElement> GetGttOrdersAsync(string accessToken) =>
        Task.Run(() =>
        {
            try
            {
                var gtts = CreateKite(accessToken).GetGTTs();
                return OkEnvelope(gtts.Select(MapGtt).ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetGttOrders failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> GetGttOrderAsync(string accessToken, int triggerId) =>
        Task.Run(() =>
        {
            try
            {
                var gtt = CreateKite(accessToken).GetGTT(triggerId);
                return OkEnvelope(MapGtt(gtt));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetGttOrder {TriggerId} failed", triggerId);
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> PlaceGttOrderAsync(string accessToken, Dictionary<string, string> gttParams) =>
        Task.Run(() =>
        {
            try
            {
                var kite = CreateKite(accessToken);
                var gttParamObj = BuildGttParams(gttParams);
                var result = kite.PlaceGTT(gttParamObj);
                return DictToElement(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PlaceGttOrder failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> ModifyGttOrderAsync(string accessToken, int triggerId, Dictionary<string, string> gttParams) =>
        Task.Run(() =>
        {
            try
            {
                var kite = CreateKite(accessToken);
                var gttParamObj = BuildGttParams(gttParams);
                var result = kite.ModifyGTT(triggerId, gttParamObj);
                return DictToElement(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ModifyGttOrder {TriggerId} failed", triggerId);
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> DeleteGttOrderAsync(string accessToken, int triggerId) =>
        Task.Run(() =>
        {
            try
            {
                var result = CreateKite(accessToken).CancelGTT(triggerId);
                return DictToElement(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteGttOrder {TriggerId} failed", triggerId);
                return ErrorEnvelope(ex.Message);
            }
        });

    // ── Portfolio ─────────────────────────────────────────────────────────────

    public Task<JsonElement> GetHoldingsAsync(string accessToken) =>
        Task.Run(() =>
        {
            try
            {
                var holdings = CreateKite(accessToken).GetHoldings();
                return OkEnvelope(holdings.Select(MapHolding).ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetHoldings failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> GetPositionsAsync(string accessToken) =>
        Task.Run(() =>
        {
            try
            {
                KC.PositionResponse positions = CreateKite(accessToken).GetPositions();
                return OkEnvelope(new
                {
                    day = positions.Day.Select(MapPosition).ToArray(),
                    net = positions.Net.Select(MapPosition).ToArray()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPositions failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> ConvertPositionAsync(string accessToken, Dictionary<string, string> positionParams) =>
        Task.Run(() =>
        {
            try
            {
                var result = CreateKite(accessToken).ConvertPosition(
                    Exchange: positionParams.GetValueOrDefault("exchange", ""),
                    TradingSymbol: positionParams.GetValueOrDefault("tradingsymbol", ""),
                    TransactionType: positionParams.GetValueOrDefault("transaction_type", ""),
                    PositionType: positionParams.GetValueOrDefault("position_type", ""),
                    Quantity: int.TryParse(positionParams.GetValueOrDefault("quantity"), out var qty) ? qty : 0,
                    OldProduct: positionParams.GetValueOrDefault("old_product", ""),
                    NewProduct: positionParams.GetValueOrDefault("new_product", ""));
                return DictToElement(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConvertPosition failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    // ── Market Data ───────────────────────────────────────────────────────────

    public Task<JsonElement> GetQuoteAsync(string accessToken, IEnumerable<string> instruments) =>
        Task.Run(() =>
        {
            try
            {
                var instrumentArray = instruments.ToArray();
                var quotes = CreateKite(accessToken).GetQuote(instrumentArray);
                var data = quotes.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)MapQuote(kvp.Value));
                return OkEnvelope(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetQuote failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> GetOhlcAsync(string accessToken, IEnumerable<string> instruments) =>
        Task.Run(() =>
        {
            try
            {
                var instrumentArray = instruments.ToArray();
                var ohlcDict = CreateKite(accessToken).GetOHLC(instrumentArray);
                var data = ohlcDict.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)MapOhlc(kvp.Value));
                return OkEnvelope(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetOhlc failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> GetLtpAsync(string accessToken, IEnumerable<string> instruments) =>
        Task.Run(() =>
        {
            try
            {
                var instrumentArray = instruments.ToArray();
                var ltpDict = CreateKite(accessToken).GetLTP(instrumentArray);
                var data = ltpDict.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)MapLtp(kvp.Value));
                return OkEnvelope(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetLtp failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> GetInstrumentsAsync(string? exchange = null) =>
        Task.Run(() =>
        {
            try
            {
                var instruments = CreateKite().GetInstruments(exchange);
                var data = instruments.Select(i => new
                {
                    instrument_token = i.InstrumentToken,
                    exchange_token = i.ExchangeToken,
                    tradingsymbol = i.TradingSymbol,
                    name = i.Name,
                    last_price = i.LastPrice,
                    expiry = i.Expiry,
                    strike = i.Strike,
                    tick_size = i.TickSize,
                    lot_size = i.LotSize,
                    instrument_type = i.InstrumentType,
                    segment = i.Segment,
                    exchange = i.Exchange
                }).ToArray();
                return OkEnvelope(data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetInstruments failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    public Task<JsonElement> GetHistoricalDataAsync(string accessToken, long instrumentToken, string interval, DateTime from, DateTime to, bool continuous = false, bool oi = false) =>
        Task.Run(() =>
        {
            try
            {
                var candles = CreateKite(accessToken).GetHistoricalData(
                    InstrumentToken: instrumentToken.ToString(),
                    FromDate: from,
                    ToDate: to,
                    Interval: interval,
                    Continuous: continuous,
                    OI: oi);

                // Format candles as array-of-arrays to match the Kite API's native format
                // so that HistoricalController can parse them as expected.
                var candleArrays = candles.Select(c => new object[]
                {
                    c.TimeStamp.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    c.Open, c.High, c.Low, c.Close, c.Volume, c.OI
                }).ToArray();

                return OkEnvelope(new { candles = candleArrays });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetHistoricalData failed");
                return ErrorEnvelope(ex.Message);
            }
        });

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static object MapUser(KC.User u) => new
    {
        user_id = u.UserId,
        user_name = u.UserName,
        user_shortname = u.UserShortName,
        email = u.Email,
        user_type = u.UserType,
        broker = u.Broker,
        exchanges = u.Exchanges,
        products = u.Products,
        order_types = u.OrderTypes,
        avatar_url = u.AvatarURL,
        api_key = u.APIKey,
        access_token = u.AccessToken,
        public_token = u.PublicToken,
        refresh_token = u.RefreshToken,
        login_time = u.LoginTime
    };

    private static object MapProfile(KC.Profile p) => new
    {
        user_name = p.UserName,
        user_shortname = p.UserShortName,
        email = p.Email,
        user_type = p.UserType,
        broker = p.Broker,
        avatar_url = p.AvatarURL,
        exchanges = p.Exchanges,
        products = p.Products,
        order_types = p.OrderTypes
    };

    private static object MapMargin(KC.UserMargin m) => new
    {
        enabled = m.Enabled,
        net = m.Net,
        available = new
        {
            adhoc_margin = m.Available.AdHocMargin,
            cash = m.Available.Cash,
            collateral = m.Available.Collateral,
            intraday_payin = m.Available.IntradayPayin
        },
        utilised = new
        {
            debits = m.Utilised.Debits,
            exposure = m.Utilised.Exposure,
            m2m_realised = m.Utilised.M2MRealised,
            m2m_unrealised = m.Utilised.M2MUnrealised,
            option_premium = m.Utilised.OptionPremium,
            payout = m.Utilised.Payout,
            span = m.Utilised.Span,
            holding_sales = m.Utilised.HoldingSales,
            turnover = m.Utilised.Turnover
        }
    };

    private static object MapOrder(KC.Order o) => new
    {
        order_id = o.OrderId,
        exchange_order_id = o.ExchangeOrderId,
        parent_order_id = o.ParentOrderId,
        status = o.Status,
        status_message = o.StatusMessage,
        tradingsymbol = o.Tradingsymbol,
        exchange = o.Exchange,
        instrument_token = o.InstrumentToken,
        order_type = o.OrderType,
        transaction_type = o.TransactionType,
        validity = o.Validity,
        product = o.Product,
        quantity = o.Quantity,
        pending_quantity = o.PendingQuantity,
        filled_quantity = o.FilledQuantity,
        cancelled_quantity = o.CancelledQuantity,
        disclosed_quantity = o.DisclosedQuantity,
        price = o.Price,
        trigger_price = o.TriggerPrice,
        average_price = o.AveragePrice,
        tag = o.Tag,
        tags = o.Tags,
        variety = o.Variety,
        placed_by = o.PlacedBy,
        order_timestamp = o.OrderTimestamp,
        exchange_timestamp = o.ExchangeTimestamp
    };

    private static object MapTrade(KC.Trade t) => new
    {
        trade_id = t.TradeId,
        order_id = t.OrderId,
        exchange_order_id = t.ExchangeOrderId,
        tradingsymbol = t.Tradingsymbol,
        exchange = t.Exchange,
        instrument_token = t.InstrumentToken,
        transaction_type = t.TransactionType,
        product = t.Product,
        average_price = t.AveragePrice,
        quantity = t.Quantity,
        fill_timestamp = t.FillTimestamp,
        exchange_timestamp = t.ExchangeTimestamp
    };

    private static object MapHolding(KC.Holding h) => new
    {
        tradingsymbol = h.TradingSymbol,
        exchange = h.Exchange,
        instrument_token = h.InstrumentToken,
        isin = h.ISIN,
        product = h.Product,
        price = h.Price,
        quantity = h.Quantity,
        t1_quantity = h.T1Quantity,
        realised_quantity = h.RealisedQuantity,
        used_quantity = h.UsedQuantity,
        authorised_quantity = h.AuthorisedQuantity,
        collateral_quantity = h.CollateralQuantity,
        collateral_type = h.CollateralType,
        average_price = h.AveragePrice,
        last_price = h.LastPrice,
        close_price = h.ClosePrice,
        pnl = h.PNL,
        // The SDK's Holding model does not expose day_change / day_change_percentage
        // (these fields exist in the raw Kite API JSON but were not parsed by the library).
        // Consumers that need these values should query the Kite API directly or use
        // market quote data to compute them.
        day_change = 0m,
        day_change_percentage = 0m,
        discrepancy = h.Discrepancy
    };

    private static object MapPosition(KC.Position p) => new
    {
        tradingsymbol = p.TradingSymbol,
        exchange = p.Exchange,
        instrument_token = p.InstrumentToken,
        product = p.Product,
        quantity = p.Quantity,
        overnight_quantity = p.OvernightQuantity,
        multiplier = p.Multiplier,
        average_price = p.AveragePrice,
        close_price = p.ClosePrice,
        last_price = p.LastPrice,
        value = p.Value,
        pnl = p.PNL,
        m2m = p.M2M,
        unrealised = p.Unrealised,
        realised = p.Realised,
        buy_quantity = p.BuyQuantity,
        buy_price = p.BuyPrice,
        buy_value = p.BuyValue,
        buy_m2m = p.BuyM2M,
        sell_quantity = p.SellQuantity,
        sell_price = p.SellPrice,
        sell_value = p.SellValue,
        sell_m2m = p.SellM2M,
        day_buy_quantity = p.DayBuyQuantity,
        day_buy_price = p.DayBuyPrice,
        day_buy_value = p.DayBuyValue,
        day_sell_quantity = p.DaySellQuantity,
        day_sell_price = p.DaySellPrice,
        day_sell_value = p.DaySellValue
    };

    private static object MapGtt(KC.GTT g) => new
    {
        id = g.Id,
        type = g.TriggerType,
        status = g.Status,
        created_at = g.CreatedAt,
        updated_at = g.UpdatedAt,
        expires_at = g.ExpiresAt,
        condition = g.Condition.HasValue ? (object)new
        {
            exchange = g.Condition.Value.Exchange,
            tradingsymbol = g.Condition.Value.TradingSymbol,
            instrument_token = g.Condition.Value.InstrumentToken,
            trigger_values = g.Condition.Value.TriggerValues,
            last_price = g.Condition.Value.LastPrice
        } : null,
        orders = g.Orders?.Select(o => (object)new
        {
            product = o.Product,
            order_type = o.OrderType,
            transaction_type = o.TransactionType,
            quantity = o.Quantity,
            price = o.Price,
            result = o.Result
        }).ToArray()
    };

    private static object MapQuote(KC.Quote q) => new
    {
        instrument_token = q.InstrumentToken,
        last_price = q.LastPrice,
        last_quantity = q.LastQuantity,
        average_price = q.AveragePrice,
        volume = q.Volume,
        buy_quantity = q.BuyQuantity,
        sell_quantity = q.SellQuantity,
        change = q.Change,
        lower_circuit_limit = q.LowerCircuitLimit,
        upper_circuit_limit = q.UpperCircuitLimit,
        last_trade_time = q.LastTradeTime,
        timestamp = q.Timestamp,
        oi = q.OI,
        oi_day_high = q.OIDayHigh,
        oi_day_low = q.OIDayLow,
        ohlc = new { open = q.Open, high = q.High, low = q.Low, close = q.Close },
        depth = new
        {
            buy = q.Bids?.Select(b => new { quantity = b.Quantity, price = b.Price, orders = b.Orders }).ToArray(),
            sell = q.Offers?.Select(o => new { quantity = o.Quantity, price = o.Price, orders = o.Orders }).ToArray()
        }
    };

    private static object MapOhlc(KC.OHLC o) => new
    {
        instrument_token = o.InstrumentToken,
        last_price = o.LastPrice,
        ohlc = new { open = o.Open, high = o.High, low = o.Low, close = o.Close }
    };

    private static object MapLtp(KC.LTP l) => new
    {
        instrument_token = l.InstrumentToken,
        last_price = l.LastPrice
    };

    /// <summary>
    /// Constructs a <see cref="KC.GTTParams"/> from the key/value dictionary passed by the
    /// <see cref="Controllers.GttOrderController"/>.
    /// </summary>
    private KC.GTTParams BuildGttParams(Dictionary<string, string> p)
    {
        var gttParams = new KC.GTTParams
        {
            TriggerType = p.GetValueOrDefault("type", ""),
            TradingSymbol = "",
            Exchange = "",
            LastPrice = 0,
            TriggerPrices = new List<decimal>(),
            Orders = new List<KC.GTTOrderParams>()
        };

        // Parse the condition JSON (serialised by the controller)
        if (p.TryGetValue("condition", out var condJson))
        {
            try
            {
                var cond = JsonDocument.Parse(condJson).RootElement;
                gttParams.TradingSymbol = cond.TryGetProperty("tradingsymbol", out var ts) ? ts.GetString() ?? "" : "";
                gttParams.Exchange = cond.TryGetProperty("exchange", out var ex) ? ex.GetString() ?? "" : "";
                gttParams.LastPrice = cond.TryGetProperty("last_price", out var lp) ? lp.GetDecimal() : 0;
                if (cond.TryGetProperty("trigger_values", out var tv) && tv.ValueKind == JsonValueKind.Array)
                    gttParams.TriggerPrices = tv.EnumerateArray().Select(v => v.GetDecimal()).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse GTT condition JSON; condition params will be empty");
            }
        }

        // Parse the orders JSON array (serialised by the controller)
        if (p.TryGetValue("orders", out var ordersJson))
        {
            try
            {
                var ordersEl = JsonDocument.Parse(ordersJson).RootElement;
                if (ordersEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var o in ordersEl.EnumerateArray())
                    {
                        gttParams.Orders.Add(new KC.GTTOrderParams
                        {
                            TransactionType = o.TryGetProperty("transaction_type", out var tt) ? tt.GetString() ?? "" : "",
                            Quantity = o.TryGetProperty("quantity", out var qty) ? qty.GetInt32() : 0,
                            Price = o.TryGetProperty("price", out var price) ? price.GetDecimal() : 0,
                            Product = o.TryGetProperty("product", out var prod) ? prod.GetString() ?? "" : "",
                            OrderType = o.TryGetProperty("order_type", out var ot) ? ot.GetString() ?? "" : ""
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse GTT orders JSON; order params will be empty");
            }
        }

        return gttParams;
    }
}
