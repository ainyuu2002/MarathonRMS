using MarathonRMS.BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarathonRMS.BusinessObjects.Data.Configurations;

public class RegistrationConfiguration : IEntityTypeConfiguration<Registration>
{
    private const string Active = "[Status] IN ('PendingPayment','Confirmed')";

    public void Configure(EntityTypeBuilder<Registration> b)
    {
        b.HasKey(x => x.RegistrationId);
        b.Property(x => x.QrToken).HasMaxLength(64).IsUnicode(false).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(100).IsRequired();
        b.Property(x => x.BibName).HasMaxLength(16).IsRequired();
        b.Property(x => x.IdNumber).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.Nationality).HasMaxLength(60).IsRequired();
        b.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.EmergencyContactName).HasMaxLength(100).IsRequired();
        b.Property(x => x.EmergencyContactPhone).HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(x => x.BloodType).HasMaxLength(5).IsUnicode(false);
        b.Property(x => x.MedicalNotes).HasMaxLength(500);
        b.Property(x => x.ClubName).HasMaxLength(100);
        b.Property(x => x.GuardianName).HasMaxLength(100);
        b.Property(x => x.GuardianPhone).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.GuardianIdNumber).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.EscortName).HasMaxLength(100);
        b.Property(x => x.EscortPhone).HasMaxLength(20).IsUnicode(false);

        b.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        b.HasOne(x => x.Participant).WithMany().HasForeignKey(x => x.ParticipantId);
        b.HasOne(x => x.RegisteredBy).WithMany().HasForeignKey(x => x.RegisteredByUserId);

        // BR-01: mỗi người chạy 1 đăng ký còn hiệu lực / giải – theo Participant và theo số giấy tờ
        b.HasIndex(x => new { x.EventId, x.ParticipantId }).IsUnique().HasFilter(Active)
            .HasDatabaseName("UX_Registrations_Event_Participant_Active");
        b.HasIndex(x => new { x.EventId, x.IdType, x.IdNumber }).IsUnique()
            .HasFilter($"[IdNumber] IS NOT NULL AND {Active}")
            .HasDatabaseName("UX_Registrations_Event_IdNumber_Active");
        // BR-19: BIB duy nhất trong giải
        b.HasIndex(x => new { x.EventId, x.BibNumber }).IsUnique().HasFilter("[BibNumber] IS NOT NULL")
            .HasDatabaseName("UX_Registrations_Event_Bib");
        b.HasIndex(x => x.QrToken).IsUnique();
        b.HasIndex(x => new { x.CategoryId, x.Status });
        b.HasIndex(x => new { x.Status, x.ExpiresAt });

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Registrations_Prices",
                "[OriginalPrice] >= 0 AND [DiscountAmount] >= 0 AND [CoinDiscountAmount] >= 0 AND [FinalPrice] >= 0 " +
                "AND [FinalPrice] = [OriginalPrice] - [DiscountAmount] - [CoinDiscountAmount]");
            t.HasCheckConstraint("CK_Registrations_Consents", "[WaiverAccepted] = 1 AND [HealthDeclarationAccepted] = 1");
            t.HasCheckConstraint("CK_Registrations_Bib", "[BibNumber] IS NULL OR [BibNumber] > 0");
            t.HasCheckConstraint("CK_Registrations_BibOnlyConfirmed", "[BibNumber] IS NULL OR [Status] IN ('Confirmed','Transferred')");
            t.HasCheckConstraint("CK_Registrations_Counts", "[ChangeCount] >= 0 AND [TransferCount] >= 0");
            t.HasCheckConstraint("CK_Registrations_IdNumber_Type", "[IdNumber] IS NULL OR [IdType] IS NOT NULL");
        });
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.HasKey(x => x.PaymentId);
        b.Property(x => x.TxnRef).HasMaxLength(50).IsUnicode(false).IsRequired();
        b.Property(x => x.ProviderTransactionNo).HasMaxLength(50).IsUnicode(false);
        b.Property(x => x.ResponseCode).HasMaxLength(10).IsUnicode(false);
        b.HasOne(x => x.Registration).WithMany(r => r.Payments).HasForeignKey(x => x.RegistrationId);
        b.HasIndex(x => x.TxnRef).IsUnique();
        // BR-12: đơn 0đ không tạo Payment
        b.ToTable(t => t.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0"));
    }
}

public class RaceKitPickupConfiguration : IEntityTypeConfiguration<RaceKitPickup>
{
    public void Configure(EntityTypeBuilder<RaceKitPickup> b)
    {
        b.HasKey(x => x.PickupId);
        b.Property(x => x.ProxyName).HasMaxLength(100);
        b.Property(x => x.ProxyIdNumber).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasOne(x => x.Registration).WithOne(r => r.RaceKitPickup).HasForeignKey<RaceKitPickup>(x => x.RegistrationId);
        b.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId);
        b.HasIndex(x => x.RegistrationId).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_RaceKitPickups_Proxy",
            "[IsProxy] = 0 OR ([ProxyName] IS NOT NULL AND [ProxyIdNumber] IS NOT NULL)"));
    }
}
