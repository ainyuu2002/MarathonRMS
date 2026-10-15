using MarathonRMS.BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarathonRMS.BusinessObjects.Data.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> b)
    {
        b.HasKey(x => x.EventId);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Slug).HasMaxLength(200).IsUnicode(false).IsRequired();
        b.Property(x => x.BannerUrl).HasMaxLength(500);
        b.Property(x => x.WaiverText).IsRequired();
        b.Property(x => x.CancelReason).HasMaxLength(1000);
        b.Property(x => x.EmergencyPhone).HasMaxLength(20).IsUnicode(false);
        b.Property(x => x.InsuranceNote).HasMaxLength(1000);
        b.Property(x => x.CreatedById).HasMaxLength(450).IsRequired();

        b.HasOne(x => x.Organization).WithMany(o => o.Events).HasForeignKey(x => x.OrganizationId);
        b.HasOne(x => x.Venue).WithMany().HasForeignKey(x => x.VenueId);
        b.HasOne(x => x.Policy).WithOne(p => p.Event).HasForeignKey<EventPolicy>(p => p.EventId);

        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => new { x.Status, x.EventDate });

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Events_RegistrationWindow",
                "[RegistrationOpenAt] < [RegistrationCloseAt] AND CAST([RegistrationCloseAt] AS date) <= [EventDate]");
            t.HasCheckConstraint("CK_Events_EndDate", "[EndDate] >= [EventDate]");
            t.HasCheckConstraint("CK_Events_WaiverVersion", "[WaiverVersion] >= 1");
        });
    }
}

public class EventPolicyConfiguration : IEntityTypeConfiguration<EventPolicy>
{
    public void Configure(EntityTypeBuilder<EventPolicy> b)
    {
        b.HasKey(x => x.EventId);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_EventPolicies_GuardianAge", "[GuardianConsentUnderAge] BETWEEN 0 AND 21");
            t.HasCheckConstraint("CK_EventPolicies_MaxDependents", "[MaxDependentsPerAccount] BETWEEN 0 AND 10");
            t.HasCheckConstraint("CK_EventPolicies_Fees", "[TransferFee] >= 0 AND [CategoryChangeFee] >= 0 AND [ComplaintFee] >= 0");
            t.HasCheckConstraint("CK_EventPolicies_PaidTransfer", "[TransferPolicy] <> 'Paid' OR [TransferFee] > 0");
            t.HasCheckConstraint("CK_EventPolicies_MaxChanges", "[MaxTransfers] >= 0 AND [MaxCategoryChanges] >= 0");
            t.HasCheckConstraint("CK_EventPolicies_ComplaintWindow", "[ComplaintWindowMinutes] BETWEEN 10 AND 20160");
            t.HasCheckConstraint("CK_EventPolicies_LateStart", "[LateStartToleranceMinutes] IS NULL OR [LateStartToleranceMinutes] > 0");
        });
    }
}

public class RaceCategoryConfiguration : IEntityTypeConfiguration<RaceCategory>
{
    public void Configure(EntityTypeBuilder<RaceCategory> b)
    {
        b.HasKey(x => x.CategoryId);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.DistanceKm).HasPrecision(6, 2);
        b.Property(x => x.ActualDistanceKm).HasPrecision(6, 2);
        b.Property(x => x.BibColor).HasMaxLength(20);
        b.Property(x => x.MedalImageUrl).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Event).WithMany(e => e.Categories).HasForeignKey(x => x.EventId);
        b.HasIndex(x => new { x.EventId, x.Name }).IsUnique();

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_RaceCategories_Distance", "[DistanceKm] > 0 AND ([ActualDistanceKm] IS NULL OR [ActualDistanceKm] > 0)");
            t.HasCheckConstraint("CK_RaceCategories_Capacity", "[Capacity] > 0");
            t.HasCheckConstraint("CK_RaceCategories_Age", "[MinAge] BETWEEN 0 AND 100 AND ([MaxAge] IS NULL OR [MaxAge] >= [MinAge])");
            t.HasCheckConstraint("CK_RaceCategories_Bib", "[BibFrom] > 0 AND [BibFrom] < [BibTo]");
            t.HasCheckConstraint("CK_RaceCategories_RankedNeedsTimed", "[IsRanked] = 0 OR [IsTimed] = 1");
            t.HasCheckConstraint("CK_RaceCategories_CutOff", "[IsTimed] = 0 OR [CutOffMinutes] > 0");
            t.HasCheckConstraint("CK_RaceCategories_Itra", "[ItraPoints] IS NULL OR [ItraPoints] BETWEEN 0 AND 6");
            t.HasCheckConstraint("CK_RaceCategories_Elevation", "[ElevationGainM] IS NULL OR [ElevationGainM] >= 0");
        });
    }
}

