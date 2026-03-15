# AmoSave.Kite
Kite Integration with Zerodha

## Overview

**AmoSave.Kite** is an ASP.NET Core Web API middleware that integrates with the [Zerodha Kite Connect v3 API](https://kite.trade/docs/connect/v3/). It caches all responses in a local SQLite database, so repeated requests are served from the cache instead of hitting Kite Connect every time.

## Features

| Feature | Endpoint Prefix |
|---------|----------------|
| **Connection / Authentication** | `POST /api/Connection/session` |
| **User Profile & Margins** | `GET /api/User/profile`, `/api/User/margins` |
| **Orders** | `GET/POST/PUT/DELETE /api/Order` |
| **GTT Orders** | `GET/POST/PUT/DELETE /api/gtt` |
| **Alerts & Postbacks** | `GET/POST /api/Alert`, `POST /api/Alert/postback` |
| **Portfolio (Holdings & Positions)** | `GET /api/Portfolio/holdings`, `/api/Portfolio/positions` |
| **Market Quotes & Instruments** | `GET /api/market/quote`, `/ohlc`, `/ltp`, `/instruments` |
| **Historical Candle Data** | `GET /api/historical/{token}/{interval}` |
| **WebSocket Streaming** | `WS /api/stream/ticker` |

## Quick Start

### 1. Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A Zerodha Kite Connect developer account and API key/secret from [Kite Connect Developer](https://developers.kite.trade/)

### 2. Configure
Edit `src/AmoSave.Kite.API/appsettings.json` (or use environment variables / user secrets):

```json
{
  "KiteConnect": {
    "ApiKey": "YOUR_KITE_API_KEY",
    "ApiSecret": "YOUR_KITE_API_SECRET",
    "CacheExpiryMinutes": 5,
    "InstrumentCacheHours": 24
  }
}
```

### 3. Run
```bash
cd src/AmoSave.Kite.API
dotnet run
```

The API starts at `http://localhost:5126`. Swagger UI is available at `http://localhost:5126/swagger`.

## Authentication Flow

1. **Get Login URL** → `GET /api/Connection/login-url`  
   Redirect the user to the returned `loginUrl` to authenticate with Zerodha.

2. **Generate Session** → `POST /api/Connection/session`  
   Send the `request_token` you received in the Kite callback:
   ```json
   { "requestToken": "<token_from_kite_callback>" }
   ```
   Returns `accessToken` and `userId` to use in subsequent requests.

3. **Use Headers** → All API endpoints require:
   - `X-User-Id: <userId>`
   - `X-Access-Token: <accessToken>`

4. **Check Session Validity** → `GET /api/Connection/session/status`  
   Sessions are valid until midnight IST (end of trading day). The API automatically tracks token expiry.

## Caching Strategy

- **Market quotes / orders / positions / holdings** — cached for `CacheExpiryMinutes` (default: 5 minutes)
- **Instruments list** — cached for `InstrumentCacheHours` (default: 24 hours)
- **Historical candles** — cached permanently; only missing date ranges are re-fetched from Kite
- **User profile** — cached for `CacheExpiryMinutes`

## WebSocket Streaming

Connect via WebSocket to proxy the Kite Ticker:

```
ws://localhost:5126/api/stream/ticker?token=<accessToken>&instruments=408065,738561&mode=full
```

Parameters:
- `token` — Kite access token
- `instruments` — comma-separated Kite instrument tokens
- `mode` — `ltp`, `quote` (default), or `full`

The server connects to the Kite Ticker WebSocket and relays all binary/text frames bidirectionally.

## Architecture

```
src/
└── AmoSave.Kite.API/
    ├── Controllers/          # HTTP endpoints
    │   ├── ConnectionController.cs
    │   ├── UserController.cs
    │   ├── OrderController.cs
    │   ├── GttOrderController.cs
    │   ├── AlertController.cs
    │   ├── PortfolioController.cs
    │   ├── MarketController.cs
    │   ├── HistoricalController.cs
    │   └── StreamController.cs    # WebSocket
    ├── Data/
    │   └── KiteDbContext.cs        # EF Core / SQLite
    ├── Models/                     # Domain models + DTOs
    ├── Services/
    │   ├── KiteConnectService.cs   # HTTP client for Kite Connect
    │   └── SessionService.cs      # Token lifecycle management
    ├── appsettings.json
    └── Program.cs
```

## Development Notes

- **No secrets in source control** — configure `ApiKey` and `ApiSecret` via environment variables or `dotnet user-secrets` in development.
- **SQLite database** (`amosave_kite.db`) is created automatically on first run.
- **Postback URL** — configure `http://<your-host>/api/Alert/postback` in the Kite Connect developer console to receive order update notifications.
