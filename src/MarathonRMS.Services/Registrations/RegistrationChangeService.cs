using System.Text.RegularExpressions;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.Registrations;

/// <summary>Phần A – UC-46 đổi cự ly (BR-17), UC-47 chuyển nhượng BIB (BR-16), suất đặc biệt (BR-18).</summary>
public interface IRegistrationChangeService
{
    Task<RegistrationChangeOptionsDto> GetOptionsAsync(int registrationId, CancellationToken ct = default);
    Task<CategoryChangeResultDto> ChangeCategoryAsync(int registrationId, ChangeCategoryRequest request, CancellationToken ct = default);
    Task<TransferDto> CreateTransferAsync(int registrationId, CreateTransferRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TransferDto>> GetMyTransfersAsync(CancellationToken ct = default);
    Task<TransferDto> GetTransferAsync(int transferId, CancellationToken ct = default);
    Task<TransferDto> AcceptTransferAsync(int transferId, AcceptTransferRequest request, CancellationToken ct = default);
    Task RejectTransferAsync(int transferId, CancellationToken ct = default);
    Task CancelTransferAsync(int transferId, CancellationToken ct = default);
}

public class RegistrationChangeService : IRegistrationChangeService
{
    private static readonly Regex CccdPattern = new(@"^\d{12}$", RegexOptions.Compiled);

