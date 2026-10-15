using MarathonRMS.BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarathonRMS.BusinessObjects.Data.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.Property(x => x.FullName).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
        b.HasOne(x => x.Organization).WithMany(o => o.Members).HasForeignKey(x => x.OrganizationId);
    }
}

public class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> b)
    {
        b.HasKey(x => x.ParticipantId);
        b.Property(x => x.FullName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Nationality).HasMaxLength(60);
        b.Property(x => x.IdNumber).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.EmergencyContactName).HasMaxLength(100);
        b.Property(x => x.EmergencyContactPhone).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.BloodType).HasMaxLength(5).IsUnicode(false);
        b.Property(x => x.MedicalNotes).HasMaxLength(500);
        b.Property(x => x.ClubName).HasMaxLength(100);

        b.HasOne(x => x.Owner).WithMany(u => u.Participants).HasForeignKey(x => x.OwnerUserId);
        b.HasOne(x => x.LinkedUser).WithMany().HasForeignKey(x => x.LinkedUserId);

        // Mỗi tài khoản có đúng 1 hồ sơ chính chủ
        b.HasIndex(x => x.OwnerUserId).IsUnique()
            .HasFilter("[Relationship] = 'Self' AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Participants_Owner_Self");
        // Một chủ tài khoản không khai trùng giấy tờ
        b.HasIndex(x => new { x.OwnerUserId, x.IdType, x.IdNumber }).IsUnique()
            .HasFilter("[IdNumber] IS NOT NULL AND [IsDeleted] = 0");

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Participants_Self_Linked", "[Relationship] = 'Self' OR [LinkedUserId] IS NULL");
            t.HasCheckConstraint("CK_Participants_IdNumber_Type", "[IdNumber] IS NULL OR [IdType] IS NOT NULL");
        });
    }
}
