using System.Text.RegularExpressions;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using MarathonRMS.Services.Coupons;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.Registrations;

/// <summary>Quy tắc dùng chung cho đăng ký – cũng dùng khi chuyển nhượng / đổi cự ly (UC-46, 47).</summary>
public static class RegistrationRules
{
    /// <summary>Tuổi theo AgeCalculationMode của giải (BR-05).</summary>
    public static int AgeFor(DateOnly dateOfBirth, Event ev) =>
        ev.Policy.AgeCalculationMode == AgeCalculationMode.CalendarYear
            ? ev.EventDate.Year - dateOfBirth.Year
            : AgeCalculator.AgeAt(dateOfBirth, ev.EventDate);

    /// <summary>BR-05: tuổi nằm trong [MinAge, MaxAge] của cự ly.</summary>
    public static void EnsureAgeAllowed(int age, RaceCategory category)
    {
        if (age < category.MinAge || (category.MaxAge is int max && age > max))
            throw new BusinessRuleException("BR-05",
                $"Cự ly {category.Name} dành cho người từ {category.MinAge}" + (category.MaxAge is int m ? $" đến {m}" : "") +
                $" tuổi (tính theo giải: {age} tuổi).", 400);
    }

    /// <summary>Đăng ký còn hiệu lực (giữ suất – BR-04).</summary>
    public static bool IsActive(RegistrationStatus s) => s is RegistrationStatus.PendingPayment or RegistrationStatus.Confirmed;

    /// <summary>Hạn tự sửa thông tin (BR-08): InfoEditDeadline, nếu BTC không đặt thì đến lúc đóng đăng ký.</summary>
    public static DateTime InfoEditDeadline(Event ev) => ev.Policy.InfoEditDeadline ?? ev.RegistrationCloseAt;

    /// <summary>Khóa ghi theo giải cho mọi thao tác ảnh hưởng suất / BR-01 (đăng ký, chuyển nhượng, đổi cự ly).</summary>
    public static string Lock(int eventId) => $"mrms:registrations:{eventId}";
}

/// <summary>UC-39, 43, 44, 45, 48 + hết hạn giữ chỗ (BR-13).</summary>
public class RegistrationService : IRegistrationService
{
    private static readonly Regex CccdPattern = new(@"^\d{12}$", RegexOptions.Compiled);