    private readonly IRegistrationChangeRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public RegistrationChangeService(IRegistrationChangeRepository repo, ICurrentUserService currentUser, IDateTimeProvider clock,
        IAuditLogger audit, IUnitOfWork uow)
    {
        _repo = repo;
        _currentUser = currentUser;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    /// <summary>Cùng tên khóa với luồng đăng ký (đăng ký / chuyển nhượng / đổi cự ly của một giải chạy lần lượt – BR-01, BR-04).</summary>
    private static string RegistrationLock(int eventId) => RegistrationLocks.Event(eventId);

    private string UserId => _currentUser.UserId ?? throw new ForbiddenException("Chưa đăng nhập.");

    // ============================ Tùy chọn ============================

    public async Task<RegistrationChangeOptionsDto> GetOptionsAsync(int registrationId, CancellationToken ct = default)
    {
        var r = await LoadOwnAsync(registrationId, ct);
        var now = _clock.Now;
        var p = r.Event.Policy;
        var categories = await _repo.GetCategoriesWithTiersAsync(r.EventId, ct);
        var counts = await _repo.GetActiveCountsAsync(r.EventId, ct);
        var pending = await _repo.GetPendingForRegistrationAsync(registrationId, ct);
        var changeBlocked = CategoryChangeBlockReason(r, now) ?? (pending is not null ? "Đang có yêu cầu chuyển nhượng chờ xử lý." : null);

        var options = changeBlocked is not null
            ? new List<CategoryChangeQuoteDto>()
            : categories.Where(c => c.CategoryId != r.CategoryId)
                .Select(c => Quote(r, c, counts.GetValueOrDefault(c.CategoryId), now)).ToList();

        TransferDto? pendingDto = null;
        if (pending is not null)
        {
            var names = await _repo.GetUserNamesAsync(new[] { pending.FromUserId, pending.ToUserId }, ct);
            pending.Registration = r;
            pendingDto = ToDto(pending, UserId, names);
        }

        return new RegistrationChangeOptionsDto
        {
            RegistrationId = r.RegistrationId,
            EventId = r.EventId,
            EventName = r.Event.Name,
            CategoryName = r.Category.Name,
            RunnerName = r.FullName,
            BibNumber = r.BibNumber,
            TransferBlockedReason = TransferBlockReason(r, now) ?? (pending is not null ? "Đang có yêu cầu chuyển nhượng chờ người nhận xử lý." : null),
            TransferFee = p.TransferPolicy == TransferPolicy.Paid ? p.TransferFee : 0,
            TransferDeadline = TransferDeadline(r.Event),
            TransfersLeft = Math.Max(0, p.MaxTransfers - r.TransferCount),
            PendingTransfer = pendingDto,
            CategoryChangeBlockedReason = changeBlocked,
            CategoryChangeFee = p.CategoryChangeFee,
            CategoryChangeDeadline = ChangeDeadline(r.Event),
            CategoryChangesLeft = Math.Max(0, p.MaxCategoryChanges - r.ChangeCount),
            CategoryOptions = options
        };
    }

    // ============================ UC-46 đổi cự ly ============================

    public async Task<CategoryChangeResultDto> ChangeCategoryAsync(int registrationId, ChangeCategoryRequest request, CancellationToken ct = default)
    {
        var header = await LoadOwnHeaderAsync(registrationId, ct);
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RegistrationLock(header.EventId), ct);
        await _uow.LockAsync(RaceDayLocks.Bibs(header.EventId), ct);
        // Khóa của B: không đăng ký chen vào cự ly mới (lố suất) và không thanh toán / sửa đăng ký này cùng lúc
        await _uow.LockAsync(RegistrationLocks.Category(request.NewCategoryId), ct);
        await _uow.LockAsync(RegistrationLocks.Payment(registrationId), ct);

        var r = await _repo.GetRegistrationTrackedAsync(registrationId, ct) ?? throw new NotFoundException("Không tìm thấy đăng ký.");
        var now = _clock.Now;
        if (CategoryChangeBlockReason(r, now) is { } reason) throw new BusinessRuleException(reason == SpecialEntryLocked ? "BR-18" : "BR-17", reason);
        if (await _repo.GetPendingForRegistrationAsync(registrationId, ct) is not null)
            throw new BusinessRuleException("BR-17", "Đang có yêu cầu chuyển nhượng chờ xử lý – hủy yêu cầu đó trước.");

        var categories = await _repo.GetCategoriesWithTiersAsync(r.EventId, ct);
        var target = categories.FirstOrDefault(c => c.CategoryId == request.NewCategoryId && c.CategoryId != r.CategoryId)
            ?? throw new BusinessRuleException(ErrorCodes.Validation, "Cự ly mới không hợp lệ.", 400);
        var counts = await _repo.GetActiveCountsAsync(r.EventId, ct);
        var quote = Quote(r, target, counts.GetValueOrDefault(target.CategoryId), now);
        if (quote.BlockedReason is { } blocked) throw new BusinessRuleException("BR-17", blocked);
        if (quote.Total != request.ExpectedTotal)
            throw new BusinessRuleException(ErrorCodes.Conflict, $"Số tiền đã thay đổi thành {quote.Total:N0}đ – vui lòng xem lại trước khi xác nhận.");

        // Thanh toán thử (Mock) phần chênh lệch + phí; 0đ thì không tạo thanh toán (CK_Payments_Amount)
        Payment? payment = null;
        if (quote.Total > 0)
        {
            payment = NewMockPayment(r.RegistrationId, PaymentPurpose.CategoryChange, quote.Total, now);
            await _repo.AddPaymentAsync(payment, ct);
        }

        var old = new { CategoryId = r.CategoryId, Category = r.Category.Name, r.BibNumber };
        var hadBib = r.BibNumber is not null;
        r.CategoryId = target.CategoryId;
        r.BibNumber = null;          // BR-17: thu hồi BIB cũ
        r.ChangeCount++;
        await _uow.SaveChangesAsync(ct);
        if (hadBib)                  // … và gán lại trong dải của cự ly mới
        {
            r.BibNumber = await _repo.GetNextFreeBibAsync(r.EventId, target.BibFrom, target.BibTo, ct);
            await _uow.SaveChangesAsync(ct);
        }

        await _audit.LogAsync("Registration.ChangeCategory", nameof(Registration), registrationId.ToString(), old,
            new { CategoryId = target.CategoryId, Category = target.Name, r.BibNumber, quote.PriceDifference, quote.Fee, payment?.TxnRef }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new CategoryChangeResultDto
        {
            RegistrationId = registrationId, NewCategoryName = target.Name, AmountPaid = quote.Total,
            TxnRef = payment?.TxnRef, NewBibNumber = r.BibNumber
        };
    }

    // ============================ UC-47 chuyển nhượng ============================

    public async Task<TransferDto> CreateTransferAsync(int registrationId, CreateTransferRequest request, CancellationToken ct = default)
    {
        var userId = UserId;
        var header = await LoadOwnHeaderAsync(registrationId, ct);
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RegistrationLock(header.EventId), ct);

        var r = await _repo.GetRegistrationTrackedAsync(registrationId, ct) ?? throw new NotFoundException("Không tìm thấy đăng ký.");
        var now = _clock.Now;
        if (TransferBlockReason(r, now) is { } reason) throw new BusinessRuleException(reason == SpecialEntryLocked ? "BR-18" : "BR-16", reason);
        if (await _repo.GetPendingForRegistrationAsync(registrationId, ct) is not null)
            throw new BusinessRuleException("BR-16", "Đăng ký này đã có yêu cầu chuyển nhượng đang chờ.");

        var recipient = await _repo.FindActiveUserByEmailAsync(request.RecipientEmail.Trim(), ct)
            ?? throw new BusinessRuleException("BR-16", "Không tìm thấy tài khoản đang hoạt động với email này – người nhận cần có tài khoản.", 400);
        if (recipient.Id == userId) throw new BusinessRuleException("BR-16", "Không thể chuyển nhượng cho chính mình.", 400);
        var recipientSelf = await _repo.GetSelfParticipantAsync(recipient.Id, ct);
        if (recipientSelf is not null && await _repo.ParticipantHasActiveAsync(r.EventId, recipientSelf.ParticipantId, ct))
            throw new BusinessRuleException("BR-16", "Người nhận đã có đăng ký còn hiệu lực trong giải này.");

        var transfer = new BibTransfer
        {
            RegistrationId = r.RegistrationId,
            FromUserId = userId,
            ToUserId = recipient.Id,
            ToParticipantId = recipientSelf?.ParticipantId,
            Status = TransferStatus.Pending,
            FeeAmount = r.Event.Policy.TransferPolicy == TransferPolicy.Paid ? r.Event.Policy.TransferFee : 0,
            RequestedAt = now
        };
        await _repo.AddAsync(transfer, ct);
        try { await _uow.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) { throw DbErrors.ToBusinessError(ex, "Đăng ký này đã có yêu cầu chuyển nhượng đang chờ.", "BR-16"); }
        await tx.CommitAsync(ct);

        transfer.Registration = r;
        var names = await _repo.GetUserNamesAsync(new[] { userId, recipient.Id }, ct);
        return ToDto(transfer, userId, names);
    }

