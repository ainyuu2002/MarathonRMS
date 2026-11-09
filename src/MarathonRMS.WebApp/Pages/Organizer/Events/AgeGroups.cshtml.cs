using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.Events;

/// <summary>UC-23 – nhóm tuổi của giải (chung cho mọi cự ly hoặc riêng một cự ly).</summary>
public class AgeGroupsModel : BasePageModel
{
    private readonly EventApiClient _events;
    public AgeGroupsModel(EventApiClient events) => _events = events;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public int? AgeGroupId { get; set; }
    [BindProperty] public UpsertAgeGroupRequest Input { get; set; } = new();
    public EventDetailDto Event { get; private set; } = new();
    public List<AgeGroupDto> Groups { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
            if (AgeGroupId is int groupId)
            {
                var g = Groups.FirstOrDefault(x => x.Id == groupId);
                if (g is null) return NotFound();
                Input = new UpsertAgeGroupRequest { CategoryId = g.CategoryId, Name = g.Name, MinAge = g.MinAge, MaxAge = g.MaxAge };
            }
            else
            {
                var lastMax = Groups.Where(g => g.CategoryId == null).Select(g => g.MaxAge).DefaultIfEmpty(null).Max();
                var from = Groups.Count(g => g.CategoryId == null) == 0 ? 18 : (lastMax ?? 59) + 1;
                Input = new UpsertAgeGroupRequest { MinAge = from, MaxAge = from + 11, Name = $"{from}–{from + 11}" };
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
            _ = AgeGroupId is int groupId
                ? await _events.UpdateAgeGroupAsync(groupId, Input, ct)
                : await _events.CreateAgeGroupAsync(Id, Input, ct);
            StatusMessage = AgeGroupId is null ? "Đã thêm nhóm tuổi." : "Đã cập nhật nhóm tuổi.";
            return RedirectToPage(new { id = Id });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex, nameof(Input)) ?? await ReloadAsync(ct); }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int ageGroupId, CancellationToken ct)
    {
        try
        {
            await _events.DeleteAgeGroupAsync(ageGroupId, ct);
            StatusMessage = "Đã xóa nhóm tuổi.";
            return RedirectToPage(new { id = Id });
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? await ReloadAsync(ct); }
    }

    public string CategoryName(int? categoryId) =>
        categoryId is null ? "Mọi cự ly" : Event.Categories.FirstOrDefault(c => c.Id == categoryId)?.Name ?? "?";

    private async Task<IActionResult> ReloadAsync(CancellationToken ct)
    {
        try { await LoadAsync(ct); }
        catch (ApiException ex) { await HandleApiErrorAsync(ex); }
        return Page();
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Event = await _events.GetAsync(Id, ct);
        Groups = await _events.GetAgeGroupsAsync(Id, ct);
    }
}
