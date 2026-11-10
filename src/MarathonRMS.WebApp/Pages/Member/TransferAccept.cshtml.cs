using MarathonRMS.DTOs.Events;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

/// <summary>Phần A – UC-47: người nhận khai thông tin của mình, đồng ý, trả phí (nếu có) để nhận suất.</summary>
public class TransferAcceptModel : BasePageModel
{
    private readonly RegistrationChangeApiClient _api;
    private readonly ProfileApiClient _profile;
    private readonly EventApiClient _events;

    public TransferAcceptModel(RegistrationChangeApiClient api, ProfileApiClient profile, EventApiClient events)
    {
        _api = api;
        _profile = profile;
        _events = events;
    }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public AcceptTransferRequest Input { get; set; } = new();
    public TransferDto Transfer { get; private set; } = new();
    public EventWaiverDto Waiver { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            if (!await LoadAsync(ct)) return NotFound();
            var p = await _profile.GetProfileAsync(ct);
            Input = new AcceptTransferRequest
            {
                FullName = p.FullName,
                BibName = (p.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "").ToUpperInvariant(),
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
            };
            if (Input.BibName.Length > 16) Input.BibName = Input.BibName[..16];
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
            await _api.AcceptAsync(Id, Input, ct);
            StatusMessage = $"Đã nhận suất {Transfer.EventName} – {Transfer.CategoryName}" +
                            (Transfer.FeeAmount > 0 ? $" (đã thanh toán phí {Transfer.FeeAmount.ToString("N0", VnFormat.Culture)}đ)." : ".");
            return RedirectToPage("/Member/Registrations");
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? Page(); }
    }

    private async Task<bool> LoadAsync(CancellationToken ct)
    {
        Transfer = await _api.GetTransferAsync(Id, ct);
        if (Transfer.Direction != "Incoming") return false;
        Waiver = await _events.GetWaiverAsync(Transfer.EventId, ct);
        return true;
    }
}
