using MarathonRMS.BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarathonRMS.BusinessObjects.Data.Configurations;

public class TimingRecordConfiguration : IEntityTypeConfiguration<TimingRecord>
{
    public void Configure(EntityTypeBuilder<TimingRecord> b)
    {
        b.HasKey(x => x.TimingRecordId);
        b.Property(x => x.RecordedById).HasMaxLength(450).IsRequired();
        b.HasOne(x => x.Registration).WithMany(r => r.TimingRecords).HasForeignKey(x => x.RegistrationId);
        b.HasOne(x => x.Checkpoint).WithMany().HasForeignKey(x => x.CheckpointId);
        b.HasIndex(x => new { x.RegistrationId, x.CheckpointId }).IsUnique();
    }
}

public class ResultConfiguration : IEntityTypeConfiguration<Result>
{
    public void Configure(EntityTypeBuilder<Result> b)
    {
        b.HasKey(x => x.ResultId);
        b.HasOne(x => x.Registration).WithOne(r => r.Result).HasForeignKey<Result>(x => x.RegistrationId);
        b.HasOne(x => x.DnfCheckpoint).WithMany().HasForeignKey(x => x.DnfCheckpointId);
        b.HasOne(x => x.AgeGroup).WithMany().HasForeignKey(x => x.AgeGroupId);
        b.HasIndex(x => x.RegistrationId).IsUnique();
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Results_ChipLeGun", "[ChipTimeMs] IS NULL OR [GunTimeMs] IS NULL OR [ChipTimeMs] <= [GunTimeMs]");
            t.HasCheckConstraint("CK_Results_DnfReason", "[Status] <> 'DNF' OR [DnfReason] IS NOT NULL");
            t.HasCheckConstraint("CK_Results_Penalty", "[PenaltySeconds] >= 0");
            t.HasCheckConstraint("CK_Results_Ranks",
                "([OverallRank] IS NULL OR [OverallRank] >= 1) AND ([GenderRank] IS NULL OR [GenderRank] >= 1) AND ([AgeGroupRank] IS NULL OR [AgeGroupRank] >= 1)");
        });
    }
}

public class PenaltyReasonConfiguration : IEntityTypeConfiguration<PenaltyReason>
{
    public void Configure(EntityTypeBuilder<PenaltyReason> b)
    {
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(40).IsUnicode(false);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.ToTable(t => t.HasCheckConstraint("CK_PenaltyReasons_Minutes",
            "[DefaultAction] <> 'TimePenalty' OR [DefaultMinutes] > 0"));
        b.HasData(SeedData.PenaltyReasons);
    }
}

public class ResultPenaltyConfiguration : IEntityTypeConfiguration<ResultPenalty>
{
    public void Configure(EntityTypeBuilder<ResultPenalty> b)
    {
        b.HasKey(x => x.PenaltyId);
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.IssuedById).HasMaxLength(450).IsRequired();
        b.HasOne(x => x.Result).WithMany(r => r.Penalties).HasForeignKey(x => x.ResultId);
        b.HasOne(x => x.Reason).WithMany().HasForeignKey(x => x.ReasonCode);
        b.ToTable(t => t.HasCheckConstraint("CK_ResultPenalties_Minutes",
            "[Action] <> 'TimePenalty' OR [Minutes] > 0"));
    }
}
