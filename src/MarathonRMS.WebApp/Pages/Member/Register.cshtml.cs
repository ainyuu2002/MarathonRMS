using MarathonRMS.DTOs.Accounts;
using MarathonRMS.DTOs.Coupons;
using MarathonRMS.DTOs.Events;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

/// <summary>UC-39 – đăng ký một cự ly cho bản thân: form điền sẵn từ hồ sơ, giám hộ / chạy kèm nếu cần, đồng ý, coupon.</summary>
public class RegisterModel : BasePageModel
{
    private readonly RegistrationApiClient _registrations;
    private readonly ProfileApiClient _profile;
    private readonly CouponApiClient _coupons;
    private readonly EventApiClient _events;

    public RegisterModel(RegistrationApiClient registrations, ProfileApiClient profile, CouponApiClient coupons, EventApiClient events)
    {
        _registrations = registrations;
        _events = events;
        _profile = profile;
        _coupons = coupons;
    }

    [BindProperty(SupportsGet = true)] public int EventId { get; set; }
    [BindProperty(SupportsGet = true)] public int CategoryId { get; set; }
    [BindProperty] public CreateRegistrationRequest Input { get; set; } = new();

    public EventDetailDto Event { get; private set; } = new();
    public RaceCategoryDto Category { get; private set; } = new();
    public EventPolicyDto Policy { get; private set; } = new();
    public EventWaiverDto Waiver { get; private set; } = new();
    public CouponQuoteDto? Quote { get; private set; }
    public string? CouponError { get; private set; }

    /// <summary>Tuổi theo cách tính của giải – để mở sẵn phần giám hộ / chạy kèm.</summary>
    public int? Age { get; private set; }
    public bool NeedsGuardian => Age is int a && a < Policy.GuardianConsentUnderAge;
    public bool NeedsEscort => Age is int a && Category.EscortRequiredUnderAge is int e && a < e;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            if (!await LoadAsync(ct)) return NotFound();
            var p = await _profile.GetProfileAsync(ct);
            Input = FromProfile(p);
            ComputeAge();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    /// <summary>Xem trước mã giảm giá (không trừ lượt).</summary>
    public async Task<IActionResult> OnPostCouponAsync(CancellationToken ct)
    {
        ModelState.Clear();
        try
        {
            if (!await LoadAsync(ct)) return NotFound();
            ComputeAge();
            if (string.IsNullOrWhiteSpace(Input.CouponCode)) { CouponError = "Nhập mã giảm giá."; return Page(); }
            Quote = await _coupons.ValidateAsync(new ValidateCouponRequest { Code = Input.CouponCode.Trim(), CategoryId = CategoryId }, ct);
        }
        catch (ApiException ex) when (ex.StatusCode != System.Net.HttpStatusCode.Unauthorized) { CouponError = ex.Message; }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        Input.CategoryId = CategoryId;
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            var created = await _registrations.CreateAsync(Input, ct);
            StatusMessage = created.Status == "Confirmed"
                ? $"Đăng ký thành công {created.EventName} – {created.CategoryName}."
                : $"Đã giữ chỗ {created.EventName} – {created.CategoryName}. Vui lòng thanh toán trước {created.ExpiresAt:HH:mm dd/MM}.";
            return RedirectToPage("/Member/Registrations");
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct); }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        try { if (!await LoadAsync(ct)) return NotFound(); ComputeAge(); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }

    private async Task<bool> LoadAsync(CancellationToken ct)
    {
        Event = await _events.GetAsync(EventId, ct);
        var category = Event.Categories.FirstOrDefault(c => c.Id == CategoryId);
        if (category is null) return false;
        Category = category;
        Policy = Event.Policy;
        Waiver = await _registrations.GetWaiverAsync(EventId, ct);
        return true;
    }

    private void ComputeAge()
    {
        if (Input.DateOfBirth == default) return;
        var raceDay = DateOnly.FromDateTime(Category.StartAt);
        var dob = Input.DateOfBirth;
        Age = Policy.AgeCalculationMode == "CalendarYear"
            ? raceDay.Year - dob.Year
            : raceDay.Year - dob.Year - (dob > raceDay.AddYears(-(raceDay.Year - dob.Year)) ? 1 : 0);
    }

    private CreateRegistrationRequest FromProfile(ParticipantProfileDto p) => new()
    {
        CategoryId = CategoryId,
        ParticipantId = p.ParticipantId,
        FullName = p.FullName,
        BibName = BibNameFrom(p.FullName),
        DateOfBirth = p.DateOfBirth ?? default,
        Gender = p.Gender ?? "",
        Nationality = p.Nationality ?? "Việt Nam",
        IdType = p.IdType,
        IdNumber = p.IdNumber,
        Phone = p.Phone ?? "",
        Email = p.Email,
        TShirtSize = p.TShirtSize ?? "",
        EmergencyContactName = p.EmergencyContactName ?? "",
        EmergencyContactPhone = p.EmergencyContactPhone ?? "",
        BloodType = p.BloodType,
        MedicalNotes = p.MedicalNotes,
        ClubName = p.ClubName
        // Các ô đồng ý để trống – không tick sẵn (BR-09)
    };

    /// <summary>Tên in BIB mặc định: tên gọi (chữ cuối), tối đa 16 ký tự.</summary>
    private static string BibNameFrom(string fullName)
    {
        var last = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
        return last.Length > 16 ? last[..16] : last.ToUpperInvariant();
    }
}
