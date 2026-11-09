using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using MarathonRMS.Services.Coupons;

namespace MarathonRMS.Services.Events;

/// <summary>Phần A – UC-25 hoãn / hủy giải (BR-15).</summary>
public interface IEventLifecycleService
{
    Task<EventLifecycleResultDto> PostponeAsync(int eventId, PostponeEventRequest request, CancellationToken ct = default);
    Task<EventLifecycleResultDto> CancelAsync(int eventId, CancelEventRequest request, CancellationToken ct = default);
}

/// <summary>Phần A – UC-50 hoàn tiền khi hủy giải (xử lý thủ công, ghi người thực hiện + mã giao dịch – BR-15, BR-36).</summary>
public interface IRefundService
{
    Task<IReadOnlyList<RefundDto>> GetByEventAsync(int eventId, CancellationToken ct = default);
    Task<IReadOnlyList<RefundDto>> GetMineAsync(CancellationToken ct = default);
    Task<RefundDto> UpdateBankInfoAsync(int refundId, UpdateRefundBankInfoRequest request, CancellationToken ct = default);
    Task<RefundDto> CompleteAsync(int refundId, CompleteRefundRequest request, CancellationToken ct = default);
    Task<RefundDto> RejectAsync(int refundId, RejectRefundRequest request, CancellationToken ct = default);
}