    private readonly IRegistrationRepository _registrations;
    private readonly ICouponService _coupons;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemSettingsProvider _settings;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public RegistrationService(IRegistrationRepository registrations, ICouponService coupons, IOrganizationScope scope,
        ICurrentUserService currentUser, ISystemSettingsProvider settings, IDateTimeProvider clock, IAuditLogger audit, IUnitOfWork uow)
    {
        _registrations = registrations;
        _coupons = coupons;
        _scope = scope;
        _currentUser = currentUser;
        _settings = settings;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    private string UserId => _currentUser.UserId ?? throw new ForbiddenException("Chưa đăng nhập.");

    // ============================ UC-39 ============================

    public async Task<MyRegistrationDto> CreateAsync(CreateRegistrationRequest request, CancellationToken ct = default)
    {
        var userId = UserId;
        var now = _clock.Now;
        var participant = await _registrations.GetParticipantAsync(request.ParticipantId, ct);
        if (participant is null || participant.OwnerUserId != userId)
            throw new NotFoundException("Không tìm thấy hồ sơ người chạy.");
        var category = await _registrations.GetCategoryForRegistrationAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Không tìm thấy cự ly.");
        var ev = category.Event;

        // BR-03
        if (ev.Status != EventStatus.RegistrationOpen || now < ev.RegistrationOpenAt || now >= ev.RegistrationCloseAt)
            throw new BusinessRuleException("BR-03", "Giải hiện không trong thời gian nhận đăng ký.");
        if (string.IsNullOrWhiteSpace(ev.WaiverText))
            throw new BusinessRuleException("BR-38", "Giải chưa có văn bản miễn trừ trách nhiệm – chưa nhận đăng ký.");

        var snapshot = BuildSnapshot(request);
        var age = RegistrationRules.AgeFor(snapshot.DateOfBirth, ev);
        RegistrationRules.EnsureAgeAllowed(age, category);                      // BR-05
        ValidateGuardianAndEscort(request, age, ev, category);                  // BR-06
        ValidateIdentity(snapshot.IdType, snapshot.IdNumber, age);              // BR-07
        var hasMedical = !string.IsNullOrWhiteSpace(request.BloodType) || !string.IsNullOrWhiteSpace(request.MedicalNotes);
        if (hasMedical && !request.HealthDataConsent)                            // BR-09
            throw new BusinessRuleException("BR-09", "Bạn đã khai nhóm máu / ghi chú y tế – cần đồng ý cho phép xử lý dữ liệu sức khỏe.", 400);

        // BR-10: giá theo mức giá chứa thời điểm đăng ký
        var tier = category.PriceTiers.FirstOrDefault(t => t.ValidFrom <= now && now < t.ValidTo)
            ?? throw new BusinessRuleException("BR-10", "Cự ly chưa có mức giá cho thời điểm hiện tại.");
        var original = tier.Price;

        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RegistrationRules.Lock(ev.EventId), ct);

        // BR-01: một đăng ký còn hiệu lực / người / giải – theo hồ sơ và theo số giấy tờ
        if (await _registrations.ParticipantHasActiveAsync(ev.EventId, participant.ParticipantId, null, ct))
            throw new BusinessRuleException("BR-01", "Người chạy này đã có đăng ký còn hiệu lực trong giải.");
        if (snapshot.IdType is IdType idType && snapshot.IdNumber is { } idNumber &&
            await _registrations.IdNumberHasActiveAsync(ev.EventId, idType, idNumber, null, ct))
            throw new BusinessRuleException("BR-01", "Số giấy tờ này đã được dùng cho một đăng ký khác trong giải.");
        // BR-02
        if (participant.Relationship != ParticipantRelationship.Self &&
            await _registrations.CountActiveDependentsAsync(ev.EventId, userId, ct) >= ev.Policy.MaxDependentsPerAccount)
            throw new BusinessRuleException("BR-02", $"Mỗi tài khoản đăng ký tối đa {ev.Policy.MaxDependentsPerAccount} người phụ thuộc trong một giải.");
        // BR-04: kiểm tra suất trong cùng transaction (đã khóa theo giải)
        if (await _registrations.CountActiveInCategoryAsync(category.CategoryId, ct) >= category.Capacity)
            throw new BusinessRuleException("BR-04", $"Cự ly {category.Name} đã hết suất.");

        // BR-11: coupon (tính trên giá gốc, trừ lượt trong cùng transaction)
        decimal discount = 0;
        int? couponId = null;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var quote = await _coupons.QuoteAsync(request.CouponCode.Trim(), category.CategoryId, original, userId, ct);
            discount = quote.DiscountAmount;
            couponId = quote.CouponId;
            await _coupons.RedeemAsync(quote.CouponId, ct);
        }
        var final = original - discount;

