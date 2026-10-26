using MarathonRMS.BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarathonRMS.BusinessObjects.Data.Configurations;

public class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> b)
    {
        b.HasKey(x => x.CouponId);
        b.Property(x => x.Code).HasMaxLength(30).IsUnicode(false).IsRequired();
        b.Property(x => x.PartnerName).HasMaxLength(200);
        b.Property(x => x.CreatedById).HasMaxLength(450);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId);
        b.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        b.HasOne(x => x.OwnerMember).WithMany().HasForeignKey(x => x.OwnerMemberId);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.IsActive });
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Coupons_Uses", "[MaxUses] >= 1 AND [UsedCount] >= 0 AND [UsedCount] <= [MaxUses]");
            t.HasCheckConstraint("CK_Coupons_Value",
                "([DiscountType] = 'Percent' AND [DiscountValue] BETWEEN 1 AND 100) OR ([DiscountType] = 'Amount' AND [DiscountValue] > 0 AND [DiscountValue] % 1000 = 0)");
            t.HasCheckConstraint("CK_Coupons_MaxDiscount", "[MaxDiscountAmount] IS NULL OR ([MaxDiscountAmount] > 0 AND [MaxDiscountAmount] % 1000 = 0)");
            t.HasCheckConstraint("CK_Coupons_Range", "[ValidFrom] < [ValidTo]");
            t.HasCheckConstraint("CK_Coupons_CategoryNeedsEvent", "[CategoryId] IS NULL OR [EventId] IS NOT NULL");
        });
    }
}

public class BibTransferConfiguration : IEntityTypeConfiguration<BibTransfer>
{
    public void Configure(EntityTypeBuilder<BibTransfer> b)
    {
        b.HasKey(x => x.TransferId);
        b.Property(x => x.FromUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.ToUserId).HasMaxLength(450).IsRequired();
        b.HasOne(x => x.Registration).WithMany().HasForeignKey(x => x.RegistrationId);
        b.HasIndex(x => x.RegistrationId).IsUnique()
            .HasFilter("[Status] IN ('Pending','AwaitingFee')")
            .HasDatabaseName("UX_BibTransfers_Registration_Open");
        b.HasIndex(x => new { x.ToUserId, x.Status });
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_BibTransfers_Users", "[FromUserId] <> [ToUserId]");
            t.HasCheckConstraint("CK_BibTransfers_Fee", "[FeeAmount] >= 0");
        });
    }
}

public class ResultComplaintConfiguration : IEntityTypeConfiguration<ResultComplaint>
{
    public void Configure(EntityTypeBuilder<ResultComplaint> b)
    {
        b.HasKey(x => x.ComplaintId);
        b.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Content).HasMaxLength(2000).IsRequired();
        b.Property(x => x.EvidenceUrl).HasMaxLength(500);
        b.Property(x => x.Response).HasMaxLength(2000);
        b.Property(x => x.HandledById).HasMaxLength(450);
        b.HasOne(x => x.Result).WithMany().HasForeignKey(x => x.ResultId);
        b.HasIndex(x => new { x.ResultId, x.Status });
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_ResultComplaints_Response", "[Status] = 'Open' OR [Response] IS NOT NULL");
            t.HasCheckConstraint("CK_ResultComplaints_Fee", "[FeeAmount] >= 0");
        });
    }
}

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> b)
    {
        b.HasKey(x => x.RefundId);
        b.Property(x => x.BankInfo).HasMaxLength(300);
        b.Property(x => x.ProcessedById).HasMaxLength(450);
        b.Property(x => x.TransactionRef).HasMaxLength(100);
        b.HasOne(x => x.Registration).WithMany().HasForeignKey(x => x.RegistrationId);
        b.HasIndex(x => x.RegistrationId).IsUnique();
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_Refunds_Completed", "[Status] <> 'Completed' OR [TransactionRef] IS NOT NULL");
        });
    }
}

public class PrizeConfiguration : IEntityTypeConfiguration<Prize>
{
    public void Configure(EntityTypeBuilder<Prize> b)
    {
        b.HasKey(x => x.PrizeId);
        b.Property(x => x.Nationality).HasMaxLength(60);
        b.Property(x => x.Reward).HasMaxLength(300).IsRequired();
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        b.HasOne(x => x.AgeGroup).WithMany().HasForeignKey(x => x.AgeGroupId);
        b.HasIndex(x => new { x.CategoryId, x.AwardType, x.Gender, x.AgeGroupId, x.Rank }).IsUnique()
            .HasFilter(null); // AgeGroupId null cũng phải duy nhất
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Prizes_Rank", "[Rank] >= 1");
            t.HasCheckConstraint("CK_Prizes_AgeGroup", "([AwardType] = 'AgeGroup' AND [AgeGroupId] IS NOT NULL) OR ([AwardType] <> 'AgeGroup' AND [AgeGroupId] IS NULL)");
            t.HasCheckConstraint("CK_Prizes_Cash", "[CashAmount] IS NULL OR [CashAmount] >= 0");
        });
    }
}

public class RaceRouteConfiguration : IEntityTypeConfiguration<RaceRoute>
{
    public void Configure(EntityTypeBuilder<RaceRoute> b)
    {
        b.HasKey(x => x.RouteId);
        b.Property(x => x.RouteGeoJson).IsRequired();
        b.Property(x => x.CalculatedDistanceKm).HasPrecision(7, 3);
        b.Property(x => x.UpdatedById).HasMaxLength(450);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        b.HasIndex(x => x.CategoryId).IsUnique();
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_RaceRoutes_Distance", "[CalculatedDistanceKm] > 0");
            t.HasCheckConstraint("CK_RaceRoutes_Json", "ISJSON([RouteGeoJson]) = 1");
        });
    }
}

public class RoutePoiConfiguration : IEntityTypeConfiguration<RoutePoi>
{
    public void Configure(EntityTypeBuilder<RoutePoi> b)
    {
        b.HasKey(x => x.PoiId);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Latitude).HasPrecision(9, 6);
        b.Property(x => x.Longitude).HasPrecision(9, 6);
        b.Property(x => x.DistanceFromStartKm).HasPrecision(7, 3);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId);
        b.ToTable(t => t.HasCheckConstraint("CK_RoutePois_Coordinates",
            "[Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180"));
    }
}

public class EventContentSectionConfiguration : IEntityTypeConfiguration<EventContentSection>
{
    public void Configure(EntityTypeBuilder<EventContentSection> b)
    {
        b.HasKey(x => x.SectionId);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.HasOne(x => x.Event).WithMany(e => e.ContentSections).HasForeignKey(x => x.EventId);
        // Không UNIQUE (EventId, DisplayOrder): đổi thứ tự cập nhật nhiều dòng – trùng tạm thời sẽ vi phạm
        b.HasIndex(x => new { x.EventId, x.DisplayOrder });
        b.ToTable(t => t.HasCheckConstraint("CK_EventContentSections_Order", "[DisplayOrder] >= 0"));
    }
}

public class EventScheduleItemConfiguration : IEntityTypeConfiguration<EventScheduleItem>
{
    public void Configure(EntityTypeBuilder<EventScheduleItem> b)
    {
        b.HasKey(x => x.ItemId);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Location).HasMaxLength(200);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasOne(x => x.Event).WithMany(e => e.ScheduleItems).HasForeignKey(x => x.EventId);
        b.HasIndex(x => new { x.EventId, x.StartsAt });
        b.ToTable(t => t.HasCheckConstraint("CK_EventScheduleItems_Range", "[EndsAt] IS NULL OR [EndsAt] > [StartsAt]"));
    }
}
