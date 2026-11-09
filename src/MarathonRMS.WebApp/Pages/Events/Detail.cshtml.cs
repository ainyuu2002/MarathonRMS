using MarathonRMS.DTOs.EventContent;
using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Events;

/// <summary>UC-35 – chi tiết giải công khai: cự ly, giá hiện hành, suất còn lại, chính sách, trang giới thiệu + album (A).</summary>
public class DetailModel : BasePageModel
{
    private readonly EventApiClient _events;
    private readonly EventContentApiClient _content;

    public DetailModel(EventApiClient events, EventContentApiClient content)
    {
        _events = events;
        _content = content;
    }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    public EventDetailDto Event { get; private set; } = new();
    public EventContentDto? Content { get; private set; }

    public bool RegistrationOpenNow =>
        Event.Status == "RegistrationOpen" && DateTime.Now >= Event.RegistrationOpenAt && DateTime.Now < Event.RegistrationCloseAt;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Event = await _events.GetAsync(Id, ct); }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return NotFound(); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }

        // Trang giới thiệu + album của A: lỗi thì vẫn hiện phần thông tin chính
        try { Content = await _content.GetPublicAsync(Id, ct); }
        catch (ApiException) { Content = null; }
        return Page();
    }
}