        var holdMinutes = await _settings.GetIntAsync("Payment.HoldMinutes.Mock", 15, ct);
        var entity = snapshot;
        entity.EventId = ev.EventId;
        entity.CategoryId = category.CategoryId;
        entity.ParticipantId = participant.ParticipantId;
        entity.RegisteredByUserId = userId;
        entity.EntryType = EntryType.Public;
        entity.OriginalPrice = original;
        entity.DiscountAmount = discount;
        entity.FinalPrice = final;
        entity.CouponId = couponId;
        entity.QrToken = Guid.NewGuid().ToString("N");
        entity.RegisteredAt = now;
        entity.HideFromPublicList = request.HideFromPublicList;
        entity.WaiverAccepted = request.WaiverAccepted;
        entity.HealthDeclarationAccepted = request.HealthDeclarationAccepted;
        entity.BloodType = request.HealthDataConsent ? Clean(request.BloodType) : null;
        entity.MedicalNotes = request.HealthDataConsent ? Clean(request.MedicalNotes) : null;
        // BR-12: giá cuối 0đ ⇒ xác nhận ngay; còn lại giữ chỗ theo BR-13
        if (final == 0)
        {
            entity.Status = RegistrationStatus.Confirmed;
            entity.ConfirmedAt = now;
        }
        else
        {
            entity.Status = RegistrationStatus.PendingPayment;
            entity.ExpiresAt = now.AddMinutes(holdMinutes);
        }

        await _registrations.AddAsync(entity, ct);
        await SaveAsync(ct);

        // BR-09: mỗi đồng ý một bản ghi có phiên bản
        await AddConsentAsync(userId, entity.RegistrationId, ConsentType.Waiver, ev.WaiverVersion, true, now, ct);
        if (request.HealthDataConsent) await AddConsentAsync(userId, entity.RegistrationId, ConsentType.HealthData, 1, true, now, ct);
        await AddConsentAsync(userId, entity.RegistrationId, ConsentType.PhotoMarketing, 1, request.PhotoMarketingConsent, now, ct);
        if (request.GuardianConsent) await AddConsentAsync(userId, entity.RegistrationId, ConsentType.Guardian, 1, true, now, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToMyDto(entity, ev, category, participant, userId, now);
    }

    // ============================ UC-43 ============================

    public async Task<IReadOnlyList<MyRegistrationDto>> GetMineAsync(CancellationToken ct = default)
    {
        var userId = UserId;
        var now = _clock.Now;
        return (await _registrations.GetMineAsync(userId, ct)).Select(r => ToMyDto(r, userId, now)).ToList();
    }

    // ============================ UC-44 ============================

    public async Task<MyRegistrationDto> UpdateInfoAsync(int registrationId, UpdateRegistrationInfoRequest request, CancellationToken ct = default)
    {
        var userId = UserId;
        var now = _clock.Now;
        var r = await LoadOwnAsync(registrationId, userId, ct);
        if (!RegistrationRules.IsActive(r.Status))
            throw new BusinessRuleException("BR-08", "Đăng ký không còn hiệu lực – không sửa được.");
        if (now >= RegistrationRules.InfoEditDeadline(r.Event))
            throw new BusinessRuleException("BR-08",
                $"Đã quá hạn tự sửa thông tin ({RegistrationRules.InfoEditDeadline(r.Event):dd/MM/yyyy HH:mm}). Liên hệ ban tổ chức.");

        r.BibName = request.BibName.Trim();
        r.TShirtSize = EnumParser.ParseOptional<TShirtSize>(request.TShirtSize) ?? r.TShirtSize;
        r.Phone = request.Phone.Trim();
        r.EmergencyContactName = request.EmergencyContactName.Trim();
        r.EmergencyContactPhone = request.EmergencyContactPhone.Trim();
        r.ClubName = Clean(request.ClubName);
        r.HideFromPublicList = request.HideFromPublicList;
        await _uow.SaveChangesAsync(ct);
        return ToMyDto(r, userId, now);
    }

    // ============================ UC-45 ============================

    public async Task CancelAsync(int registrationId, CancellationToken ct = default)
    {
        var userId = UserId;
        var probe = await _registrations.GetHeaderAsync(registrationId, ct);
        if (probe is null || probe.RegisteredByUserId != userId) throw new NotFoundException("Không tìm thấy đăng ký.");
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RegistrationRules.Lock(probe.EventId), ct);
        var r = await _registrations.GetTrackedAsync(registrationId, ct) ?? throw new NotFoundException("Không tìm thấy đăng ký.");
        // BR-14: VĐV tự bỏ không hoàn tiền – chỉ hủy được đăng ký chưa thanh toán
        if (r.Status != RegistrationStatus.PendingPayment)
            throw new BusinessRuleException("BR-14",
                r.Status == RegistrationStatus.Confirmed
                    ? "Đăng ký đã thanh toán không thể hủy (không hoàn tiền). Bạn có thể chuyển nhượng nếu giải cho phép."
                    : "Đăng ký này không còn hiệu lực.");

