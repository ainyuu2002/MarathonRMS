using MarathonRMS.DTOs.Events;
using MarathonRMS.DTOs.RaceDay;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Staff;

/// <summary>UC-57 – nhập thời gian tại checkpoint theo số BIB.</summary>
public class TimingModel : BasePageModel
{
    private readonly RaceDayApiClient _raceDay;
    private readonly EventApiClient _events;

    public TimingModel(RaceDayApiClient raceDay, EventApiClient events)
    {
        _raceDay = raceDay;
        _events = events;
    }

    [BindProperty(SupportsGet = true)] public int? EventId { get; set; }
    [BindProperty(SupportsGet = true)] public int? CheckpointId { get; set; }
    [BindProperty(SupportsGet = true)] public int? LookupBib { get; set; }
    [BindProperty] public int? BibNumber { get; set; }
    /// <summary>Để trống = giờ hiện tại.</summary>
    [BindProperty] public DateTime? RecordedAt { get; set; }

    public List<EventSummaryDto> Events { get; private set; } = new();
    public List<RaceCategoryDto> Categories { get; private set; } = new();
    public List<TimingRecordDto> Recent { get; private set; } = new();
    public List<TimingRecordDto> BibRecords { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostRecordAsync(CancellationToken ct)
    {
        ModelState.Clear();
        if (EventId is not int eventId || CheckpointId is not int checkpointId || BibNumber is not int bib || bib <= 0)
        {
            ModelState.AddModelError(string.Empty, "Chọn checkpoint và nhập số BIB.");
            return await ReloadAsync(ct);
        }
        try
        {
            var saved = await _raceDay.RecordTimingAsync(new RecordTimingRequest
            {
                EventId = eventId,
                BibNumber = bib,
                CheckpointId = checkpointId,
                RecordedAt = RecordedAt ?? VietnamNow()
            }, ct);
            StatusMessage = $"BIB {saved.BibNumber} – {saved.FullName}: {saved.CheckpointName} lúc {saved.RecordedAt:HH:mm:ss.fff}.";
            return RedirectToPage(new { EventId, CheckpointId });
        }
        catch (ApiException ex)
        {
            var redirect = await HandleApiErrorAsync(ex);
            if (redirect is not null) return redirect;
            foreach (var message in ex.ValidationErrors.SelectMany(e => e.Value))
                ModelState.AddModelError(string.Empty, message);
            return await ReloadAsync(ct);
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int recordId, CancellationToken ct)
    {
        try
        {
            await _raceDay.DeleteTimingAsync(recordId, ct);
            StatusMessage = "Đã xóa bản ghi thời gian.";
            return RedirectToPage(new { EventId, CheckpointId, LookupBib });
        }
        catch (ApiException ex)
        {
            var redirect = await HandleApiErrorAsync(ex);
            return redirect ?? await ReloadAsync(ct);
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
        if (EventId is not int eventId) return;

        Categories = (await _events.GetCategoriesAsync(eventId, ct)).Where(c => c.IsTimed).ToList();
        if (CheckpointId is int cp && !Categories.SelectMany(c => c.Checkpoints).Any(p => p.Id == cp))
            CheckpointId = null;
        Recent = await _raceDay.GetRecentTimingAsync(eventId, CheckpointId, ct);
        if (LookupBib is int bib)
        {
            try { BibRecords = await _raceDay.GetTimingByBibAsync(eventId, bib, ct); }
            catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }
    }

    /// <summary>Giờ Việt Nam hiện tại (API lưu thời gian theo giờ VN).</summary>
    private static DateTime VietnamNow()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone))
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        return DateTime.UtcNow.AddHours(7);
    }
}
