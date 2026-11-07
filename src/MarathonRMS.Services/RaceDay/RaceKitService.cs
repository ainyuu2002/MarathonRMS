using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.RaceDay;

/// <summary>UC-52 – tra cứu VĐV tại quầy race kit.</summary>
public class RaceKitService : IRaceKitService
{
    private const int MaxResults = 20;

    private readonly IRaceKitRepository _raceKits;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly IPrivacyFilter _privacy;

    public RaceKitService(IRaceKitRepository raceKits, IOrganizationScope scope, ICurrentUserService currentUser,
        IPrivacyFilter privacy)
    {
        _raceKits = raceKits;
        _scope = scope;
        _currentUser = currentUser;
        _privacy = privacy;
    }

    public async Task<IReadOnlyList<RaceKitLookupItemDto>> LookupAsync(int eventId, string keyword, CancellationToken ct = default)
    {
        var ev = await _raceKits.GetEventWithPolicyAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanOperate(ev.OrganizationId);

        var k = (keyword ?? "").Trim();
        if (k.Length == 0)
            throw new BusinessRuleException(ErrorCodes.Validation, "Nhập số BIB, mã QR, số giấy tờ hoặc số điện thoại.", 400);

        int? bibNumber = k.Length <= 6 && int.TryParse(k, out var n) && n > 0 ? n : null;
        var matches = await _raceKits.SearchAsync(eventId, bibNumber, k, RaceDayRules.Compact(k), MaxResults, ct);
        return matches.Select(r => ToLookupItem(r, ev)).ToList();
    }

    public Task<RaceKitPickupDto> PickupAsync(int registrationId, RaceKitPickupRequest request, CancellationToken ct = default) =>
        throw new NotImplementedException();

    private RaceKitLookupItemDto ToLookupItem(Registration r, Event ev)
    {
        var needsConsent = RaceDayRules.NeedsGuardianConsent(r.DateOfBirth, ev);
        var me = _currentUser.UserId;
        var warnings = new List<string>();
        if (r.Status != RegistrationStatus.Confirmed)
            warnings.Add($"Đăng ký đang ở trạng thái {RaceDayRules.RegistrationStatusLabel(r.Status)}.");
        if (r.BibNumber is null)
            warnings.Add("Chưa được gán BIB.");
        if (needsConsent && !r.GuardianConsent)
            warnings.Add($"VĐV dưới {ev.Policy.GuardianConsentUnderAge} tuổi chưa có đồng ý của người giám hộ.");
        if (me is not null && (me == r.Participant.LinkedUserId || me == r.RegisteredByUserId))
            warnings.Add("Đây là đăng ký của chính bạn hoặc do bạn đăng ký – nhờ nhân viên khác phát kit (BR-32).");

        return new RaceKitLookupItemDto
        {
            RegistrationId = r.RegistrationId,
            BibNumber = r.BibNumber,
            FullName = r.FullName,
            CategoryName = r.Category.Name,
            TShirtSize = r.TShirtSize.ToString(),
            Status = r.Status.ToString(),
            IdNumberMasked = _privacy.MaskIdNumber(r.IdNumber),
            IsMinor = needsConsent,
            HasGuardianConsent = r.GuardianConsent,
            PickedUp = r.RaceKitPickup is not null,
            PickedUpAt = r.RaceKitPickup?.PickedUpAt,
            Warnings = warnings
        };
    }
}
