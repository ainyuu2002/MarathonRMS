using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.Events;

/// <summary>UC-19 – cấu hình chính sách giải và quản lý phiên bản văn bản miễn trừ.</summary>
public class EventPolicyService : IEventPolicyService
{
    private readonly IEventRepository _events;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemSettingsProvider _settings;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public EventPolicyService(IEventRepository events, IOrganizationScope scope, ICurrentUserService currentUser,
        ISystemSettingsProvider settings, IAuditLogger audit, IUnitOfWork uow)
    {
        _events = events;
        _scope = scope;
        _currentUser = currentUser;
        _settings = settings;
        _audit = audit;
        _uow = uow;
    }

    public async Task<EventPolicyDto> GetAsync(int eventId, CancellationToken ct = default)
    {
        var entity = await LoadForReadAsync(eventId, ct);
        return ToDto(entity.Policy);
    }

    public async Task<EventPolicyDto> UpdateAsync(int eventId, UpdateEventPolicyRequest request, CancellationToken ct = default)
    {
        var entity = await LoadForWriteAsync(eventId, ct);
        var policy = entity.Policy;

        var ageMode = Parse<AgeCalculationMode>(request.AgeCalculationMode);
        var transferPolicy = Parse<TransferPolicy>(request.TransferPolicy);
        var overallBasis = ParseOptional<RankingBasis>(request.OverallRankingBasis);
        var ageGroupBasis = ParseOptional<RankingBasis>(request.AgeGroupRankingBasis);
        var tieBreakRule = Parse<TieBreakRule>(request.TieBreakRule);
        var awardStacking = Parse<AwardStackingPolicy>(request.AwardStackingPolicy);
        var forceMajeure = Parse<ForceMajeurePolicy>(request.ForceMajeurePolicy);

        await ValidateAsync(entity, request, transferPolicy, ct);
        await EnsureAgePolicyNotTightenedAsync(entity, ageMode, request.GuardianConsentUnderAge, ct);
        EnsureRankingFieldsUnlocked(entity, policy, overallBasis, ageGroupBasis, tieBreakRule, awardStacking);

        var old = ToDto(policy);
        policy.AgeCalculationMode = ageMode;
        policy.GuardianConsentUnderAge = request.GuardianConsentUnderAge;
        policy.MaxDependentsPerAccount = request.MaxDependentsPerAccount;
        policy.InfoEditDeadline = request.InfoEditDeadline;
        policy.TransferPolicy = transferPolicy;
        policy.TransferFee = request.TransferFee;
        policy.MaxTransfers = request.MaxTransfers;
        policy.TransferDeadline = request.TransferDeadline;
        policy.CategoryChangeFee = request.CategoryChangeFee;
        policy.MaxCategoryChanges = request.MaxCategoryChanges;
        policy.CategoryChangeDeadline = request.CategoryChangeDeadline;
        policy.LockSpecialEntries = request.LockSpecialEntries;
        policy.OverallRankingBasis = overallBasis;
        policy.AgeGroupRankingBasis = ageGroupBasis;
        policy.TieBreakRule = tieBreakRule;
        policy.LateStartToleranceMinutes = request.LateStartToleranceMinutes;
        policy.ComplaintWindowMinutes = request.ComplaintWindowMinutes;
        policy.ComplaintFee = request.ComplaintFee;
        policy.AwardStackingPolicy = awardStacking;
        policy.ForceMajeurePolicy = forceMajeure;

        var updated = ToDto(policy);
        await _audit.LogAsync("EventPolicy.Update", nameof(EventPolicy), eventId.ToString(), old, updated, ct);
        await _uow.SaveChangesAsync(ct);
        return updated;
    }

    public async Task<EventWaiverDto> GetWaiverAsync(int eventId, CancellationToken ct = default)
    {
        var entity = await LoadForReadAsync(eventId, ct);
        return ToWaiverDto(entity);
    }

    public async Task<EventWaiverDto> UpdateWaiverAsync(int eventId, UpdateEventWaiverRequest request, CancellationToken ct = default)
    {
        var entity = await LoadForWriteAsync(eventId, ct);
        var waiver = request.WaiverText.Trim();
        if (string.Equals(entity.WaiverText, waiver, StringComparison.Ordinal)) return ToWaiverDto(entity);

        var old = ToWaiverDto(entity);
        entity.WaiverText = waiver;
        if (!string.IsNullOrWhiteSpace(old.WaiverText)) entity.WaiverVersion++;

        var updated = ToWaiverDto(entity);
        await _audit.LogAsync("Event.WaiverUpdate", nameof(Event), eventId.ToString(), old, updated, ct);
        await _uow.SaveChangesAsync(ct);
        return updated;
    }

    private async Task<Event> LoadForReadAsync(int eventId, CancellationToken ct)
    {
        var entity = await _events.GetDetailedAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        if (entity.Status == EventStatus.Draft && !CanManage(entity.OrganizationId))
            throw new NotFoundException("Không tìm thấy giải.");
        return entity;
    }

