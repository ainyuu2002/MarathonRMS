using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.Events;

/// <summary>UC-48 – danh sách đăng ký của giải (OData) + sửa trường đã khóa có lý do (BR-08, BR-36).</summary>
public class RegistrationsModel : BasePageModel
{
    private readonly EventApiClient _events;
    private readonly RegistrationApiClient _registrations;

    public RegistrationsModel(EventApiClient events, RegistrationApiClient registrations)
    {
        _events = events;
        _registrations = registrations;
    }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    /// <summary>Họ tên, BIB, SĐT hoặc số giấy tờ.</summary>
    [BindProperty(SupportsGet = true)] public string? Keyword { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int? EditId { get; set; }
    [BindProperty] public UpdateLockedInfoRequest Input { get; set; } = new();

    public EventDetailDto Event { get; private set; } = new();
    public PagedResult<RegistrationListItemDto> Data { get; private set; } = new();
    public RegistrationListItemDto? Editing { get; private set; }

    public static readonly string[] Statuses = { "PendingPayment", "Confirmed", "Cancelled", "Expired", "Transferred", "RefundPending", "Refunded" };

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
            if (Editing is { } e)
                Input = new UpdateLockedInfoRequest
                {
                    FullName = e.FullName, DateOfBirth = e.DateOfBirth, Gender = e.Gender, IdType = e.IdType, IdNumber = e.IdNumber
                };
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostLockedInfoAsync(CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid || EditId is null) { await LoadAsync(ct); return Page(); }
            await _registrations.UpdateLockedInfoAsync(EditId.Value, Input, ct);
            StatusMessage = "Đã sửa thông tin đăng ký (đã ghi nhật ký).";
            return RedirectToPage(new { id = Id, categoryId = CategoryId, status = Status, keyword = Keyword, p = P });
        }
        catch (ApiException ex)
        {
            var redirect = await HandleApiErrorAsync(ex, nameof(Input));
            if (redirect is not null) return redirect;
            try { await LoadAsync(ct); } catch (ApiException) { }
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Event = await _events.GetAsync(Id, ct);
        var keyword = Keyword?.Trim();
        var filter = ODataQuery.And(
            $"EventId eq {Id}",
            CategoryId is int c ? $"CategoryId eq {c}" : null,
            string.IsNullOrEmpty(Status) ? null : $"Status eq {ODataQuery.Str(Status)}",
            string.IsNullOrEmpty(keyword) ? null
                : int.TryParse(keyword, out var bib) && keyword.Length <= 6
                    ? $"BibNumber eq {bib}"
                    : $"contains(FullName,{ODataQuery.Str(keyword)}) or Phone eq {ODataQuery.Str(keyword)} or IdNumber eq {ODataQuery.Str(keyword)}");
        Data = await _registrations.SearchAsync(filter, P, 20, ct);
        if (EditId is int editId)
            Editing = (await _registrations.SearchAsync($"EventId eq {Id} and Id eq {editId}", 1, 1, ct)).Items.FirstOrDefault();
    }
}
