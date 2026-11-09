using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Organizer.Events;

/// <summary>Phần A – UC-25 hoãn / hủy giải (BR-15) và UC-50 xử lý hoàn tiền.</summary>
public class LifecycleModel : BasePageModel
{
    private readonly EventApiClient _events;
    private readonly EventLifecycleApiClient _lifecycle;

    public LifecycleModel(EventApiClient events, EventLifecycleApiClient lifecycle)
    {
        _events = events;
        _lifecycle = lifecycle;
    }

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    public PostponeEventRequest PostponeInput { get; set; } = new();
    public CancelEventRequest CancelInput { get; set; } = new();
    public EventDetailDto Event { get; private set; } = new();
    public List<RefundDto> Refunds { get; private set; } = new();

    public bool CanChange => Event.Status is not ("Completed" or "Cancelled" or "Draft") && Event.ResultsStatus == "Draft";

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try
        {
            await LoadAsync(ct);
            PostponeInput.NewEventDate = Event.EventDate.AddDays(7);
            CancelInput.RefundPaidRegistrations = Event.Policy.ForceMajeurePolicy == "Refund";
        }
        catch (ApiException ex) { return await HandleApiErrorAsync(ex) ?? Page(); }
        return Page();
    }

    public async Task<IActionResult> OnPostPostponeAsync([Bind(Prefix = nameof(PostponeInput))] PostponeEventRequest input, CancellationToken ct)
    {
        PostponeInput = input;
        return await RunAsync(async () =>
        {
            var r = await _lifecycle.PostponeAsync(Id, input, ct);
            StatusMessage = $"Đã dời giải sang {r.EventDate:dd/MM/yyyy} (thêm {r.ShiftedDays} ngày). Giờ xuất phát, cut-off và các hạn đã dời theo.";
        }, nameof(PostponeInput), ct);
    }

    public async Task<IActionResult> OnPostCancelAsync([Bind(Prefix = nameof(CancelInput))] CancelEventRequest input, CancellationToken ct)
    {
        CancelInput = input;
        return await RunAsync(async () =>
        {
            var r = await _lifecycle.CancelAsync(Id, input, ct);
            StatusMessage = $"Đã hủy giải. Hủy {r.CancelledUnpaid} đơn chưa thanh toán; tạo {r.RefundsCreated} yêu cầu hoàn tiền ({r.RefundTotal:N0}đ)" +
                            (r.CancelledWithoutRefund > 0 ? $"; {r.CancelledWithoutRefund} đăng ký hủy không hoàn tiền." : ".");
        }, nameof(CancelInput), ct);
    }

    public async Task<IActionResult> OnPostCompleteAsync(int refundId, string? transactionRef, CancellationToken ct)
    {
        ModelState.Clear();
        return await RunAsync(async () =>
        {
            await _lifecycle.CompleteRefundAsync(refundId, new CompleteRefundRequest { TransactionRef = transactionRef ?? "" }, ct);
            StatusMessage = "Đã ghi nhận hoàn tiền.";
        }, null, ct);
    }

    public async Task<IActionResult> OnPostRejectAsync(int refundId, string? reason, CancellationToken ct)
    {
        ModelState.Clear();
        return await RunAsync(async () =>
        {
            await _lifecycle.RejectRefundAsync(refundId, new RejectRefundRequest { Reason = reason ?? "" }, ct);
            StatusMessage = "Đã từ chối yêu cầu hoàn tiền.";
        }, null, ct);
    }

    private async Task<IActionResult> RunAsync(Func<Task> action, string? prefix, CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid) { await LoadAsync(ct); return Page(); }
            await action();
            return RedirectToPage(new { id = Id });
        }
        catch (ApiException ex)
        {
            var redirect = await HandleApiErrorAsync(ex, prefix);
            if (redirect is not null) return redirect;
            try { await LoadAsync(ct); } catch (ApiException) { }
            return Page();
        }
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Event = await _events.GetAsync(Id, ct);
        Refunds = await _lifecycle.GetRefundsAsync(Id, ct);
    }
}