    public async Task<IReadOnlyList<TransferDto>> GetMyTransfersAsync(CancellationToken ct = default)
    {
        var userId = UserId;
        var list = await _repo.GetForUserAsync(userId, ct);
        var names = await _repo.GetUserNamesAsync(list.SelectMany(t => new[] { t.FromUserId, t.ToUserId }), ct);
        return list.Select(t => ToDto(t, userId, names)).ToList();
    }

    public async Task<TransferDto> GetTransferAsync(int transferId, CancellationToken ct = default)
    {
        var userId = UserId;
        var t = await _repo.GetDetailsAsync(transferId, ct);   // không theo dõi: các thao tác sau sẽ đọc lại sau khi khóa
        if (t is null || (t.FromUserId != userId && t.ToUserId != userId)) throw new NotFoundException("Không tìm thấy yêu cầu chuyển nhượng.");
        var names = await _repo.GetUserNamesAsync(new[] { t.FromUserId, t.ToUserId }, ct);
        return ToDto(t, userId, names);
    }

    public async Task<TransferDto> AcceptTransferAsync(int transferId, AcceptTransferRequest request, CancellationToken ct = default)
    {
        var userId = UserId;
        var probe = await GetTransferAsync(transferId, ct);
        if (probe.Direction != "Incoming") throw new NotFoundException("Không tìm thấy yêu cầu chuyển nhượng.");

        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RegistrationLock(probe.EventId), ct);
        // Khóa của B (lấy trước khi nạp bản theo dõi): không sửa thông tin đăng ký cũ chen vào
        await _uow.LockAsync(RegistrationLocks.Payment(probe.RegistrationId), ct);
        var t = await _repo.GetTrackedAsync(transferId, ct) ?? throw new NotFoundException("Không tìm thấy yêu cầu chuyển nhượng.");
        var old = t.Registration;
        var ev = old.Event;
        // Cự ly giữ nguyên suất (cũ ⇒ Transferred, mới ⇒ Confirmed) nhưng không cho đăng ký mới chen vào lúc đổi chủ.
        // Cự ly của đăng ký chỉ đổi dưới khóa giải đang giữ ⇒ đọc trước khóa này vẫn đúng.
        await _uow.LockAsync(RegistrationLocks.Category(old.CategoryId), ct);
        var now = _clock.Now;

