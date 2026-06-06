using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ScoutsAttendance.Application;
using ScoutsAttendance.Infrastructure;
using ScoutsAttendance.Infrastructure.Data;
using ScoutsAttendance.API.Middleware;
using ScoutsAttendance.API.Hubs;
using System.Text;

// ── Capture any startup exception so the app can still start and report it ──
var startupError = string.Empty;

var builder = WebApplication.CreateBuilder(args);

// Railway/Docker inject PORT — only bind manually in that case.
// On IIS (Windows hosting like Monstores) UseUrls must NOT be called;
// IIS manages the port via the ASPNETCORE_PORT / web.config integration.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Scouts Attendance API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Format: Bearer {token}",
        Name        = "Authorization",
        In          = ParameterLocation.Header,
        Type        = SecuritySchemeType.ApiKey,
        Scheme      = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHttpContextAccessor();

// Wrap infrastructure registration — if any library fails (e.g. QuestPDF/SkiaSharp
// native DLL missing) the app still starts and reports the error via /api/startup-error
try
{
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplication();
}
catch (Exception ex)
{
    startupError = $"[DI Registration Error]\n{ex}";
}

// Allow JWT key override via env var (set JWT_KEY in Railway for production).
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
          ?? builder.Configuration["Jwt:Key"]!;
builder.Configuration["Jwt:Key"] = jwtKey;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey        = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer          = true,
            ValidIssuer             = builder.Configuration["Jwt:Issuer"],
            ValidateAudience        = true,
            ValidAudience           = builder.Configuration["Jwt:Audience"],
            ValidateLifetime        = true,
            ClockSkew               = TimeSpan.Zero
        };

        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins(
                "http://localhost:4200",
                "http://localhost:4201",
                "https://localhost:4200",
                "https://MikhealAdelGaber.github.io",
                "http://mikha.runasp.net",
                "https://mikha.runasp.net"
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

var app = builder.Build();

// ── Diagnostic endpoint — always available, no auth required ─────────────────
// Visit http://mikha.runasp.net/api/startup-error to see what crashed
app.MapGet("/api/startup-error", () =>
{
    var info = new
    {
        status        = string.IsNullOrEmpty(startupError) ? "OK" : "ERROR",
        dotnetVersion = Environment.Version.ToString(),
        os            = Environment.OSVersion.ToString(),
        machineName   = Environment.MachineName,
        error         = string.IsNullOrEmpty(startupError) ? null : startupError
    };
    return Results.Json(info);
});

// ── Health check ─────────────────────────────────────────────────────────────
app.MapGet("/api/health", () => Results.Json(new { status = "healthy", time = DateTime.UtcNow }));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("AllowAngular");

try { app.UseStaticFiles(); } catch { /* ignore if wwwroot missing */ }

if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

try { app.MapHub<AttendanceHub>("/hubs/attendance"); } catch { /* SignalR optional */ }

// ── Seed the database ─────────────────────────────────────────────────────────
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DbSeeder.SeedAsync(db);
}
catch (Exception ex)
{
    startupError += $"\n[DB Seeder Error]\n{ex}";
    Console.Error.WriteLine($"DB seeder failed: {ex.Message}");
}

app.Run();
