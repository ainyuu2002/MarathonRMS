using MarathonRMS.DTOs.Accounts;
using MarathonRMS.DTOs.Events;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

public class RegisterModel : BasePageModel
{
    private readonly ProfileApiClient _profiles;
    private readonly EventApiClient _events;
    private readonly RegistrationApiClient _registrations;

    public RegisterModel(ProfileApiClient profiles, EventApiClient events, RegistrationApiClient registrations)
    {
        _profiles = profiles; _events = events; _registrations = registrations;
    }

    [BindProperty(SupportsGet = true)] public int CategoryId { get; set; }
    [BindProperty] public CreateRegistrationRequest Input { get; set; } = new();
    public ParticipantProfileDto Profile { get; private set; } = new();
    public RaceCategoryDto Category { get; private set; } = new();
    public EventDetailDto Event { get; private set; } = new();
    public EventWaiverDto Waiver { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
            Input = new CreateRegistrationRequest
            {
                CategoryId = CategoryId, ParticipantId = Profile.ParticipantId, FullName = Profile.FullName,
                BibName = Profile.FullName.Length <= 16 ? Profile.FullName : Profile.FullName[..16],
                DateOfBirth = Profile.DateOfBirth ?? default, Gender = Profile.Gender ?? "",
                Nationality = Profile.Nationality ?? "Việt Nam", IdType = Profile.IdType, IdNumber = Profile.IdNumber,
                Phone = Profile.Phone ?? "", Email = Profile.Email, TShirtSize = Profile.TShirtSize ?? "",
                EmergencyContactName = Profile.EmergencyContactName ?? "",
                EmergencyContactPhone = Profile.EmergencyContactPhone ?? "", BloodType = Profile.BloodType,
                MedicalNotes = Profile.MedicalNotes, ClubName = Profile.ClubName
            };
        }
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
                ? "Đăng ký miễn phí đã được xác nhận."
                : "Đã giữ suất. Hãy hoàn tất thanh toán trước thời hạn.";
            return RedirectToPage("/Member/Checkout", new { registrationId = created.Id });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct); }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Profile = await _profiles.GetProfileAsync(ct);
        Category = await _events.GetCategoryAsync(CategoryId, ct);
        Event = await _events.GetAsync(Category.EventId, ct);
        Waiver = await _events.GetWaiverAsync(Category.EventId, ct);
    }
}
