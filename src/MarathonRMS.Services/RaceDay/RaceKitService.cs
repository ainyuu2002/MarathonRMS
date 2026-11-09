using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.RaceDay;

/// <summary>UC-52, UC-53 – tra cứu VĐV và phát race kit tại quầy (BR-20).</summary>
public class RaceKitService : IRaceKitService
{
    private const int MaxResults = 20;

    private readonly IRaceKitRepository _raceKits;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly IPrivacyFilter _privacy;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    public RaceKitService(IRaceKitRepository raceKits, IOrganizationScope scope, ICurrentUserService currentUser,
        IPrivacyFilter privacy, IDateTimeProvider clock, IUnitOfWork uow)
    {
        _raceKits = raceKits;
        _scope = scope;
        _currentUser = currentUser;
        _privacy = privacy;
        _clock = clock;
        _uow = uow;
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

    public async Task<RaceKitPickupDto> PickupAsync(int registrationId, RaceKitPickupRequest request, CancellationToken ct = default)
    {
        var r = await _raceKits.GetRegistrationForPickupAsync(registrationId, ct)
                ?? throw new NotFoundException("Không tìm thấy đăng ký.");
        var ev = r.Event;
        _scope.EnsureCanOperate(ev.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);
        _scope.EnsureNotOwnRegistration(r.Participant.LinkedUserId, r.RegisteredByUserId);

        if (ev.Status is not (EventStatus.RegistrationOpen or EventStatus.RegistrationClosed or EventStatus.Postponed))
            throw new BusinessRuleException("BR-20", "Giải không còn trong thời gian phát race kit.");
        if (r.Status != RegistrationStatus.Confirmed)
            throw new BusinessRuleException("BR-20",
                $"Chỉ phát kit cho đăng ký đã xác nhận (đăng ký đang {RaceDayRules.RegistrationStatusLabel(r.Status)}).");
        if (r.BibNumber is null)
            throw new BusinessRuleException("BR-20", "Đăng ký chưa được gán BIB.");
        if (r.RaceKitPickup is not null)
            throw new BusinessRuleException("BR-20", $"VĐV đã nhận kit lúc {r.RaceKitPickup.PickedUpAt:HH:mm dd/MM/yyyy}.");
        if (!request.IdChecked)
            throw new BusinessRuleException(ErrorCodes.Validation, "Phải đối chiếu giấy tờ trước khi phát kit.", 400);
        if (RaceDayRules.NeedsGuardianConsent(r.DateOfBirth, ev) && !r.GuardianConsent)
            throw new BusinessRuleException("BR-20",
                $"VĐV dưới {ev.Policy.GuardianConsentUnderAge} tuổi chưa có đồng ý của người giám hộ.");

        var proxyName = Clean(request.ProxyName);
        var proxyIdNumber = Clean(request.ProxyIdNumber);
        if (request.IsProxy && (proxyName is null || proxyIdNumber is null))
            throw new BusinessRuleException(ErrorCodes.Validation, "Nhận hộ phải ghi họ tên và số giấy tờ của người nhận.", 400);

        var pickup = new RaceKitPickup
        {
            RegistrationId = r.RegistrationId,
            PickedUpAt = _clock.Now,
            StaffId = _currentUser.UserId!,
            IdChecked = true,
            IsProxy = request.IsProxy,
            ProxyName = request.IsProxy ? proxyName : null,
            ProxyIdNumber = request.IsProxy ? proxyIdNumber : null,
            Note = Clean(request.Note)
        };
        await _raceKits.AddPickupAsync(pickup, ct);
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique RegistrationId: hai quầy cùng phát cho một VĐV
            throw new BusinessRuleException("BR-20", "VĐV vừa được phát kit ở quầy khác.");
        }

        return new RaceKitPickupDto
        {
            RegistrationId = r.RegistrationId,
            BibNumber = r.BibNumber,
            FullName = r.FullName,
            PickedUpAt = pickup.PickedUpAt,
            StaffName = await _raceKits.GetUserFullNameAsync(pickup.StaffId, ct) ?? "",
            IsProxy = pickup.IsProxy,
            ProxyName = pickup.ProxyName
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