        if (t.Status != TransferStatus.Pending)
            throw new BusinessRuleException("BR-16", "Yêu cầu này không còn chờ xử lý.");
        if (TransferBlockReason(old, now) is { } reason)
        {
            // Quá hạn / giải đổi trạng thái sau khi gửi ⇒ yêu cầu hết hiệu lực
            t.Status = TransferStatus.Expired;
            t.RespondedAt = now;
            await _uow.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw new BusinessRuleException("BR-16", "Không thể hoàn tất chuyển nhượng: " + reason);
        }

        // Hồ sơ chính chủ của người nhận (tạo nếu chưa có) + BR-01
        var participant = await _repo.GetSelfParticipantAsync(userId, ct);
        if (participant is null)
        {
            participant = new Participant
            {
                OwnerUserId = userId, LinkedUserId = userId, Relationship = ParticipantRelationship.Self,
                FullName = request.FullName.Trim(), CreatedAt = now
            };
            await _repo.AddParticipantAsync(participant, ct);
            await _uow.SaveChangesAsync(ct);
        }
        else if (await _repo.ParticipantHasActiveAsync(ev.EventId, participant.ParticipantId, ct))
            throw new BusinessRuleException("BR-01", "Bạn đã có đăng ký còn hiệu lực trong giải này.");

        var snapshot = BuildSnapshot(request);
        var age = AgeFor(snapshot.DateOfBirth, ev);
        EnsureAgeAllowed(age, old.Category);
        if (age < ev.Policy.GuardianConsentUnderAge &&
            (snapshot.GuardianName is null || snapshot.GuardianPhone is null || !request.GuardianConsent))
            throw new BusinessRuleException("BR-06", $"Người chạy dưới {ev.Policy.GuardianConsentUnderAge} tuổi – cần thông tin và đồng ý của người giám hộ.", 400);
        if (old.Category.EscortRequiredUnderAge is int escortAge && age < escortAge && (snapshot.EscortName is null || snapshot.EscortPhone is null))
            throw new BusinessRuleException("BR-06", $"Người chạy dưới {escortAge} tuổi – cần khai người chạy kèm.", 400);
        ValidateIdentity(snapshot.IdType, snapshot.IdNumber, age);
        if (snapshot.IdType is IdType idType && snapshot.IdNumber is { } idNumber &&
            await _repo.IdNumberHasActiveAsync(ev.EventId, idType, idNumber, ct))
            throw new BusinessRuleException("BR-01", "Số giấy tờ này đã có đăng ký còn hiệu lực trong giải.");
        var hasMedical = !string.IsNullOrWhiteSpace(request.BloodType) || !string.IsNullOrWhiteSpace(request.MedicalNotes);
        if (hasMedical && !request.HealthDataConsent)
            throw new BusinessRuleException("BR-09", "Bạn đã khai nhóm máu / ghi chú y tế – cần đồng ý cho phép xử lý dữ liệu sức khỏe.", 400);

        // Phí chuyển nhượng (Paid): người nhận trả trước khi hoàn tất – thanh toán thử (Mock)
        Payment? payment = null;
        if (t.FeeAmount > 0)
        {
            payment = NewMockPayment(old.RegistrationId, PaymentPurpose.TransferFee, t.FeeAmount, now);
            await _repo.AddPaymentAsync(payment, ct);
        }

        // Đăng ký cũ ⇒ Transferred, nhả BIB trước (BIB là duy nhất trong giải) rồi đăng ký mới giữ cự ly + BIB
        var bib = old.BibNumber;
        old.Status = RegistrationStatus.Transferred;
        old.BibNumber = null;
        old.CancelledAt = now;
        await _uow.SaveChangesAsync(ct);

        var created = snapshot;
        created.EventId = ev.EventId;
        created.CategoryId = old.CategoryId;
        created.ParticipantId = participant.ParticipantId;
        created.RegisteredByUserId = userId;
        created.EntryType = old.EntryType;
        created.Status = RegistrationStatus.Confirmed;
        created.BibNumber = bib;
        created.OriginalPrice = old.OriginalPrice;
        created.DiscountAmount = old.DiscountAmount;
        created.CoinDiscountAmount = old.CoinDiscountAmount;
        created.FinalPrice = old.FinalPrice;
        created.QrToken = Guid.NewGuid().ToString("N");
        created.RegisteredAt = now;
        created.ConfirmedAt = now;
        created.ChangeCount = old.ChangeCount;
        created.TransferCount = old.TransferCount + 1;
        created.HideFromPublicList = request.HideFromPublicList;
        created.WaiverAccepted = true;
        created.HealthDeclarationAccepted = true;
        created.BloodType = request.HealthDataConsent ? Clean(request.BloodType) : null;
        created.MedicalNotes = request.HealthDataConsent ? Clean(request.MedicalNotes) : null;
        await _repo.AddRegistrationAsync(created, ct);
        try { await _uow.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) { throw DbErrors.ToBusinessError(ex, "Người nhận hoặc số giấy tờ đã có đăng ký còn hiệu lực trong giải.", "BR-01"); }

