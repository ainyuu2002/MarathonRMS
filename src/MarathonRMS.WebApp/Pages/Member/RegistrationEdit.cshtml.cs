using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

/// <summary>UC-44 – tự sửa tên in BIB, size áo, SĐT, liên hệ khẩn cấp, CLB trước hạn khóa (BR-08).</summary>
public class RegistrationEditModel : BasePageModel
{
    private readonly RegistrationApiClient _registrations;
    public RegistrationEditModel(RegistrationApiClient registrations) => _registrations = registrations;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public UpdateRegistrationInfoRequest Input { get; set; } = new();
    public MyRegistrationDto Registration { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            if (!await LoadAsync(ct)) return NotFound();
            var r = Registration;
            Input = new UpdateRegistrationInfoRequest
            {
                BibName = r.BibName, TShirtSize = r.TShirtSize, Phone = r.Phone, EmergencyContactName = r.EmergencyContactName,
                EmergencyContactPhone = r.EmergencyContactPhone, ClubName = r.ClubName, HideFromPublicList = r.HideFromPublicList
            };
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        try
        {
            if (!await LoadAsync(ct)) return NotFound();
            if (!ModelState.IsValid) return Page();
            await _registrations.UpdateInfoAsync(Id, Input, ct);
            StatusMessage = "Đã cập nhật thông tin đăng ký.";
            return RedirectToPage("/Member/Registrations");
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? Page(); }
    }

    private async Task<bool> LoadAsync(CancellationToken ct)
    {
        var r = (await _registrations.GetMineAsync(ct)).FirstOrDefault(x => x.Id == Id);
        if (r is null) return false;
        Registration = r;
        return true;
    }
}