    private async Task<Event> LoadForWriteAsync(int eventId, CancellationToken ct)
    {
        var entity = await _events.GetTrackedAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(entity.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(entity.OrganizationId, ct);
        return entity;
    }

    private bool CanManage(int organizationId) =>
        _currentUser.IsAdminOrAbove ||
        (_currentUser.IsInRole(AppRoles.Organizer) && _currentUser.OrganizationId == organizationId);

    private async Task ValidateAsync(Event entity, UpdateEventPolicyRequest request, TransferPolicy transferPolicy,
        CancellationToken ct)
    {
        var maxFee = await _settings.GetDecimalAsync("Fee.Max", 1_000_000m, ct);
        ValidateFee(request.TransferFee, maxFee, "Phí chuyển nhượng");
        ValidateFee(request.CategoryChangeFee, maxFee, "Phí đổi cự ly");
        ValidateFee(request.ComplaintFee, maxFee, "Phí khiếu nại");

        if (transferPolicy == TransferPolicy.Paid && request.TransferFee <= 0)
            throw new BusinessRuleException("BR-48", "Chuyển nhượng có phí phải có mức phí lớn hơn 0.", 400);
        if (transferPolicy != TransferPolicy.Paid && request.TransferFee != 0)
            throw new BusinessRuleException("BR-48", "Chỉ chính sách chuyển nhượng có phí mới được nhập phí.", 400);
        if (transferPolicy != TransferPolicy.Forbidden && request.MaxTransfers == 0)
            throw new BusinessRuleException("VALIDATION", "Phải cho phép ít nhất một lần chuyển nhượng.", 400);

        var latestDeadline = entity.EventDate.ToDateTime(TimeOnly.MaxValue);
        ValidateDeadline(request.InfoEditDeadline, latestDeadline, "Hạn sửa thông tin");
        ValidateDeadline(request.TransferDeadline, latestDeadline, "Hạn chuyển nhượng");
        ValidateDeadline(request.CategoryChangeDeadline, latestDeadline, "Hạn đổi cự ly");
    }

    private static void ValidateFee(decimal value, decimal maxFee, string field)
    {
        if (value % 1000 != 0 || value > maxFee)
            throw new BusinessRuleException("BR-48", $"{field} phải là bội số 1.000đ và không vượt {maxFee:N0}đ.", 400);
    }

    private static void ValidateDeadline(DateTime? value, DateTime latest, string field)
    {
        if (value > latest)
            throw new BusinessRuleException("VALIDATION", $"{field} không được sau ngày tổ chức.", 400);
    }

    private static void EnsureRankingFieldsUnlocked(Event entity, EventPolicy current, RankingBasis? overallBasis,
        RankingBasis? ageGroupBasis, TieBreakRule tieBreakRule, AwardStackingPolicy awardStacking)
    {
        if (entity.ResultsStatus == ResultsStatus.Draft) return;
        if (current.OverallRankingBasis != overallBasis || current.AgeGroupRankingBasis != ageGroupBasis ||
            current.TieBreakRule != tieBreakRule || current.AwardStackingPolicy != awardStacking)
            throw new BusinessRuleException("BR-46", "Không được sửa cơ sở xếp hạng, quy tắc hòa hoặc cộng dồn giải thưởng sau khi công bố kết quả tạm.");
    }

    private async Task EnsureAgePolicyNotTightenedAsync(Event entity, AgeCalculationMode ageMode,
        int guardianConsentUnderAge, CancellationToken ct)
    {
        var policy = entity.Policy;
        if (policy.AgeCalculationMode == ageMode && guardianConsentUnderAge <= policy.GuardianConsentUnderAge) return;
        if (!await _events.HasRegistrationsAsync(entity.EventId, ct)) return;
        if (policy.AgeCalculationMode != ageMode)
            throw new BusinessRuleException("BR-46", "Không được đổi cách tính tuổi khi giải đã có đăng ký.");
        if (guardianConsentUnderAge > policy.GuardianConsentUnderAge)
            throw new BusinessRuleException("BR-46", "Không được tăng ngưỡng giám hộ khi giải đã có đăng ký.");
    }

    private static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(value, ignoreCase: true);

    private static TEnum? ParseOptional<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null : Parse<TEnum>(value);

    private static EventWaiverDto ToWaiverDto(Event entity) => new()
    {
        EventId = entity.EventId,
        WaiverText = entity.WaiverText,
        WaiverVersion = entity.WaiverVersion
    };

    private static EventPolicyDto ToDto(EventPolicy p) => new()
    {
        AgeCalculationMode = p.AgeCalculationMode.ToString(),
        GuardianConsentUnderAge = p.GuardianConsentUnderAge,
        MaxDependentsPerAccount = p.MaxDependentsPerAccount,
        InfoEditDeadline = p.InfoEditDeadline,
        TransferPolicy = p.TransferPolicy.ToString(),
        TransferFee = p.TransferFee,
        MaxTransfers = p.MaxTransfers,
        TransferDeadline = p.TransferDeadline,
        CategoryChangeFee = p.CategoryChangeFee,
        MaxCategoryChanges = p.MaxCategoryChanges,
        CategoryChangeDeadline = p.CategoryChangeDeadline,
        LockSpecialEntries = p.LockSpecialEntries,
        OverallRankingBasis = p.OverallRankingBasis?.ToString(),
        AgeGroupRankingBasis = p.AgeGroupRankingBasis?.ToString(),
        TieBreakRule = p.TieBreakRule.ToString(),
        LateStartToleranceMinutes = p.LateStartToleranceMinutes,
        ComplaintWindowMinutes = p.ComplaintWindowMinutes,
        ComplaintFee = p.ComplaintFee,
        AwardStackingPolicy = p.AwardStackingPolicy.ToString(),
        ForceMajeurePolicy = p.ForceMajeurePolicy.ToString()
    };
}