        await AddConsentAsync(userId, created.RegistrationId, ConsentType.Waiver, ev.WaiverVersion, true, now, ct);
        if (request.HealthDataConsent) await AddConsentAsync(userId, created.RegistrationId, ConsentType.HealthData, 1, true, now, ct);
        await AddConsentAsync(userId, created.RegistrationId, ConsentType.PhotoMarketing, 1, request.PhotoMarketingConsent, now, ct);
        if (request.GuardianConsent) await AddConsentAsync(userId, created.RegistrationId, ConsentType.Guardian, 1, true, now, ct);

        t.Status = TransferStatus.Accepted;
        t.RespondedAt = now;
        t.ToParticipantId = participant.ParticipantId;
        t.NewRegistrationId = created.RegistrationId;
        await _audit.LogAsync("Registration.Transfer", nameof(Registration), old.RegistrationId.ToString(),
            new { From = t.FromUserId, old.FullName, BibNumber = bib },
            new { To = userId, NewRegistrationId = created.RegistrationId, created.FullName, t.FeeAmount, payment?.TxnRef }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var names = await _repo.GetUserNamesAsync(new[] { t.FromUserId, t.ToUserId }, ct);
        return ToDto(t, userId, names);
    }

    public Task RejectTransferAsync(int transferId, CancellationToken ct = default) =>
        CloseTransferAsync(transferId, incoming: true, TransferStatus.Rejected, ct);

    public Task CancelTransferAsync(int transferId, CancellationToken ct = default) =>
        CloseTransferAsync(transferId, incoming: false, TransferStatus.Cancelled, ct);

    private async Task CloseTransferAsync(int transferId, bool incoming, TransferStatus target, CancellationToken ct)
    {
        var probe = await GetTransferAsync(transferId, ct);
        if (probe.Direction != (incoming ? "Incoming" : "Outgoing")) throw new NotFoundException("Không tìm thấy yêu cầu chuyển nhượng.");
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RegistrationLock(probe.EventId), ct);
        var t = await _repo.GetTrackedAsync(transferId, ct) ?? throw new NotFoundException("Không tìm thấy yêu cầu chuyển nhượng.");
        if (t.Status != TransferStatus.Pending) throw new BusinessRuleException("BR-16", "Yêu cầu này không còn chờ xử lý.");
        t.Status = target;
        t.RespondedAt = _clock.Now;
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // ============================ Quy tắc ============================

    private const string SpecialEntryLocked = "Suất đặc biệt (mời, đối tác, pacer…) không được chuyển nhượng hay đổi cự ly.";

    private static DateTime TransferDeadline(Event ev) => ev.Policy.TransferDeadline ?? ev.RegistrationCloseAt;
    private static DateTime ChangeDeadline(Event ev) => ev.Policy.CategoryChangeDeadline ?? ev.RegistrationCloseAt;

    /// <summary>Điều kiện chung: đăng ký đã xác nhận, giải chưa diễn ra, chưa nhận kit, BR-18.</summary>
    private static string? CommonBlockReason(Registration r, DateTime now)
    {
        if (r.Status != RegistrationStatus.Confirmed) return "Chỉ đăng ký đã xác nhận (đã thanh toán) mới chuyển nhượng / đổi cự ly được.";
        if (r.Event.Status is not (EventStatus.RegistrationOpen or EventStatus.RegistrationClosed or EventStatus.Postponed))
            return "Giải không còn cho phép thay đổi đăng ký.";
        if (r.Event.ResultsStatus != ResultsStatus.Draft || now.Date >= r.Event.EventDate.ToDateTime(TimeOnly.MinValue))
            return "Giải đã diễn ra.";
        if (r.RaceKitPickup is not null) return "Đã nhận race kit – không thay đổi được nữa.";
        if (r.EntryType != EntryType.Public && r.Event.Policy.LockSpecialEntries)
            return SpecialEntryLocked;
        return null;
    }

