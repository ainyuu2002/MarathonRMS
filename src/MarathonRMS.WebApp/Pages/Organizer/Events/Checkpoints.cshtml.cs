using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.Events;

/// <summary>UC-24 – quản lý checkpoint của một cự ly.</summary>
public class CheckpointsModel : BasePageModel
{
    private readonly EventApiClient _events;
    public CheckpointsModel(EventApiClient events) => _events = events;

    [BindProperty(SupportsGet = true)] public int EventId { get; set; }
    [BindProperty(SupportsGet = true)] public int CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public int? CheckpointId { get; set; }
    [BindProperty] public UpsertCheckpointRequest Input { get; set; } = new();
    public EventDetailDto Event { get; private set; } = new();
    public RaceCategoryDto Category { get; private set; } = new();
    public List<CheckpointDto> Checkpoints { get; private set; } = new();
    public bool IsEditing => CheckpointId.HasValue;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
            if (CheckpointId is int id)
            {
                var point = Checkpoints.FirstOrDefault(p => p.Id == id);
                if (point is null) return NotFound();
                Input = new UpsertCheckpointRequest
                {
                    Name = point.Name, DistanceKm = point.DistanceKm, Sequence = point.Sequence,
                    Type = point.Type, IsMandatory = point.IsMandatory, CutOffAt = point.CutOffAt,
                    StationType = point.StationType, Latitude = point.Latitude, Longitude = point.Longitude
                };
            }
            else
            {
                var first = Checkpoints.Count == 0;
                Input = new UpsertCheckpointRequest
                {
                    Name = first ? "Start" : "Checkpoint mới", DistanceKm = first ? 0 : Checkpoints.Max(p => p.DistanceKm),
                    Sequence = first ? 1 : Checkpoints.Max(p => p.Sequence) + 1,
                    Type = first ? "Start" : "Split", IsMandatory = true, StationType = "None"
                };
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
            _ = CheckpointId is int id
                ? await _events.UpdateCheckpointAsync(id, Input, ct)
                : await _events.CreateCheckpointAsync(CategoryId, Input, ct);
            StatusMessage = CheckpointId is null ? "Đã thêm checkpoint." : "Đã cập nhật checkpoint.";
            return RedirectToPage(new { eventId = EventId, categoryId = CategoryId });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct); }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken ct)
    {
        try
        {
            await _events.DeleteCheckpointAsync(id, ct);
            StatusMessage = "Đã xóa checkpoint.";
            return RedirectToPage(new { eventId = EventId, categoryId = CategoryId });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var ev = await _events.GetAsync(EventId, ct);
        EnsureOwnOrganization(ev.OrganizationId);
        Event = ev;
        Category = Event.Categories.FirstOrDefault(c => c.Id == CategoryId) ?? throw new ApiException(
            System.Net.HttpStatusCode.NotFound, "Không tìm thấy cự ly.");
        Checkpoints = await _events.GetCheckpointsAsync(CategoryId, ct);
    }
}
