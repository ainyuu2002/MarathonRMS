using MarathonRMS.DTOs.Accounts;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

/// <summary>UC-03 (hồ sơ chính chủ) + UC-04 (đồng ý tùy chọn).</summary>
public class ProfileModel : BasePageModel
{
    private readonly ProfileApiClient _profiles;
    public ProfileModel(ProfileApiClient profiles) => _profiles = profiles;

    [BindProperty] public UpdateProfileRequest Input { get; set; } = new();
    public ParticipantProfileDto? Profile { get; private set; }
    public List<ConsentStatusDto> Consents { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
            Input = new UpdateProfileRequest
            {
                FullName = Profile!.FullName, DateOfBirth = Profile.DateOfBirth, Gender = Profile.Gender,
                Nationality = Profile.Nationality ?? "Việt Nam", IdType = Profile.IdType, IdNumber = Profile.IdNumber,
                Phone = Profile.Phone, TShirtSize = Profile.TShirtSize, EmergencyContactName = Profile.EmergencyContactName,
                EmergencyContactPhone = Profile.EmergencyContactPhone, BloodType = Profile.BloodType,
                MedicalNotes = Profile.MedicalNotes, ClubName = Profile.ClubName
            };
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            await _profiles.UpdateProfileAsync(Input, ct);
            StatusMessage = "Đã lưu hồ sơ.";
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct); }
    }

    public async Task<IActionResult> OnPostConsentAsync(string consentType, bool granted, CancellationToken ct)
    {
        try
        {
            await _profiles.SetConsentAsync(new SetConsentRequest { ConsentType = consentType, Granted = granted }, ct);
            StatusMessage = granted ? "Đã ghi nhận đồng ý." : "Đã rút lại đồng ý.";
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Profile = await _profiles.GetProfileAsync(ct);
        Consents = await _profiles.GetConsentsAsync(ct);
    }
}