    private static string? TransferBlockReason(Registration r, DateTime now)
    {
        var p = r.Event.Policy;
        if (p.TransferPolicy == TransferPolicy.Forbidden) return "Giải không cho phép chuyển nhượng.";
        if (CommonBlockReason(r, now) is { } common) return common;
        if (now >= TransferDeadline(r.Event)) return $"Đã quá hạn chuyển nhượng ({TransferDeadline(r.Event):dd/MM/yyyy HH:mm}).";
        if (r.TransferCount >= p.MaxTransfers) return $"Suất này đã chuyển nhượng tối đa {p.MaxTransfers} lần.";
        return null;
    }

    private static string? CategoryChangeBlockReason(Registration r, DateTime now)
    {
        var p = r.Event.Policy;
        if (p.MaxCategoryChanges <= 0) return "Giải không cho phép đổi cự ly.";
        if (CommonBlockReason(r, now) is { } common) return common;
        if (now >= ChangeDeadline(r.Event)) return $"Đã quá hạn đổi cự ly ({ChangeDeadline(r.Event):dd/MM/yyyy HH:mm}).";
        if (r.ChangeCount >= p.MaxCategoryChanges) return $"Đã đổi cự ly tối đa {p.MaxCategoryChanges} lần.";
        return null;
    }

    /// <summary>BR-17: chênh lệch theo giá hiện hành của hai cự ly (đổi xuống không hoàn) + phí đổi; kiểm tra suất, tuổi, chạy kèm.</summary>
    private static CategoryChangeQuoteDto Quote(Registration r, RaceCategory target, int activeCount, DateTime now)
    {
        var diff = Math.Max(0, PriceAt(target, now) - PriceAt(r.Category, now));
        var fee = r.Event.Policy.CategoryChangeFee;
        var age = AgeFor(r.DateOfBirth, r.Event);
        string? blocked = null;
        if (activeCount >= target.Capacity) blocked = "Cự ly đã hết suất.";
        else if (age < target.MinAge || (target.MaxAge is int max && age > max))
            blocked = $"Không đúng tuổi ({age} tuổi; cự ly từ {target.MinAge}" + (target.MaxAge is int m ? $" đến {m}" : "") + ").";
        else if (target.EscortRequiredUnderAge is int escortAge && age < escortAge && (r.EscortName is null || r.EscortPhone is null))
            blocked = $"Cự ly yêu cầu người chạy kèm với người dưới {escortAge} tuổi – đăng ký chưa khai người chạy kèm.";
        return new CategoryChangeQuoteDto
        {
            CategoryId = target.CategoryId, CategoryName = target.Name, DistanceKm = target.DistanceKm,
            RemainingSlots = Math.Max(0, target.Capacity - activeCount),
            PriceDifference = diff, Fee = fee, Total = diff + fee, BlockedReason = blocked
        };
    }

    /// <summary>Giá hiện hành của cự ly; ngoài thời gian bán thì lấy mức giá cuối cùng.</summary>
    private static decimal PriceAt(RaceCategory c, DateTime now) =>
        c.PriceTiers.FirstOrDefault(t => t.ValidFrom <= now && now < t.ValidTo)?.Price
        ?? c.PriceTiers.OrderBy(t => t.ValidTo).LastOrDefault()?.Price ?? 0;

    private static int AgeFor(DateOnly dob, Event ev) =>
        ev.Policy.AgeCalculationMode == AgeCalculationMode.CalendarYear ? ev.EventDate.Year - dob.Year : AgeCalculator.AgeAt(dob, ev.EventDate);

    private static void EnsureAgeAllowed(int age, RaceCategory c)
    {
        if (age < c.MinAge || (c.MaxAge is int max && age > max))
            throw new BusinessRuleException("BR-05", $"Cự ly {c.Name} dành cho người từ {c.MinAge}" + (c.MaxAge is int m ? $" đến {m}" : "") +
                                                     $" tuổi (tính theo giải: {age} tuổi).", 400);
    }

    private static void ValidateIdentity(IdType? idType, string? idNumber, int age)
    {
        if (idNumber is null)
        {
            if (age >= 14) throw new BusinessRuleException("BR-07", "Người chạy từ 14 tuổi phải khai số CCCD hoặc hộ chiếu.", 400);
            return;
        }
        if (idType is null) throw new BusinessRuleException("BR-07", "Vui lòng chọn loại giấy tờ.", 400);
        if (idType == IdType.CCCD && !CccdPattern.IsMatch(idNumber)) throw new BusinessRuleException("BR-07", "Số CCCD gồm 12 chữ số.", 400);
    }