        await ReleaseAsync(r, RegistrationStatus.Cancelled, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // ============================ UC-48 ============================

    public IQueryable<RegistrationListItemDto> QueryForOrganizer()
    {
        var organizationId = _scope.RequireOrganizationId();
        return _registrations.QueryAll()
            .Where(r => r.Event.OrganizationId == organizationId)
            .Select(r => new RegistrationListItemDto
            {
                Id = r.RegistrationId,
                EventId = r.EventId,
                CategoryId = r.CategoryId,
                CategoryName = r.Category.Name,
                FullName = r.FullName,
                BibName = r.BibName,
                BibNumber = r.BibNumber,
                Gender = r.Gender == Gender.F ? "F" : "M",
                DateOfBirth = r.DateOfBirth,
                Nationality = r.Nationality,
                IdType = r.IdType == IdType.CCCD ? "CCCD" : r.IdType == IdType.Passport ? "Passport"
                    : r.IdType == IdType.BirthCertificate ? "BirthCertificate" : null,
                IdNumber = r.IdNumber,
                Phone = r.Phone,
                Email = r.Email,
                TShirtSize = r.TShirtSize == TShirtSize.XS ? "XS" : r.TShirtSize == TShirtSize.S ? "S" : r.TShirtSize == TShirtSize.M ? "M"
                    : r.TShirtSize == TShirtSize.L ? "L" : r.TShirtSize == TShirtSize.XL ? "XL" : "XXL",
                ClubName = r.ClubName,
                EntryType = r.EntryType == EntryType.Public ? "Public" : r.EntryType == EntryType.Partner ? "Partner"
                    : r.EntryType == EntryType.Complimentary ? "Complimentary" : r.EntryType == EntryType.Pacer ? "Pacer" : "Charity",
                // Chuỗi điều kiện thay vì ToString() để OData $filter dịch được ra SQL
                Status = r.Status == RegistrationStatus.PendingPayment ? "PendingPayment"
                    : r.Status == RegistrationStatus.Confirmed ? "Confirmed"
                    : r.Status == RegistrationStatus.Cancelled ? "Cancelled"
                    : r.Status == RegistrationStatus.Expired ? "Expired"
                    : r.Status == RegistrationStatus.Transferred ? "Transferred"
                    : r.Status == RegistrationStatus.RefundPending ? "RefundPending" : "Refunded",
                FinalPrice = r.FinalPrice,
                RegisteredAt = r.RegisteredAt,
                RaceKitCollected = r.RaceKitPickup != null
            });
    }

    public async Task UpdateLockedInfoAsync(int registrationId, UpdateLockedInfoRequest request, CancellationToken ct = default)
    {
        var r = await _registrations.GetTrackedAsync(registrationId, ct) ?? throw new NotFoundException("Không tìm thấy đăng ký.");
        _scope.EnsureCanManage(r.Event.OrganizationId);
        await _scope.EnsureOrganizationActiveAsync(r.Event.OrganizationId, ct);
        if (r.Event.Status is EventStatus.Completed or EventStatus.Cancelled)
            throw new BusinessRuleException("BR-46", "Giải đã kết thúc hoặc đã hủy – không sửa đăng ký được.");

        var gender = EnumParser.ParseOptional<Gender>(request.Gender) ?? r.Gender;
        var idType = EnumParser.ParseOptional<IdType>(request.IdType);
        var idNumber = Clean(request.IdNumber);
        var age = RegistrationRules.AgeFor(request.DateOfBirth, r.Event);
        RegistrationRules.EnsureAgeAllowed(age, r.Category);
        ValidateIdentity(idType, idNumber, age);
        if (idType is IdType t && idNumber is not null && RegistrationRules.IsActive(r.Status) &&
            await _registrations.IdNumberHasActiveAsync(r.EventId, t, idNumber, r.RegistrationId, ct))
            throw new BusinessRuleException("BR-01", "Số giấy tờ này đã được dùng cho một đăng ký khác trong giải.");

        var old = new { r.FullName, r.DateOfBirth, Gender = r.Gender.ToString(), IdType = r.IdType?.ToString(), r.IdNumber };
        r.FullName = request.FullName.Trim();
        r.DateOfBirth = request.DateOfBirth;
        r.Gender = gender;
        r.IdType = idNumber is null ? null : idType;
        r.IdNumber = idNumber;
        // BR-36: sửa trường đã khóa phải ghi nhật ký kèm lý do
        await _audit.LogAsync("Registration.UpdateLockedInfo", nameof(Registration), registrationId.ToString(), old,
            new { r.FullName, r.DateOfBirth, Gender = r.Gender.ToString(), IdType = r.IdType?.ToString(), r.IdNumber, Reason = request.Reason.Trim() }, ct);
        await SaveAsync(ct);
    }

    // ============================ BR-13 ============================

    public async Task<int> ExpireOverdueAsync(CancellationToken ct = default)
    {
        var overdue = await _registrations.GetOverdueAsync(_clock.Now, 200, ct);
        var count = 0;
        // Mỗi giải một transaction + khóa – không giẫm lên thanh toán đang diễn ra cùng lúc
        foreach (var group in overdue.GroupBy(x => x.EventId))
        {
            await using var tx = await _uow.BeginTransactionAsync(ct);
            await _uow.LockAsync(RegistrationRules.Lock(group.Key), ct);
            var now = _clock.Now;
            foreach (var (registrationId, _) in group)
            {
                var r = await _registrations.GetTrackedAsync(registrationId, ct);
                if (r is null || r.Status != RegistrationStatus.PendingPayment || r.ExpiresAt is not DateTime exp || exp > now) continue;
                await ReleaseAsync(r, RegistrationStatus.Expired, ct);
                count++;
            }
            await _uow.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        return count;
    }

    // ============================ hỗ trợ ============================

    /// <summary>Đăng ký chờ thanh toán ⇒ Hủy / Hết hạn: trả suất (đổi trạng thái), trả lượt coupon, đóng thanh toán đang chờ.</summary>
    private async Task ReleaseAsync(Registration r, RegistrationStatus target, CancellationToken ct)
    {
        r.Status = target;
        r.CancelledAt = _clock.Now;
        r.ExpiresAt = null;
        if (r.CouponId is int couponId) await _coupons.ReleaseAsync(couponId, ct);
        foreach (var p in r.Payments.Where(p => p.Status == PaymentStatus.Pending)) p.Status = PaymentStatus.Expired;
    }

    private async Task<Registration> LoadOwnAsync(int registrationId, string userId, CancellationToken ct)
    {
        var r = await _registrations.GetTrackedAsync(registrationId, ct);
        if (r is null || r.RegisteredByUserId != userId) throw new NotFoundException("Không tìm thấy đăng ký.");
        return r;
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await _uow.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) { throw DbErrors.ToBusinessError(ex, "Người chạy hoặc số giấy tờ đã có đăng ký còn hiệu lực trong giải.", "BR-01"); }
    }

    private Task AddConsentAsync(string userId, int registrationId, ConsentType type, int version, bool granted, DateTime now,
        CancellationToken ct) =>
        _registrations.AddConsentAsync(new ConsentRecord
        {
            UserId = userId, RegistrationId = registrationId, ConsentType = type, DocumentVersion = version, Granted = granted, CreatedAt = now
        }, ct);

    private static Registration BuildSnapshot(CreateRegistrationRequest q) => new()
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

    /// <summary>BR-06: dưới tuổi giám hộ ⇒ bắt buộc thông tin + đồng ý của người giám hộ; dưới tuổi chạy kèm ⇒ khai người chạy kèm.</summary>
    private static void ValidateGuardianAndEscort(CreateRegistrationRequest q, int age, Event ev, RaceCategory category)
    {
        if (age < ev.Policy.GuardianConsentUnderAge &&
            (string.IsNullOrWhiteSpace(q.GuardianName) || string.IsNullOrWhiteSpace(q.GuardianPhone) || !q.GuardianConsent))
            throw new BusinessRuleException("BR-06",
                $"Người chạy dưới {ev.Policy.GuardianConsentUnderAge} tuổi – cần họ tên, SĐT và xác nhận đồng ý của người giám hộ.", 400);
        if (category.EscortRequiredUnderAge is int escortAge && age < escortAge &&
            (string.IsNullOrWhiteSpace(q.EscortName) || string.IsNullOrWhiteSpace(q.EscortPhone)))
            throw new BusinessRuleException("BR-06", $"Người chạy dưới {escortAge} tuổi ở cự ly {category.Name} – cần khai người chạy kèm.", 400);
    }

    /// <summary>BR-07: từ 14 tuổi bắt buộc số giấy tờ; có số thì phải có loại; CCCD 12 chữ số.</summary>
    private static void ValidateIdentity(IdType? idType, string? idNumber, int age)
    {
        if (idNumber is null)
        {
            if (age >= 14) throw new BusinessRuleException("BR-07", "Người chạy từ 14 tuổi phải khai số CCCD hoặc hộ chiếu.", 400);
            return;
        }
        if (idType is null) throw new BusinessRuleException("BR-07", "Vui lòng chọn loại giấy tờ.", 400);
        if (idType == IdType.CCCD && !CccdPattern.IsMatch(idNumber))
            throw new BusinessRuleException("BR-07", "Số CCCD gồm 12 chữ số.", 400);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MyRegistrationDto ToMyDto(Registration r, string userId, DateTime now) =>
        ToMyDto(r, r.Event, r.Category, r.Participant, userId, now);

    private static MyRegistrationDto ToMyDto(Registration r, Event ev, RaceCategory category, Participant participant,
        string userId, DateTime now)
    {
        var isOwner = r.RegisteredByUserId == userId;
        return new MyRegistrationDto
        {
            Id = r.RegistrationId,
            EventId = r.EventId,
            EventName = ev.Name,
            EventDate = ev.EventDate,
            CategoryId = r.CategoryId,
            CategoryName = category.Name,
            ParticipantId = r.ParticipantId,
            RunnerName = r.FullName,
            IsDependent = participant.Relationship != ParticipantRelationship.Self,
            Status = r.Status.ToString(),
            BibNumber = r.BibNumber,
            BibName = r.BibName,
            FinalPrice = r.FinalPrice,
            RegisteredAt = r.RegisteredAt,
            ExpiresAt = r.Status == RegistrationStatus.PendingPayment ? r.ExpiresAt : null,
            QrToken = r.Status == RegistrationStatus.Confirmed ? r.QrToken : null,
            CanEditInfo = isOwner && RegistrationRules.IsActive(r.Status) && now < RegistrationRules.InfoEditDeadline(ev),
            CanCancel = isOwner && r.Status == RegistrationStatus.PendingPayment,
            RaceKitCollected = r.RaceKitPickup is not null,
            TShirtSize = r.TShirtSize.ToString(),
            Phone = r.Phone,
            EmergencyContactName = r.EmergencyContactName,
            EmergencyContactPhone = r.EmergencyContactPhone,
            ClubName = r.ClubName,
            HideFromPublicList = r.HideFromPublicList
        };
    }
}
