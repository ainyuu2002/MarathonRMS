using MarathonRMS.DTOs.Admin;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Shared;

/// <summary>
/// UC-12 – quản lý địa điểm, dùng chung cho /Admin/Venues và /Organizer/Venues (khác nhau ở phân quyền thư mục).
/// Giao diện: partial _Venues.
/// </summary>
public abstract class VenuesPageModel : BasePageModel
{
    private readonly VenueApiClient _venues;
    protected VenuesPageModel(VenueApiClient venues) => _venues = venues;

    [BindProperty] public UpsertVenueRequest Input { get; set; } = new();
    [BindProperty] public int? EditingId { get; set; }
    [BindProperty(SupportsGet = true)] public string? City { get; set; }
    public List<VenueDto> Venues { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? edit, CancellationToken ct)
    {
        try
        {
            Venues = await _venues.GetAllAsync(City, ct);
            if (edit is int id && Venues.FirstOrDefault(v => v.Id == id) is { } v)
            {
                EditingId = id;
                Input = new UpsertVenueRequest { Name = v.Name, Address = v.Address, City = v.City, Latitude = v.Latitude, Longitude = v.Longitude };
            }
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return await ReloadAsync(ct);
        try
        {
            if (EditingId is int id) await _venues.UpdateAsync(id, Input, ct);
            else await _venues.CreateAsync(Input, ct);
            StatusMessage = "Đã lưu địa điểm.";
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct); }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        try
        {
            await _venues.DeleteVenueAsync(id, ct);
            StatusMessage = "Đã xóa địa điểm.";
            return RedirectToPage();
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        Venues = await _venues.GetAllAsync(City, ct);
        return Page();
    }
}
