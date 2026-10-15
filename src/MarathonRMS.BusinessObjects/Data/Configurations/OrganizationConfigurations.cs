using MarathonRMS.BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarathonRMS.BusinessObjects.Data.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b)
    {
        b.HasKey(x => x.OrganizationId);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(200).IsUnicode(false).IsRequired();
        b.Property(x => x.TaxCode).HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(x => x.LogoUrl).HasMaxLength(500);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.ContactEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.ContactPhone).HasMaxLength(20).IsUnicode(false);
        b.HasIndex(x => x.Name).IsUnique();
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.TaxCode).IsUnique();
    }
}

public class OrganizationApplicationConfiguration : IEntityTypeConfiguration<OrganizationApplication>
{
    public void Configure(EntityTypeBuilder<OrganizationApplication> b)
    {
        b.HasKey(x => x.ApplicationId);
        b.Property(x => x.OrganizationName).HasMaxLength(200).IsRequired();
        b.Property(x => x.TaxCode).HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(x => x.RepresentativeName).HasMaxLength(100).IsRequired();
        b.Property(x => x.ContactEmail).HasMaxLength(256).IsRequired();
        b.Property(x => x.ContactPhone).HasMaxLength(20).IsUnicode(false).IsRequired();
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.DocumentUrl).HasMaxLength(500);
        b.Property(x => x.RejectReason).HasMaxLength(500);

        b.HasOne(x => x.Applicant).WithMany().HasForeignKey(x => x.ApplicantId);
        b.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById);
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);

        // BR-33: mỗi Member chỉ có 1 hồ sơ đang chờ duyệt
        b.HasIndex(x => x.ApplicantId).IsUnique()
            .HasFilter("[Status] = 'Pending'")
            .HasDatabaseName("UX_OrgApplications_Applicant_Pending");

        b.ToTable(t => t.HasCheckConstraint("CK_OrgApplications_RejectReason",
            "[Status] <> 'Rejected' OR [RejectReason] IS NOT NULL"));
    }
}

public class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> b)
    {
        b.HasKey(x => x.VenueId);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Address).HasMaxLength(300).IsRequired();
        b.Property(x => x.City).HasMaxLength(100).IsRequired();
        b.Property(x => x.Latitude).HasPrecision(9, 6);
        b.Property(x => x.Longitude).HasPrecision(9, 6);
        b.HasIndex(x => new { x.Name, x.City }).IsUnique();
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Venues_Latitude", "[Latitude] IS NULL OR [Latitude] BETWEEN -90 AND 90");
            t.HasCheckConstraint("CK_Venues_Longitude", "[Longitude] IS NULL OR [Longitude] BETWEEN -180 AND 180");
        });
    }
}
