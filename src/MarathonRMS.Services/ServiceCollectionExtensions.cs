using MarathonRMS.DataAccessObjects;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Auth;
using MarathonRMS.Services.Common;
using MarathonRMS.Services.Legal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarathonRMS.Services;

/// <summary>Đăng ký DI cho 3 lớp DAO – Repository – Service (BA mục 7.4). Mỗi module mới thêm đăng ký vào đây.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMarathonServices(this IServiceCollection services, IConfiguration configuration)
    {
        // DAO (scoped theo DbContext)
        services.AddScoped<ParticipantDAO>();
        services.AddScoped<ConsentRecordDAO>();
        services.AddScoped<LegalDocumentDAO>();
        services.AddScoped<SystemSettingDAO>();
        services.AddScoped<AuditLogDAO>();
        services.AddScoped<OrganizationDAO>();

        // Repository
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IParticipantRepository, ParticipantRepository>();
        services.AddScoped<IConsentRecordRepository, ConsentRecordRepository>();
        services.AddScoped<ILegalDocumentRepository, LegalDocumentRepository>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();

        // Hạ tầng
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSingleton<IDateTimeProvider, VietnamDateTimeProvider>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<ISystemSettingsProvider, SystemSettingsProvider>();

        // Nghiệp vụ
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ILegalService, LegalService>();

        return services;
    }
}
