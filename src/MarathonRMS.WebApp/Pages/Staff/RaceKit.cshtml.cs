using MarathonRMS.DTOs.Events;
using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Staff;

/// <summary>
/// UC-52, UC-53 – tra cứu VĐV và phát race kit tại quầy.
/// Từ khóa (có thể là số giấy tờ / SĐT) chỉ gửi bằng POST và giữ qua TempData – không nằm trên URL hay log truy cập.
/// </summary>
public class RaceKitModel : BasePageModel
{
    private readonly RaceDayApiClient _raceDay;
    public RaceKitModel(RaceDayApiClient raceDay) => _raceDay = raceDay;

    [BindProperty(SupportsGet = true)] public int? EventId { get; set; }
    [BindProperty] public string? Keyword { get; set; }

    /// <summary>Từ khóa của lần tra cứu trước – để hiện lại danh sách sau khi phát kit (redirect).</summary>
    [TempData] public string? LastKeyword { get; set; }
    [BindProperty] public RaceKitPickupRequest Pickup { get; set; } = new();

    public List<EventSummaryDto> Events { get; private set; } = new();
    public List<RaceKitLookupItemDto> Results { get; private set; } = new();
    public bool Searched { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        Keyword = LastKeyword;
        try { await LoadAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostSearchAsync(CancellationToken ct)
    {
        ModelState.Clear();
        return await ReloadAsync(ct);
    }

    public async Task<IActionResult> OnPostPickupAsync(int registrationId, CancellationToken ct)
    {
        ModelState.Clear();
        if (!Pickup.IdChecked)
        {
            ModelState.AddModelError(string.Empty, "Phải đối chiếu giấy tờ của VĐV (hoặc người nhận hộ) trước khi phát kit.");
            return await ReloadAsync(ct);
        }
        try
        {
            var done = await _raceDay.PickupRaceKitAsync(registrationId, Pickup, ct);
            StatusMessage = $"Đã phát kit cho {(done.BibNumber is int bib ? $"BIB {bib} – " : "")}{done.FullName}" +
                            (done.IsProxy ? $" (nhận hộ: {done.ProxyName})." : ".");
            LastKeyword = Keyword;
            return RedirectToPage(new { EventId });
        }
        catch (ApiException ex)
        {
            var redirect = await HandleApiErrorAsync(ex);
            if (redirect is not null) return redirect;
            // Lỗi theo trường (VD độ dài) hiển thị chung ở khung lỗi đầu trang
            foreach (var message in ex.ValidationErrors.SelectMany(e => e.Value))
                ModelState.AddModelError(string.Empty, message);
            return await ReloadAsync(ct);
        }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Events = await _raceDay.GetOperationalEventsAsync(ct);
        EventId ??= Events.FirstOrDefault()?.Id;
        if (EventId is int eventId && !string.IsNullOrWhiteSpace(Keyword))
        {
            Results = await _raceDay.LookupRaceKitAsync(eventId, Keyword.Trim(), ct);
            Searched = true;
        }
    }
}