    // ============================ Hỗ trợ ============================

    private async Task<Registration> LoadOwnAsync(int registrationId, CancellationToken ct)
    {
        var r = await _repo.GetRegistrationTrackedAsync(registrationId, ct);
        if (r is null || r.RegisteredByUserId != UserId) throw new NotFoundException("Không tìm thấy đăng ký.");
        return r;
    }

    private async Task<Registration> LoadOwnHeaderAsync(int registrationId, CancellationToken ct)
    {
        var r = await _repo.GetRegistrationHeaderAsync(registrationId, ct);
        if (r is null || r.RegisteredByUserId != UserId) throw new NotFoundException("Không tìm thấy đăng ký.");
        return r;
    }

    private static Payment NewMockPayment(int registrationId, PaymentPurpose purpose, decimal amount, DateTime now) => new()
    {
        RegistrationId = registrationId,
        Purpose = purpose,
        Provider = PaymentProvider.Mock,
        Amount = amount,
        Status = PaymentStatus.Success,
        TxnRef = $"MRMS{registrationId}-{purpose}-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}",
        ProviderTransactionNo = "MOCK" + now.ToString("yyMMddHHmmssfff"),
        ResponseCode = "00",
        CreatedAt = now,
        PaidAt = now
    };

    private Task AddConsentAsync(string userId, int registrationId, ConsentType type, int version, bool granted, DateTime now, CancellationToken ct) =>
        _repo.AddConsentAsync(new ConsentRecord
        {
            UserId = userId, RegistrationId = registrationId, ConsentType = type, DocumentVersion = version, Granted = granted, CreatedAt = now
        }, ct);

    private static Registration BuildSnapshot(AcceptTransferRequest q) => new()
    {
        FullName = q.FullName.Trim(),
        BibName = q.BibName.Trim(),
        DateOfBirth = q.DateOfBirth,
        Gender = EnumParser.ParseOptional<Gender>(q.Gender) ?? throw new BusinessRuleException(ErrorCodes.Validation, "Vui lòng chọn giới tính.", 400),
        Nationality = q.Nationality.Trim(),
        IdType = EnumParser.ParseOptional<IdType>(q.IdType),
        IdNumber = Clean(q.IdNumber),
        Phone = q.Phone.Trim(),
        Email = Clean(q.Email),
        TShirtSize = EnumParser.ParseOptional<TShirtSize>(q.TShirtSize) ?? throw new BusinessRuleException(ErrorCodes.Validation, "Vui lòng chọn size áo.", 400),
        EmergencyContactName = q.EmergencyContactName.Trim(),
        EmergencyContactPhone = q.EmergencyContactPhone.Trim(),
        ClubName = Clean(q.ClubName),
        GuardianName = Clean(q.GuardianName),
        GuardianPhone = Clean(q.GuardianPhone),
        GuardianIdNumber = Clean(q.GuardianIdNumber),
        GuardianConsent = q.GuardianConsent,
        EscortName = Clean(q.EscortName),
        EscortPhone = Clean(q.EscortPhone)
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TransferDto ToDto(BibTransfer t, string userId, IReadOnlyDictionary<string, (string Name, string Email)> names)
    {
        var r = t.Registration;
        var to = names.GetValueOrDefault(t.ToUserId);
        var from = names.GetValueOrDefault(t.FromUserId);
        return new TransferDto
        {
            Id = t.TransferId,
            RegistrationId = t.RegistrationId,
            EventId = r.EventId,
            EventName = r.Event.Name,
            EventDate = r.Event.EventDate,
            CategoryName = r.Category.Name,
            Direction = t.FromUserId == userId ? "Outgoing" : "Incoming",
            FromName = from.Name ?? "",
            ToEmail = to.Email ?? "",
            ToName = to.Name ?? "",
            Status = t.Status.ToString(),
            FeeAmount = t.FeeAmount,
            RequestedAt = t.RequestedAt,
            RespondedAt = t.RespondedAt,
            Deadline = TransferDeadline(r.Event),
            NewRegistrationId = t.NewRegistrationId
        };
    }
}
