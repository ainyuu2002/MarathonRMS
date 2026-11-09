using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Events;

/// <summary>Phần A – UC-38 (GĐ3): danh sách VĐV công khai của giải – tên (hoặc "VĐV #BIB" nếu ẩn), BIB, cự ly, CLB (BR-37).</summary>
public class RunnersModel : BasePageModel
{
    private readonly EventApiClient _events;
    private readonly PublicRunnerApiClient _runners;

    public RunnersModel(EventApiClient events, PublicRunnerApiClient runners)
    {
        _events = events;
        _runners = runners;
    }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Gender { get; set; }
    /// <summary>Tên hoặc số BIB.</summary>
    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;

    public EventDetailDto Event { get; private set; } = new();
    public PagedResult<PublicRunnerDto> Data { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { Event = await _events.GetAsync(Id, ct); }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return NotFound(); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }

        var keyword = Keyword?.Trim();
        var filter = ODataQuery.And(
            $"EventId eq {Id}",
            CategoryId is int c ? $"CategoryId eq {c}" : null,
            Gender is "M" or "F" ? $"Gender eq {ODataQuery.Str(Gender)}" : null,
            string.IsNullOrEmpty(keyword) ? null
                : int.TryParse(keyword, out var bib) ? $"BibNumber eq {bib}" : $"contains(DisplayName,{ODataQuery.Str(keyword)})")!;
        try { Data = await _runners.SearchAsync(filter, P, ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }
}
