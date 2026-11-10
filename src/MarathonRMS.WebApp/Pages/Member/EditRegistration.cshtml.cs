using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

/// <summary>UC-44 – chỉ sửa các trường BR-08 cho phép trước InfoEditDeadline.</summary>
public class EditRegistrationModel : BasePageModel
{
    private readonly RegistrationApiClient _registrations;
    public EditRegistrationModel(RegistrationApiClient registrations) => _registrations = registrations;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public UpdateRegistrationInfoRequest Input { get; set; } = new();
    public MyRegistrationDto Registration { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await LoadAsync(ct);
        if (Registration.Id == 0) return result;
        if (!Registration.CanEditInfo)
        {
            StatusMessage = "Đăng ký đã quá hạn sửa thông tin.";
            return RedirectToPage("/Member/Registrations");
        }
        Input = new UpdateRegistrationInfoRequest
        {
            BibName = Registration.BibName, TShirtSize = Registration.TShirtSize,
            Phone = Registration.Phone, EmergencyContactName = Registration.EmergencyContactName,
            EmergencyContactPhone = Registration.EmergencyContactPhone, ClubName = Registration.ClubName,
            HideFromPublicList = Registration.HideFromPublicList
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await LoadAsync(ct);
        try
        {
            await _registrations.UpdateInfoAsync(Id, Input, ct);
            StatusMessage = "Đã cập nhật thông tin đăng ký.";
            return RedirectToPage("/Member/Registrations");
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await LoadAsync(ct); }
    }

    private async Task<IActionResult> LoadAsync(CancellationToken ct)
    {
        try
        {
            Registration = (await _registrations.GetMineAsync(ct)).FirstOrDefault(r => r.Id == Id)
                ?? throw new ApiException(System.Net.HttpStatusCode.NotFound, "Không tìm thấy đăng ký.");
            return Page();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
    }
}
