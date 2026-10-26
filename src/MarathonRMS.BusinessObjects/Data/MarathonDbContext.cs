using MarathonRMS.BusinessObjects.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MarathonRMS.BusinessObjects.Data;

public class MarathonDbContext : IdentityDbContext<AppUser>
{
    public MarathonDbContext(DbContextOptions<MarathonDbContext> options) : base(options) { }

    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationApplication> OrganizationApplications => Set<OrganizationApplication>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventPolicy> EventPolicies => Set<EventPolicy>();
    public DbSet<RaceCategory> RaceCategories => Set<RaceCategory>();
    public DbSet<PriceTier> PriceTiers => Set<PriceTier>();
    public DbSet<AgeGroup> AgeGroups => Set<AgeGroup>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();
    public DbSet<EventMedia> EventMedia => Set<EventMedia>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RaceKitPickup> RaceKitPickups => Set<RaceKitPickup>();
    public DbSet<TimingRecord> TimingRecords => Set<TimingRecord>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<PenaltyReason> PenaltyReasons => Set<PenaltyReason>();
    public DbSet<ResultPenalty> ResultPenalties => Set<ResultPenalty>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Giai đoạn 2
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<BibTransfer> BibTransfers => Set<BibTransfer>();
    public DbSet<ResultComplaint> ResultComplaints => Set<ResultComplaint>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Prize> Prizes => Set<Prize>();
    public DbSet<RaceRoute> RaceRoutes => Set<RaceRoute>();
    public DbSet<RoutePoi> RoutePois => Set<RoutePoi>();
    public DbSet<EventContentSection> EventContentSections => Set<EventContentSection>();
    public DbSet<EventScheduleItem> EventScheduleItems => Set<EventScheduleItem>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Thời điểm: datetime2(3), giờ Việt Nam (chạy local)
        builder.Properties<DateTime>().HavePrecision(3);
        builder.Properties<DateTime?>().HavePrecision(3);
        // Tiền VND: số nguyên
        builder.Properties<decimal>().HavePrecision(12, 0);
        builder.Properties<decimal?>().HavePrecision(12, 0);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MarathonDbContext).Assembly);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Enum lưu dạng chuỗi để CHECK constraint đọc được
            foreach (var property in entity.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type.IsEnum)
                {
                    var converterType = typeof(EnumToStringConverter<>).MakeGenericType(type);
                    property.SetValueConverter((ValueConverter)Activator.CreateInstance(converterType)!);
                    property.SetMaxLength(30);
                    property.SetIsUnicode(false);
                }
            }

            // Mặc định không xóa dây chuyền (tránh nhiều đường cascade trên SQL Server, BR-31)
            foreach (var fk in entity.GetForeignKeys().Where(fk => !fk.IsOwnership))
            {
                if (fk.DeleteBehavior == DeleteBehavior.Cascade && !CascadeAllowed(fk.PrincipalEntityType.ClrType, fk.DeclaringEntityType.ClrType))
                    fk.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }

    // Chỉ cho phép xóa dây chuyền với dữ liệu "thuộc về" cha khi giải còn bản nháp
    private static bool CascadeAllowed(Type principal, Type dependent) =>
        (principal, dependent) switch
        {
            _ when principal == typeof(Event) && dependent == typeof(EventPolicy) => true,
            _ when principal == typeof(Event) && dependent == typeof(RaceCategory) => true,
            _ when principal == typeof(Event) && dependent == typeof(EventMedia) => true,
            _ when principal == typeof(Event) && dependent == typeof(EventContentSection) => true,
            _ when principal == typeof(Event) && dependent == typeof(EventScheduleItem) => true,
            _ when principal == typeof(RaceCategory) && dependent == typeof(RaceRoute) => true,
            _ when principal == typeof(RaceCategory) && dependent == typeof(RoutePoi) => true,
            _ when principal == typeof(RaceCategory) && dependent == typeof(PriceTier) => true,
            _ when principal == typeof(RaceCategory) && dependent == typeof(Checkpoint) => true,
            _ when principal == typeof(Result) && dependent == typeof(ResultPenalty) => true,
            // Bảng Identity giữ nguyên hành vi mặc định
            _ when dependent.Namespace?.StartsWith("Microsoft.AspNetCore.Identity") == true => true,
            _ => false
        };
}