public class PriceTierConfiguration : IEntityTypeConfiguration<PriceTier>
{
    public void Configure(EntityTypeBuilder<PriceTier> b)
    {
        b.HasKey(x => x.PriceTierId);
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.HasOne(x => x.Category).WithMany(c => c.PriceTiers).HasForeignKey(x => x.CategoryId);
        b.HasIndex(x => new { x.CategoryId, x.ValidFrom });
        b.ToTable(t =>
        {
            // BR-48: giá = 0 hoặc bội số 1.000đ (giới hạn min/max kiểm tra trong Service theo SystemSettings)
            t.HasCheckConstraint("CK_PriceTiers_Price", "[Price] >= 0 AND [Price] % 1000 = 0");
            t.HasCheckConstraint("CK_PriceTiers_Range", "[ValidFrom] < [ValidTo]");
        });
    }
}

public class AgeGroupConfiguration : IEntityTypeConfiguration<AgeGroup>
{
    public void Configure(EntityTypeBuilder<AgeGroup> b)
    {
        b.HasKey(x => x.AgeGroupId);
        b.Property(x => x.Name).HasMaxLength(50).IsRequired();
        b.HasOne(x => x.Event).WithMany(e => e.AgeGroups).HasForeignKey(x => x.EventId);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        b.ToTable(t => t.HasCheckConstraint("CK_AgeGroups_Range", "[MinAge] >= 0 AND ([MaxAge] IS NULL OR [MaxAge] >= [MinAge])"));
    }
}

public class CheckpointConfiguration : IEntityTypeConfiguration<Checkpoint>
{
    public void Configure(EntityTypeBuilder<Checkpoint> b)
    {
        b.HasKey(x => x.CheckpointId);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.DistanceKm).HasPrecision(6, 2);
        b.Property(x => x.Latitude).HasPrecision(9, 6);
        b.Property(x => x.Longitude).HasPrecision(9, 6);
        b.HasOne(x => x.Category).WithMany(c => c.Checkpoints).HasForeignKey(x => x.CategoryId);
        b.HasIndex(x => new { x.CategoryId, x.Sequence }).IsUnique();
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Checkpoints_Distance", "[DistanceKm] >= 0");
            t.HasCheckConstraint("CK_Checkpoints_Sequence", "[Sequence] >= 1");
            t.HasCheckConstraint("CK_Checkpoints_Coordinates",
                "([Latitude] IS NULL OR [Latitude] BETWEEN -90 AND 90) AND ([Longitude] IS NULL OR [Longitude] BETWEEN -180 AND 180)");
        });
    }
}

public class EventMediaConfiguration : IEntityTypeConfiguration<EventMedia>
{
    public void Configure(EntityTypeBuilder<EventMedia> b)
    {
        b.HasKey(x => x.MediaId);
        b.Property(x => x.FileUrl).HasMaxLength(500).IsRequired();
        b.Property(x => x.Caption).HasMaxLength(300);
        b.Property(x => x.UploadedById).HasMaxLength(450).IsRequired();
        b.HasOne(x => x.Event).WithMany(e => e.Media).HasForeignKey(x => x.EventId);
        b.HasIndex(x => new { x.EventId, x.Kind, x.DisplayOrder });
    }
}
