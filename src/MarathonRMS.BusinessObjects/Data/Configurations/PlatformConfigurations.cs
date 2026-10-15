using MarathonRMS.BusinessObjects.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarathonRMS.BusinessObjects.Data.Configurations;

public class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> b)
    {
        b.HasKey(x => x.ConsentId);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        b.HasOne(x => x.Registration).WithMany().HasForeignKey(x => x.RegistrationId);
        b.HasIndex(x => new { x.UserId, x.ConsentType });
        b.ToTable(t => t.HasCheckConstraint("CK_ConsentRecords_Version", "[DocumentVersion] >= 1"));
    }
}

public class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> b)
    {
        b.HasKey(x => x.DocumentId);
        b.Property(x => x.Content).IsRequired();
        b.Property(x => x.PublishedById).HasMaxLength(450);
        b.HasIndex(x => new { x.DocType, x.Version }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_LegalDocuments_Version", "[Version] >= 1"));
        b.HasData(SeedData.LegalDocuments);
    }
}

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> b)
    {
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(100).IsUnicode(false);
        b.Property(x => x.Value).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.UpdatedById).HasMaxLength(450);
        b.HasData(SeedData.SystemSettings);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.HasKey(x => x.AuditLogId);
        b.Property(x => x.UserId).HasMaxLength(450);
        b.Property(x => x.UserRole).HasMaxLength(100);
        b.Property(x => x.Action).HasMaxLength(100).IsUnicode(false).IsRequired();
        b.Property(x => x.EntityType).HasMaxLength(100).IsUnicode(false).IsRequired();
        b.Property(x => x.EntityId).HasMaxLength(100);
        b.Property(x => x.IpAddress).HasMaxLength(45).IsUnicode(false);
        b.HasIndex(x => new { x.EntityType, x.EntityId });
        b.HasIndex(x => x.CreatedAt);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<IdentityRole>
{
    public void Configure(EntityTypeBuilder<IdentityRole> b) => b.HasData(SeedData.Roles);
}
