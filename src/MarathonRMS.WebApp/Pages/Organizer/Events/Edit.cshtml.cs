using MarathonRMS.DTOs.Admin;
using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.Events;

/// <summary>UC-18 – form tạo và sửa thông tin cơ bản của giải.</summary>
public class EditModel : BasePageModel
{
    private readonly EventApiClient _events;
    private readonly VenueApiClient _venues;

    public EditModel(EventApiClient events, VenueApiClient venues)
    {
        _events = events;
        _venues = venues;
    }

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty] public UpsertEventRequest Input { get; set; } = new();
    public List<VenueDto> Venues { get; private set; } = new();
    public bool IsEditing => Id is not null;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            Venues = await _venues.GetAllAsync(ct: ct);
            if (Id is int id)
            {
                var data = await _events.GetAsync(id, ct);
                Input = new UpsertEventRequest
                {
                    Name = data.Name,
                    VenueId = data.VenueId,
                    DescriptionHtml = data.DescriptionHtml,
                    BannerUrl = data.BannerUrl,
                    EventDate = data.EventDate,
                    EndDate = data.EndDate,
                    RegistrationOpenAt = data.RegistrationOpenAt,
                    RegistrationCloseAt = data.RegistrationCloseAt,
                    EmergencyPhone = data.EmergencyPhone,
                    InsuranceNote = data.InsuranceNote
                };
            }
            else
            {
                var eventDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(2));
                Input = new UpsertEventRequest
                {
                    EventDate = eventDate,
                    EndDate = eventDate,
                    RegistrationOpenAt = DateTime.Today,
                    RegistrationCloseAt = eventDate.ToDateTime(new TimeOnly(23, 59)).AddDays(-1)
                };
            }
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            _ = Id is int id
                ? await _events.UpdateAsync(id, Input, ct)
                : await _events.CreateAsync(Input, ct);
            StatusMessage = Id is null ? "Đã tạo giải mới." : "Đã cập nhật giải.";
            return RedirectToPage("Index");
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct); }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        try { Venues = await _venues.GetAllAsync(ct: ct); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }
}
