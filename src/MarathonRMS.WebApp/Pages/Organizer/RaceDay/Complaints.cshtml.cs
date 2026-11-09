using MarathonRMS.DTOs.Results;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.RaceDay;

/// <summary>UC-66 – Organizer xem và trả lời khiếu nại kết quả của giải (BR-27, BR-32).</summary>
public class ComplaintsModel : BasePageModel
{
    private readonly ResultApiClient _results;
    public ComplaintsModel(ResultApiClient results) => _results = results;

    [BindProperty(SupportsGet = true)] public int EventId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty] public ResolveComplaintRequest Input { get; set; } = new();

    public EventComplaintsDto Data { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostResolveAsync(int complaintId, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ModelState.AddModelError(string.Empty, "Phải ghi câu trả lời cho người khiếu nại.");
            return await ReloadAsync(ct);
        }
        try
        {
            var done = await _results.ResolveComplaintAsync(complaintId, Input, ct);
            StatusMessage = done.Status == "Accepted"
                ? $"Đã chấp nhận khiếu nại của {done.RunnerName}. Nhớ sửa dữ liệu (thời gian, phạt) rồi tính lại kết quả nếu cần."
                : $"Đã từ chối khiếu nại của {done.RunnerName}.";
            return RedirectToPage(new { eventId = EventId, status = Status });
        }
        catch (ApiException ex)
        {
            return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct);
        }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct) =>
        Data = await _results.GetEventComplaintsAsync(EventId, Status, ct);
}
