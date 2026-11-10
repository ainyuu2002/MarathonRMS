using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer;

/// <summary>UC-48 – Organizer sửa trường khóa, bắt buộc lý do và ghi audit.</summary>
public class EditRegistrationModel : BasePageModel
{
    private readonly RegistrationApiClient _registrations;
    public EditRegistrationModel(RegistrationApiClient registrations) => _registrations = registrations;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public UpdateLockedInfoRequest Input { get; set; } = new();
    public RegistrationListItemDto Registration { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var result = await LoadAsync(ct);
        if (Registration.Id == 0) return result;
        Input = new UpdateLockedInfoRequest
        {
            FullName = Registration.FullName, DateOfBirth = Registration.DateOfBirth,
            Gender = Registration.Gender, IdType = Registration.IdType, IdNumber = Registration.IdNumber
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await LoadAsync(ct);
        try
        {
            await _registrations.UpdateLockedInfoAsync(Id, Input, ct);
            StatusMessage = "Đã cập nhật trường khóa và ghi nhật ký lý do.";
            return RedirectToPage("/Organizer/Registrations");
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await LoadAsync(ct); }
    }

    private async Task<IActionResult> LoadAsync(CancellationToken ct)
    {
        try
        {
            Registration = (await _registrations.SearchForOrganizerAsync($"Id eq {Id}", 1, 1, ct)).Items.FirstOrDefault()
                ?? throw new ApiException(System.Net.HttpStatusCode.NotFound, "Không tìm thấy đăng ký.");
            return Page();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
    }
}
