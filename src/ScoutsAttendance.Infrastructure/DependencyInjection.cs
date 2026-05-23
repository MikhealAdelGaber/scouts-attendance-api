using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using ScoutsAttendance.Application.Interfaces;
using ScoutsAttendance.Application.Services;
using ScoutsAttendance.Infrastructure.Services;
using ScoutsAttendance.Infrastructure.Data;
using ScoutsAttendance.Infrastructure.Repositories;
using ScoutsAttendance.Infrastructure.Services;

namespace ScoutsAttendance.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Set QuestPDF community licence once at startup (Settings is in root QuestPDF namespace)
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        // Railway sets DATABASE_URL for PostgreSQL add-on; fall back to SQL Server for local dev
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

        // Npgsql 8 requires DateTime values to have Kind=Utc when writing to
        // "timestamp with time zone" columns.  Enable legacy behaviour as a safety net so
        // that any Unspecified-kind DateTime (e.g. from user input or date-only values)
        // is treated as local/UTC rather than throwing at runtime.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        services.AddDbContext<ApplicationDbContext>(opt =>
        {
            if (!string.IsNullOrEmpty(databaseUrl))
            {
                // Convert postgres://user:pass@host:port/db  →  Npgsql connection string
                var uri      = new Uri(databaseUrl);
                var userInfo = uri.UserInfo.Split(':', 2);
                // Railway's internal postgres runs on a private network — no SSL needed
                var sslMode  = uri.Host.EndsWith(".railway.internal") ? "Disable" : "Prefer";
                var npgsql   = $"Host={uri.Host};Port={uri.Port};" +
                               $"Database={uri.AbsolutePath.TrimStart('/')};" +
                               $"Username={userInfo[0]};Password={userInfo[1]};" +
                               $"SSL Mode={sslMode};Trust Server Certificate=true";
                opt.UseNpgsql(npgsql,
                    sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
            }
            else
            {
                opt.UseSqlServer(config.GetConnectionString("DefaultConnection"),
                    sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
            }
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IIpRateLimiter, InMemoryIpRateLimiter>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IQrCodeService, QrCodeService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddScoped<ICustomIdService, CustomIdService>();
        services.AddScoped<IMemberImportService, MemberImportService>();
        services.AddScoped<IQrPdfExportService, QrPdfExportService>();

        // Photo storage: Cloudinary when CLOUDINARY_URL is set, local wwwroot otherwise
        var cloudinaryUrl = Environment.GetEnvironmentVariable("CLOUDINARY_URL");
        if (!string.IsNullOrWhiteSpace(cloudinaryUrl))
        {
            // CloudinaryPhotoService needs an HttpClient for downloading photo bytes (PDF embedding)
            services.AddHttpClient<IPhotoService, CloudinaryPhotoService>();
        }
        else
        {
            services.AddScoped<IPhotoService, LocalPhotoService>();
        }

        return services;
    }
}
