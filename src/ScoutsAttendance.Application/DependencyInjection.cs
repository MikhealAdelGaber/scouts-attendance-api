using Microsoft.Extensions.DependencyInjection;
using ScoutsAttendance.Application.Services;

namespace ScoutsAttendance.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService,           AuthService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IGroupService,          GroupService>();
        services.AddScoped<ITroopService,          TroopService>();
        services.AddScoped<IMemberService,         MemberService>();
        services.AddScoped<IEventService,          EventService>();
        services.AddScoped<IAttendanceService,     AttendanceService>();
        services.AddScoped<IPointsService,         PointsService>();
        services.AddScoped<ILeaderboardService,    LeaderboardService>();
        services.AddScoped<ITransferService,       TransferService>();
        services.AddScoped<IExcuseService,         ExcuseService>();
        services.AddScoped<IProfileService,        ProfileService>();
        services.AddScoped<IExportService,         ExportService>();
        services.AddScoped<IExamScoreService,      ExamScoreService>();
        services.AddScoped<IDashboardService,      DashboardService>();
        return services;
    }
}
