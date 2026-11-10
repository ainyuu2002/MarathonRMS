using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Events;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using MarathonRMS.Services.Coupons;

namespace MarathonRMS.Services.Events;

public class EventLifecycleService : IEventLifecycleService
{
    private readonly IEventRepository _events;
    private readonly IRegistrationRepository _registrations;
    private readonly IBibTransferRepository _transfers;
    private readonly IRefundRepository _refunds;
    private readonly ICouponService _coupons;
    private readonly IOrganizationScope _scope;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public EventLifecycleService(IEventRepository events, IRegistrationRepository registrations,
        IBibTransferRepository transfers, IRefundRepository refunds, ICouponService coupons,
        IOrganizationScope scope, IDateTimeProvider clock, IAuditLogger audit, IUnitOfWork uow)
    {
        _events = events; _registrations = registrations; _transfers = transfers; _refunds = refunds;
        _coupons = coupons; _scope = scope; _clock = clock; _audit = audit; _uow = uow;
    }

    public async Task PostponeAsync(int eventId, PostponeEventRequest request, CancellationToken ct = default)
    {
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync($"mrms:event-lifecycle:{eventId}", ct);
        var ev = await LoadAsync(eventId, ct);
        EnsureMutable(ev);
        if (request.NewEventDate <= ev.EventDate)
            throw new BusinessRuleException("BR-15", "Ngày thi mới phải sau ngày thi hiện tại.", 400);

        var oldDate = ev.EventDate;
        var days = request.NewEventDate.DayNumber - oldDate.DayNumber;
        ev.PostponedFromDate ??= oldDate;
        ev.EventDate = ev.EventDate.AddDays(days);
        ev.EndDate = ev.EndDate.AddDays(days);
        ev.RegistrationOpenAt = ev.RegistrationOpenAt.AddDays(days);
        ev.RegistrationCloseAt = ev.RegistrationCloseAt.AddDays(days);
        ev.Policy.InfoEditDeadline = Shift(ev.Policy.InfoEditDeadline, days);
        ev.Policy.TransferDeadline = Shift(ev.Policy.TransferDeadline, days);
        ev.Policy.CategoryChangeDeadline = Shift(ev.Policy.CategoryChangeDeadline, days);
        foreach (var c in ev.Categories)
        {
            c.StartAt = c.StartAt.AddDays(days);
            foreach (var p in c.PriceTiers) { p.ValidFrom = p.ValidFrom.AddDays(days); p.ValidTo = p.ValidTo.AddDays(days); }
            foreach (var p in c.Checkpoints) p.CutOffAt = Shift(p.CutOffAt, days);
        }
        foreach (var s in ev.ScheduleItems) { s.StartsAt = s.StartsAt.AddDays(days); s.EndsAt = Shift(s.EndsAt, days); }
        foreach (var c in await _events.GetCouponsAsync(eventId, ct)) { c.ValidFrom = c.ValidFrom.AddDays(days); c.ValidTo = c.ValidTo.AddDays(days); }
        ev.Status = EventStatus.Postponed;
        await _audit.LogAsync("Event.Postpone", nameof(Event), eventId.ToString(), new { EventDate = oldDate }, new { ev.EventDate, ShiftDays = days }, ct);
        await _uow.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task CancelAsync(int eventId, CancelEventRequest request, CancellationToken ct = default)
    {
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync($"mrms:event-lifecycle:{eventId}", ct);
        var ev = await LoadAsync(eventId, ct);
        EnsureMutable(ev);
        if (!Enum.TryParse<ForceMajeurePolicy>(request.RefundOption, out var option) || option != ev.Policy.ForceMajeurePolicy)
            throw new BusinessRuleException("BR-15", "Phương án xử lý phải đúng chính sách bất khả kháng của giải.", 400);
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new BusinessRuleException("BR-15", "Phải nhập lý do hủy giải.", 400);

        var now = _clock.Now;
        foreach (var r in await _registrations.GetByEventTrackedAsync(eventId, ct))
        {
            if (r.Status == RegistrationStatus.PendingPayment)
            {
                r.Status = RegistrationStatus.Cancelled; r.CancelledAt = now; r.ExpiresAt = null;
                if (r.CouponId is int couponId) await _coupons.ReleaseAsync(couponId, ct);
            }
            else if (r.Status == RegistrationStatus.Confirmed && option == ForceMajeurePolicy.Refund && r.FinalPrice > 0)
            {
                r.Status = RegistrationStatus.RefundPending;
                if (!await _refunds.ExistsAsync(r.RegistrationId, ct))
                    await _refunds.AddAsync(new Refund { RegistrationId = r.RegistrationId, Amount = r.FinalPrice, Reason = RefundReason.EventCancelled }, ct);
            }
            else if (r.Status == RegistrationStatus.Confirmed && option != ForceMajeurePolicy.CarryOver)
            {
                r.Status = RegistrationStatus.Cancelled; r.CancelledAt = now;
            }
        }
        foreach (var t in await _transfers.GetOpenForEventAsync(eventId, ct)) { t.Status = TransferStatus.Cancelled; t.RespondedAt = now; }
        ev.Status = EventStatus.Cancelled; ev.CancelReason = request.Reason.Trim();
        await _audit.LogAsync("Event.Cancel", nameof(Event), eventId.ToString(), null, new { ev.CancelReason, RefundOption = option }, ct);
        await _uow.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private async Task<Event> LoadAsync(int id, CancellationToken ct)
    {
        var ev = await _events.GetForLifecycleAsync(id, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(ev.OrganizationId); await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct); return ev;
    }
    private static void EnsureMutable(Event ev)
    {
        if (ev.Status is EventStatus.Draft or EventStatus.Completed or EventStatus.Cancelled)
            throw new BusinessRuleException("BR-15", "Trạng thái hiện tại không cho phép hoãn hoặc hủy giải.");
    }
    private static DateTime? Shift(DateTime? value, int days) => value?.AddDays(days);
}
