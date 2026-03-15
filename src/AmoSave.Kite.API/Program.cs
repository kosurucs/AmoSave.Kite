using AmoSave.Kite.API.Data;
using AmoSave.Kite.API.Models;
using AmoSave.Kite.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ──────────────────────────────────────────────────────────────
builder.Services.Configure<KiteConnectSettings>(
    builder.Configuration.GetSection("KiteConnect"));

// ── Database (SQLite cache) ────────────────────────────────────────────────────
builder.Services.AddDbContext<KiteDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=amosave_kite.db"));

// ── HTTP Client (Kite Connect) ─────────────────────────────────────────────────
// KiteConnectService uses the Tech.Zerodha.KiteConnect library which manages its
// own HttpClient internally; no IHttpClientFactory registration is needed.
builder.Services.AddScoped<IKiteConnectService, KiteConnectService>();

// ── Services ───────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ISessionService, SessionService>();

// ── Controllers & WebSocket ────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── Swagger / OpenAPI ──────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AmoSave.Kite API",
        Version = "v1",
        Description = "Algo-trading middleware API that integrates with Zerodha Kite Connect v3. " +
                      "Provides caching via SQLite so repeated requests are served from the local database.",
        Contact = new OpenApiContact { Name = "AmoSave" }
    });

    c.AddSecurityDefinition("AccessToken", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Access-Token",
        Description = "Kite Connect access token"
    });

    c.AddSecurityDefinition("UserId", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-User-Id",
        Description = "Kite Connect user ID"
    });
});

// ── CORS ───────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// ── Auto-migrate database ──────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KiteDbContext>();
    db.Database.EnsureCreated();
}

// ── Middleware pipeline ────────────────────────────────────────────────────────
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AmoSave.Kite API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "AmoSave.Kite API";
    });
}

app.UseWebSockets();
app.UseAuthorization();
app.MapControllers();

app.Run();