public class EventLifecycleService : IEventLifecycleService
{
    private readonly IEventLifecycleRepository _repo;
    private readonly ICouponService _coupons;
    private readonly IOrganizationScope _scope;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public EventLifecycleService(IEventLifecycleRepository repo, ICouponService coupons, IOrganizationScope scope,
        IDateTimeProvider clock, IAuditLogger audit, IUnitOfWork uow)
    {
        _repo = repo;
        _coupons = coupons;
        _scope = scope;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    /// <summary>Cùng tên khóa với đăng ký / chuyển nhượng / đổi cự ly – không ai đăng ký chen vào lúc đang hủy giải.</summary>
    private static string RegistrationLock(int eventId) => $"mrms:registrations:{eventId}";

    public async Task<EventLifecycleResultDto> PostponeAsync(int eventId, PostponeEventRequest request, CancellationToken ct = default)
    {
        await EnsureCanManageAsync(eventId, ct);
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RegistrationLock(eventId), ct);
        var ev = await _repo.GetEventForUpdateAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        EnsureChangeable(ev);
        if (ev.Status == EventStatus.Draft)
            throw new BusinessRuleException("BR-15", "Giải nháp chưa công bố – sửa ngày trực tiếp ở trang thông tin giải.");

        var today = DateOnly.FromDateTime(_clock.Now);
        var days = request.NewEventDate.DayNumber - ev.EventDate.DayNumber;
        if (days <= 0) throw new BusinessRuleException("BR-15", "Ngày mới phải sau ngày tổ chức hiện tại.", 400);
        if (request.NewEventDate <= today) throw new BusinessRuleException("BR-15", "Ngày mới phải sau hôm nay.", 400);

        var old = new { ev.EventDate, ev.EndDate, Policy = new { ev.Policy.InfoEditDeadline, ev.Policy.TransferDeadline, ev.Policy.CategoryChangeDeadline } };
        // BR-15: đăng ký giữ nguyên, các mốc gắn với ngày thi dời theo cùng số ngày
        ev.PostponedFromDate ??= ev.EventDate;
        ev.EventDate = ev.EventDate.AddDays(days);
        ev.EndDate = ev.EndDate.AddDays(days);
        foreach (var c in ev.Categories)
        {
            c.StartAt = c.StartAt.AddDays(days);
            foreach (var cp in c.Checkpoints.Where(cp => cp.CutOffAt is not null)) cp.CutOffAt = cp.CutOffAt!.Value.AddDays(days);
        }
        var p = ev.Policy;
        p.InfoEditDeadline = p.InfoEditDeadline?.AddDays(days);
        p.TransferDeadline = p.TransferDeadline?.AddDays(days);
        p.CategoryChangeDeadline = p.CategoryChangeDeadline?.AddDays(days);

        await _audit.LogAsync("Event.Postpone", nameof(Event), eventId.ToString(), old,
            new { ev.EventDate, ev.EndDate, ShiftedDays = days, Reason = request.Reason.Trim() }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new EventLifecycleResultDto { EventId = eventId, Status = ev.Status.ToString(), EventDate = ev.EventDate, ShiftedDays = days };
    }

    public async Task<EventLifecycleResultDto> CancelAsync(int eventId, CancelEventRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
            throw new BusinessRuleException("BR-15", "Hủy giải phải nhập lý do (ít nhất 10 ký tự).", 400);
        await EnsureCanManageAsync(eventId, ct);

        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RegistrationLock(eventId), ct);
        var ev = await _repo.GetEventForUpdateAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        EnsureChangeable(ev);

        var now = _clock.Now;
        var refund = ev.Policy.ForceMajeurePolicy == ForceMajeurePolicy.Refund || request.RefundPaidRegistrations;
        var result = new EventLifecycleResultDto { EventId = eventId, EventDate = ev.EventDate };

        var transferredFrom = await _repo.GetTransferLinksAsync(eventId, ct);
        var changePayments = (await _repo.GetCategoryChangePaymentsAsync(eventId, ct)).ToLookup(x => x.RegistrationId, x => x.Amount);

        foreach (var r in await _repo.GetActiveRegistrationsAsync(eventId, ct))
        {
            r.BibNumber = null;     // CK_Registrations_BibOnlyConfirmed
            r.CancelledAt = now;
            if (r.Status == RegistrationStatus.PendingPayment)
            {
                // Chưa thanh toán: hủy, trả lượt coupon, đóng thanh toán đang chờ
                r.Status = RegistrationStatus.Cancelled;
                r.ExpiresAt = null;
                if (r.CouponId is int couponId) await _coupons.ReleaseAsync(couponId, ct);
                foreach (var pay in r.Payments.Where(x => x.Status == PaymentStatus.Pending)) pay.Status = PaymentStatus.Expired;
                result.CancelledUnpaid++;
                continue;
            }

            // Đã xác nhận: hoàn số tiền đã trả cho suất = giá cuối + chênh lệch đã trả khi đổi cự ly, kể cả lúc suất
            // còn của người trước (lần theo chuỗi chuyển nhượng). Phí đổi cự ly / chuyển nhượng là phí dịch vụ – không hoàn.
            var changePaid = 0m;
            var changeCount = 0;
            for (int? id = r.RegistrationId, guard = 0; id is int cur && guard < 20; id = transferredFrom.TryGetValue(cur, out var prev) ? prev : null, guard++)
            {
                changePaid += changePayments[cur].Sum();
                changeCount += changePayments[cur].Count();
            }
            var amount = r.FinalPrice + Math.Max(0, changePaid - changeCount * ev.Policy.CategoryChangeFee);
            if (refund && amount > 0)
            {
                r.Status = RegistrationStatus.RefundPending;
                await _repo.AddAsync(new Refund { RegistrationId = r.RegistrationId, Amount = amount, Reason = RefundReason.EventCancelled }, ct);
                result.RefundsCreated++;
                result.RefundTotal += amount;
            }
            else
            {
                r.Status = RegistrationStatus.Cancelled;
                result.CancelledWithoutRefund++;
            }
        }
        foreach (var t in await _repo.GetPendingTransfersAsync(eventId, ct))
        {
            t.Status = TransferStatus.Cancelled;
            t.RespondedAt = now;
        }

        var oldStatus = ev.Status;
        ev.Status = EventStatus.Cancelled;
        ev.CancelReason = request.Reason.Trim();
        await _audit.LogAsync("Event.Cancel", nameof(Event), eventId.ToString(), new { Status = oldStatus.ToString() },
            new { Status = "Cancelled", ev.CancelReason, Refund = refund, result.RefundsCreated, result.RefundTotal, result.CancelledUnpaid }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        result.Status = ev.Status.ToString();
        return result;
    }

    private async Task EnsureCanManageAsync(int eventId, CancellationToken ct)
    {
        var header = await _repo.GetEventHeaderAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(header.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(header.OrganizationId, ct);
    }

    private static void EnsureChangeable(Event ev)
    {
        if (ev.Status is EventStatus.Completed or EventStatus.Cancelled)
            throw new BusinessRuleException("BR-15", "Giải đã kết thúc hoặc đã hủy.");
        if (ev.ResultsStatus != ResultsStatus.Draft)
            throw new BusinessRuleException("BR-15", "Giải đã có kết quả – không hoãn / hủy được.");
    }
}

public class RefundService : IRefundService
{
    private readonly IEventLifecycleRepository _repo;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public RefundService(IEventLifecycleRepository repo, IOrganizationScope scope, ICurrentUserService currentUser,
        IDateTimeProvider clock, IAuditLogger audit, IUnitOfWork uow)
    {
        _repo = repo;
        _scope = scope;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    private string UserId => _currentUser.UserId ?? throw new ForbiddenException("Chưa đăng nhập.");

    public async Task<IReadOnlyList<RefundDto>> GetByEventAsync(int eventId, CancellationToken ct = default)
    {
        var header = await _repo.GetEventHeaderAsync(eventId, ct) ?? throw new NotFoundException("Không tìm thấy giải.");
        _scope.EnsureCanManage(header.OrganizationId);
        return (await _repo.GetByEventAsync(eventId, ct)).Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<RefundDto>> GetMineAsync(CancellationToken ct = default) =>
        (await _repo.GetForUserAsync(UserId, ct)).Select(ToDto).ToList();

    public async Task<RefundDto> UpdateBankInfoAsync(int refundId, UpdateRefundBankInfoRequest request, CancellationToken ct = default)
    {
        var userId = UserId;
        var refund = await _repo.GetTrackedAsync(refundId, ct);
        if (refund is null || (refund.Registration.RegisteredByUserId != userId && refund.Registration.Participant.LinkedUserId != userId))
            throw new NotFoundException("Không tìm thấy yêu cầu hoàn tiền.");
        if (refund.Status != RefundStatus.Pending) throw new BusinessRuleException("BR-15", "Yêu cầu hoàn tiền đã được xử lý.");
        refund.BankInfo = request.BankInfo.Trim();
        await _uow.SaveChangesAsync(ct);
        return ToDto(refund);
    }

    public async Task<RefundDto> CompleteAsync(int refundId, CompleteRefundRequest request, CancellationToken ct = default)
    {
        var refund = await LoadForOrganizerAsync(refundId, ct);
        refund.Status = RefundStatus.Completed;
        refund.TransactionRef = request.TransactionRef.Trim();
        refund.ProcessedById = UserId;
        refund.ProcessedAt = _clock.Now;
        refund.Registration.Status = RegistrationStatus.Refunded;
        await _audit.LogAsync("Refund.Complete", nameof(Refund), refundId.ToString(), new { Status = "Pending" },
            new { Status = "Completed", refund.Amount, refund.TransactionRef, refund.RegistrationId }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(refund);
    }

    public async Task<RefundDto> RejectAsync(int refundId, RejectRefundRequest request, CancellationToken ct = default)
    {
        var refund = await LoadForOrganizerAsync(refundId, ct);
        refund.Status = RefundStatus.Rejected;
        refund.ProcessedById = UserId;
        refund.ProcessedAt = _clock.Now;
        refund.Registration.Status = RegistrationStatus.Cancelled;
        await _audit.LogAsync("Refund.Reject", nameof(Refund), refundId.ToString(), new { Status = "Pending" },
            new { Status = "Rejected", refund.Amount, refund.RegistrationId, Reason = request.Reason.Trim() }, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(refund);
    }

    private async Task<Refund> LoadForOrganizerAsync(int refundId, CancellationToken ct)
    {
        var refund = await _repo.GetTrackedAsync(refundId, ct) ?? throw new NotFoundException("Không tìm thấy yêu cầu hoàn tiền.");
        _scope.EnsureCanManage(refund.Registration.Event.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(refund.Registration.Event.OrganizationId, ct);
        // BR-32: không tự xử lý hoàn tiền cho đăng ký của chính mình
        _scope.EnsureNotOwnRegistration(refund.Registration.Participant.LinkedUserId, refund.Registration.RegisteredByUserId);
        if (refund.Status != RefundStatus.Pending) throw new BusinessRuleException("BR-15", "Yêu cầu hoàn tiền đã được xử lý.");
        return refund;
    }

    private static RefundDto ToDto(Refund x)
    {
        var r = x.Registration;
        return new RefundDto
        {
            Id = x.RefundId,
            RegistrationId = x.RegistrationId,
            EventId = r.EventId,
            EventName = r.Event.Name,
            CategoryName = r.Category.Name,
            RunnerName = r.FullName,
            BibNumber = r.BibNumber,
            Phone = r.Phone,
            Email = r.Email,
            Amount = x.Amount,
            Reason = x.Reason.ToString(),
            Status = x.Status.ToString(),
            BankInfo = x.BankInfo,
            ProcessedAt = x.ProcessedAt,
            TransactionRef = x.TransactionRef
        };
    }
}
