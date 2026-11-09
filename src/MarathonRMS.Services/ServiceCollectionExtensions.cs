using MarathonRMS.DataAccessObjects;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Accounts;
using MarathonRMS.Services.Admin;
using MarathonRMS.Services.Auth;
using MarathonRMS.Services.Common;
using MarathonRMS.Services.Coupons;
using MarathonRMS.Services.EventContent;
using MarathonRMS.Services.Events;
using MarathonRMS.Services.Registrations;
using MarathonRMS.Services.Legal;
using MarathonRMS.Services.Organizations;
using MarathonRMS.Services.RaceDay;
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
        services.AddScoped<OrganizationApplicationDAO>();
        services.AddScoped<VenueDAO>();
        services.AddScoped<UserDAO>();
        services.AddScoped<CouponDAO>();
        services.AddScoped<EventContentDAO>();
        services.AddScoped<EventMediaDAO>();
        services.AddScoped<PenaltyReasonDAO>();
        // Phần B – giải
        services.AddScoped<EventDAO>();
        services.AddScoped<RaceCategoryDAO>();
        services.AddScoped<PriceTierDAO>();
        services.AddScoped<AgeGroupDAO>();
        services.AddScoped<CheckpointDAO>();
        services.AddScoped<RegistrationDAO>();
        services.AddScoped<PaymentDAO>();
        services.AddScoped<RegistrationChangeDAO>();
        services.AddScoped<EventLifecycleDAO>();
        services.AddScoped<PriceReferenceDAO>();
        services.AddScoped<PublicRunnerDAO>();
        services.AddScoped<DashboardDAO>();
        // Phần C – ngày thi & kết quả
        services.AddScoped<BibDAO>();
        services.AddScoped<RaceKitDAO>();
        services.AddScoped<TimingDAO>();
        services.AddScoped<ResultDAO>();
        services.AddScoped<ComplaintDAO>();
        services.AddScoped<PrizeDAO>();
        services.AddScoped<AchievementDAO>();
        services.AddScoped<MedicalDataDAO>();

        // Repository
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IParticipantRepository, ParticipantRepository>();
        services.AddScoped<IConsentRecordRepository, ConsentRecordRepository>();
        services.AddScoped<ILegalDocumentRepository, LegalDocumentRepository>();
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IOrganizationApplicationRepository, OrganizationApplicationRepository>();
        services.AddScoped<IVenueRepository, VenueRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddScoped<IEventContentRepository, EventContentRepository>();
        services.AddScoped<IEventMediaRepository, EventMediaRepository>();
        services.AddScoped<IPenaltyReasonRepository, PenaltyReasonRepository>();
        // Phần B – giải
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IRaceCategoryRepository, RaceCategoryRepository>();
        services.AddScoped<IPriceTierRepository, PriceTierRepository>();
        services.AddScoped<IAgeGroupRepository, AgeGroupRepository>();
        services.AddScoped<ICheckpointRepository, CheckpointRepository>();
        services.AddScoped<IRegistrationRepository, RegistrationRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IRegistrationChangeRepository, RegistrationChangeRepository>();
        services.AddScoped<IEventLifecycleRepository, EventLifecycleRepository>();
        services.AddScoped<IPriceReferenceRepository, PriceReferenceRepository>();
        services.AddScoped<IPublicRunnerRepository, PublicRunnerRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        // Phần C – ngày thi & kết quả
        services.AddScoped<IBibRepository, BibRepository>();
        services.AddScoped<IRaceKitRepository, RaceKitRepository>();
        services.AddScoped<ITimingRepository, TimingRepository>();
        services.AddScoped<IResultRepository, ResultRepository>();
        services.AddScoped<IComplaintRepository, ComplaintRepository>();
        services.AddScoped<IPrizeRepository, PrizeRepository>();
        services.AddScoped<IAchievementRepository, AchievementRepository>();
        services.AddScoped<IMedicalDataRepository, MedicalDataRepository>();

        // Hạ tầng
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddSingleton<IDateTimeProvider, VietnamDateTimeProvider>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<ISystemSettingsProvider, SystemSettingsProvider>();
        // Dùng chung cho B, C: lọc HTML (BR-50), quyền riêng tư công khai (BR-37), phạm vi BTC + xung đột lợi ích (BR-30, BR-32)
        services.AddSingleton<IHtmlContentSanitizer, HtmlContentSanitizer>();
        services.AddSingleton<IPrivacyFilter, PrivacyFilter>();
        services.AddScoped<IOrganizationScope, OrganizationScope>();

        // Nghiệp vụ
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ILegalService, LegalService>();

        // Phần A – hồ sơ, BTC, quản trị
        services.AddScoped<IConsentService, ConsentService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IOrganizationApplicationService, OrganizationApplicationService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IVenueService, VenueService>();
        services.AddScoped<ISystemSettingService, SystemSettingService>();
        // Phần A – Giai đoạn 2
        services.AddScoped<ICouponService, CouponService>();
        // Lưu file tải lên (BR-51). API cấu hình FileStorageOptions (thư mục wwwroot/uploads + địa chỉ công khai).
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<IEventContentService, EventContentService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IPenaltyReasonService, PenaltyReasonService>();
        // Phần A – đổi cự ly, chuyển nhượng BIB (UC-46, 47)
        services.AddScoped<IRegistrationChangeService, RegistrationChangeService>();
        // Phần A – hoãn / hủy giải, hoàn tiền (UC-25, 50)
        services.AddScoped<IEventLifecycleService, EventLifecycleService>();
        services.AddScoped<IRefundService, RefundService>();
        // Phần A – GĐ3: khoảng giá tham khảo (UC-87)
        services.AddScoped<IPriceReferenceService, PriceReferenceService>();
        // Phần A – GĐ3: danh sách VĐV công khai (UC-38)
        services.AddScoped<IPublicRunnerService, PublicRunnerService>();
        // Phần A – GĐ3: dashboard giải + hệ thống (UC-69, 71)
        services.AddScoped<IDashboardService, DashboardService>();

        // Phần B – giải, đăng ký, thanh toán (thêm bên dưới)
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IEventPolicyService, EventPolicyService>();
        services.AddScoped<IRaceCategoryService, RaceCategoryService>();
        services.AddScoped<EventSetupGuard>();
        services.AddScoped<IPriceTierService, PriceTierService>();
        services.AddScoped<IAgeGroupService, AgeGroupService>();
        services.AddScoped<ICheckpointService, CheckpointService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IPaymentService, PaymentService>();

        // Phần C – BIB, race kit, bấm giờ, kết quả (thêm bên dưới)
        services.AddSingleton<IBibAllocator, BibAllocator>();
        services.AddScoped<IBibService, BibService>();
        services.AddScoped<IRaceKitService, RaceKitService>();
        services.AddScoped<ITimingService, TimingService>();
        services.AddSingleton<IResultCalculator, ResultCalculator>();
        services.AddScoped<IResultRecalculation, ResultRecalculation>();
        services.AddScoped<IResultService, ResultService>();
        services.AddScoped<IComplaintService, ComplaintService>();
        services.AddSingleton<IPrizeAllocator, PrizeAllocator>();
        services.AddScoped<IPrizeService, PrizeService>();
        services.AddScoped<IAchievementService, AchievementService>();
        services.AddScoped<IMedicalDataRetentionService, MedicalDataRetentionService>();

        return services;
    }
}
