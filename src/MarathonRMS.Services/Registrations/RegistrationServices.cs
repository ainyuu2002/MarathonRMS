using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Payments;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using MarathonRMS.Services.Coupons;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.Registrations;

public class RegistrationService : IRegistrationService
{
    private readonly IRegistrationRepository _registrations;
    private readonly IParticipantRepository _participants;
    private readonly IConsentRecordRepository _consents;
    private readonly ICouponService _coupons;
    private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemSettingsProvider _settings;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public RegistrationService(IRegistrationRepository registrations, IParticipantRepository participants,
        IConsentRecordRepository consents, ICouponService coupons, IOrganizationScope scope,
        ICurrentUserService currentUser, ISystemSettingsProvider settings, IDateTimeProvider clock,
        IAuditLogger audit, IUnitOfWork uow)
    {
        _registrations = registrations;
        _participants = participants;
        _consents = consents;
        _coupons = coupons;
        _scope = scope;
        _currentUser = currentUser;
        _settings = settings;
        _clock = clock;
        _audit = audit;
        _uow = uow;
    }

    public async Task<MyRegistrationDto> CreateAsync(CreateRegistrationRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Bạn cần đăng nhập để đăng ký.");
        var now = _clock.Now;

        await using var tx = await _uow.BeginTransactionAsync(ct);
        if (!await _uow.TryLockAsync($"mrms:registration-category:{request.CategoryId}", ct))
            throw new BusinessRuleException("BR-04", "Cự ly đang có nhiều người đăng ký. Vui lòng thử lại.");

        var category = await _registrations.GetCategoryForRegistrationAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Không tìm thấy cự ly.");
        var ev = category.Event;
        await _scope.EnsureOrganizationActiveAsync(ev.OrganizationId, ct);

        if (ev.Status != EventStatus.RegistrationOpen || now < ev.RegistrationOpenAt || now >= ev.RegistrationCloseAt)
            throw new BusinessRuleException("BR-03", "Giải hiện không mở đăng ký.");

        var participant = await _participants.GetOwnedAsync(request.ParticipantId, userId, ct)
            ?? throw new ForbiddenException("Hồ sơ người chạy không thuộc tài khoản của bạn.");
        if (participant.Relationship != ParticipantRelationship.Self)
            throw new BusinessRuleException("UC-39", "Đợt này chỉ hỗ trợ đăng ký chính chủ.", 400);

        var age = ev.Policy.AgeCalculationMode == AgeCalculationMode.CalendarYear
            ? ev.EventDate.Year - request.DateOfBirth.Year
            : AgeCalculator.AgeAt(request.DateOfBirth, ev.EventDate);
        if (age < category.MinAge || category.MaxAge is int maxAge && age > maxAge)
            throw new BusinessRuleException("BR-05", "Người chạy không nằm trong độ tuổi của cự ly này.");
        if (age >= 14 && (string.IsNullOrWhiteSpace(request.IdNumber) || string.IsNullOrWhiteSpace(request.IdType)))
            throw new BusinessRuleException("BR-05", "Người chạy từ 14 tuổi phải có loại và số giấy tờ.", 400);

        var needsGuardian = age < ev.Policy.GuardianConsentUnderAge;
        if (needsGuardian && (string.IsNullOrWhiteSpace(request.GuardianName)
                              || string.IsNullOrWhiteSpace(request.GuardianPhone) || !request.GuardianConsent))
            throw new BusinessRuleException("BR-06", "Người chạy chưa đủ tuổi cần thông tin và xác nhận của người giám hộ.", 400);
        var needsEscort = category.EscortRequiredUnderAge is int escortAge && age < escortAge;
        if (needsEscort && (string.IsNullOrWhiteSpace(request.EscortName) || string.IsNullOrWhiteSpace(request.EscortPhone)))
            throw new BusinessRuleException("BR-06", "Cự ly yêu cầu thông tin người chạy kèm.", 400);
        if ((!string.IsNullOrWhiteSpace(request.BloodType) || !string.IsNullOrWhiteSpace(request.MedicalNotes))
            && !request.HealthDataConsent)
            throw new BusinessRuleException("BR-09", "Bạn phải đồng ý xử lý dữ liệu sức khỏe đã cung cấp.", 400);
        if (!request.WaiverAccepted || !request.HealthDeclarationAccepted)
            throw new BusinessRuleException("BR-09", "Bạn phải chấp nhận miễn trừ trách nhiệm và cam kết sức khỏe.", 400);

        var idType = ParseOptional<IdType>(request.IdType, "loại giấy tờ");
        var idNumber = Clean(request.IdNumber)?.ToUpperInvariant();
        IdentityRules.Validate(idType, idNumber);
        if (await _registrations.HasActiveForParticipantAsync(ev.EventId, participant.ParticipantId, ct)
            || await _registrations.HasActiveForIdAsync(ev.EventId, idType, idNumber, ct))
            throw new BusinessRuleException("BR-01", "Người chạy đã có đăng ký còn hiệu lực tại giải này.");
        if (await _registrations.CountActiveAsync(category.CategoryId, ct) >= category.Capacity)
            throw new BusinessRuleException("BR-04", "Cự ly đã hết suất.");

        var tier = category.PriceTiers.SingleOrDefault(t => t.ValidFrom <= now && now < t.ValidTo)
            ?? throw new BusinessRuleException("BR-10", "Cự ly hiện không có mức giá hợp lệ.");
        var originalPrice = tier.Price;
        var discount = 0m;
        int? couponId = null;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var quote = await _coupons.QuoteAsync(request.CouponCode, category.CategoryId, originalPrice, userId, ct);
            discount = quote.DiscountAmount;
            couponId = quote.CouponId;
            await _coupons.RedeemAsync(quote.CouponId, ct);
        }

        var finalPrice = originalPrice - discount;
        var holdMinutes = Math.Max(1, await _settings.GetIntAsync("Payment.HoldMinutes.Mock", 15, ct));
        var registration = new Registration
        {
            EventId = ev.EventId,
            CategoryId = category.CategoryId,
            ParticipantId = participant.ParticipantId,
            RegisteredByUserId = userId,
            EntryType = EntryType.Public,
            Status = finalPrice == 0 ? RegistrationStatus.Confirmed : RegistrationStatus.PendingPayment,
            OriginalPrice = originalPrice,
            DiscountAmount = discount,
            CoinDiscountAmount = 0,
            FinalPrice = finalPrice,
            CouponId = couponId,
            QrToken = Guid.NewGuid().ToString("N"),
            RegisteredAt = now,
            ExpiresAt = finalPrice == 0 ? null : now.AddMinutes(holdMinutes),
            ConfirmedAt = finalPrice == 0 ? now : null,
            HideFromPublicList = request.HideFromPublicList,
            FullName = request.FullName.Trim(),
            BibName = request.BibName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Gender = ParseRequired<Gender>(request.Gender, "giới tính"),
            IdType = idType,
            IdNumber = idNumber,
            Nationality = request.Nationality.Trim(),
            Phone = request.Phone.Trim(),
            Email = Clean(request.Email),
            TShirtSize = ParseRequired<TShirtSize>(request.TShirtSize, "cỡ áo"),
            EmergencyContactName = request.EmergencyContactName.Trim(),
            EmergencyContactPhone = request.EmergencyContactPhone.Trim(),
            BloodType = Clean(request.BloodType),
            MedicalNotes = Clean(request.MedicalNotes),
            ClubName = Clean(request.ClubName),
            GuardianName = needsGuardian ? Clean(request.GuardianName) : null,
            GuardianPhone = needsGuardian ? Clean(request.GuardianPhone) : null,
            GuardianIdNumber = needsGuardian ? Clean(request.GuardianIdNumber) : null,
            GuardianConsent = needsGuardian && request.GuardianConsent,
            EscortName = needsEscort ? Clean(request.EscortName) : null,
            EscortPhone = needsEscort ? Clean(request.EscortPhone) : null,
            WaiverAccepted = true,
            HealthDeclarationAccepted = true
        };

        try
        {
            await _registrations.AddAsync(registration, ct);
            await _uow.SaveChangesAsync(ct);
            foreach (var consent in BuildConsents(registration, ev.WaiverVersion, request, needsGuardian, now))
                await _consents.AddAsync(consent, ct);
            await _audit.LogAsync("Registration.Create", nameof(Registration), registration.RegistrationId.ToString(), null,
                new { registration.EventId, registration.CategoryId, registration.ParticipantId, registration.Status, registration.FinalPrice }, ct);
            await _uow.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessRuleException("BR-11", "Mã giảm giá vừa hết lượt. Vui lòng chọn mã khác.");
        }
        catch (DbUpdateException)
        {
            throw new BusinessRuleException("BR-01", "Người chạy đã có đăng ký còn hiệu lực tại giải này.");
        }

        registration.Event = ev;
        registration.Category = category;
        registration.Participant = participant;
        return ToMineDto(registration, now);
    }

    public async Task<IReadOnlyList<MyRegistrationDto>> GetMineAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Bạn cần đăng nhập.");
        var rows = await _registrations.GetMineAsync(userId, ct);
        return rows.Select(r => ToMineDto(r, _clock.Now)).ToList();
    }

    public async Task<MyRegistrationDto> UpdateInfoAsync(int registrationId, UpdateRegistrationInfoRequest request, CancellationToken ct = default)
    {
        await using var tx = await _uow.BeginTransactionAsync(ct);
        if (!await _uow.TryLockAsync($"mrms:payment-registration:{registrationId}", ct))
            throw new BusinessRuleException("BR-08", "Đăng ký đang được xử lý. Vui lòng thử lại.");
        var registration = await LoadOwnedAsync(registrationId, ct);
        var deadline = registration.Event.Policy.InfoEditDeadline ?? registration.Event.RegistrationCloseAt;
        if (_clock.Now >= deadline || registration.Status is not (RegistrationStatus.PendingPayment or RegistrationStatus.Confirmed))
            throw new BusinessRuleException("BR-08", "Đã quá hạn sửa thông tin đăng ký.");
        registration.BibName = request.BibName.Trim();
        registration.TShirtSize = ParseRequired<TShirtSize>(request.TShirtSize, "cỡ áo");
        registration.Phone = request.Phone.Trim();
        registration.EmergencyContactName = request.EmergencyContactName.Trim();
        registration.EmergencyContactPhone = request.EmergencyContactPhone.Trim();
        registration.ClubName = Clean(request.ClubName);
        registration.HideFromPublicList = request.HideFromPublicList;
        await _consents.AddAsync(new ConsentRecord
        {
            UserId = registration.RegisteredByUserId, RegistrationId = registrationId,
            ConsentType = ConsentType.PublicListing, DocumentVersion = registration.Event.WaiverVersion,
            Granted = !request.HideFromPublicList, CreatedAt = _clock.Now
        }, ct);
        await _audit.LogAsync("Registration.UpdateInfo", nameof(Registration), registrationId.ToString(), null, request, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToMineDto(registration, _clock.Now);
    }

    public async Task CancelAsync(int registrationId, CancellationToken ct = default)
    {
        await using var tx = await _uow.BeginTransactionAsync(ct);
        if (!await _uow.TryLockAsync($"mrms:payment-registration:{registrationId}", ct))
            throw new BusinessRuleException("BR-14", "Đăng ký đang được xử lý. Vui lòng thử lại.");
        var registration = await LoadOwnedAsync(registrationId, ct);
        if (registration.Status != RegistrationStatus.PendingPayment)
            throw new BusinessRuleException("BR-14", "Chỉ có thể hủy đăng ký đang chờ thanh toán.");
        registration.Status = RegistrationStatus.Cancelled;
        registration.CancelledAt = _clock.Now;
        registration.ExpiresAt = null;
        if (registration.CouponId is int couponId) await _coupons.ReleaseAsync(couponId, ct);
        await _audit.LogAsync("Registration.Cancel", nameof(Registration), registrationId.ToString(), null, new { registration.Status }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public IQueryable<RegistrationListItemDto> QueryForOrganizer()
    {
        var organizationId = _scope.RequireOrganizationId();
        return _registrations.QueryWithDetails().Where(r => r.Event.OrganizationId == organizationId)
            .Select(r => new RegistrationListItemDto
            {
                Id = r.RegistrationId, EventId = r.EventId, CategoryId = r.CategoryId, CategoryName = r.Category.Name,
                FullName = r.FullName, BibName = r.BibName, BibNumber = r.BibNumber,
                Gender = r.Gender == Gender.M ? "M" : "F",
                DateOfBirth = r.DateOfBirth,
                IdType = r.IdType == null ? null
                    : r.IdType == IdType.CCCD ? "CCCD"
                    : r.IdType == IdType.Passport ? "Passport" : "BirthCertificate",
                IdNumber = r.IdNumber, Nationality = r.Nationality, Phone = r.Phone, Email = r.Email,
                TShirtSize = r.TShirtSize == TShirtSize.XS ? "XS"
                    : r.TShirtSize == TShirtSize.S ? "S"
                    : r.TShirtSize == TShirtSize.M ? "M"
                    : r.TShirtSize == TShirtSize.L ? "L"
                    : r.TShirtSize == TShirtSize.XL ? "XL" : "XXL",
                ClubName = r.ClubName,
                EntryType = r.EntryType == EntryType.Public ? "Public"
                    : r.EntryType == EntryType.Partner ? "Partner"
                    : r.EntryType == EntryType.Complimentary ? "Complimentary"
                    : r.EntryType == EntryType.Pacer ? "Pacer" : "Charity",
                Status = r.Status == RegistrationStatus.PendingPayment ? "PendingPayment"
                    : r.Status == RegistrationStatus.Confirmed ? "Confirmed"
                    : r.Status == RegistrationStatus.Cancelled ? "Cancelled"
                    : r.Status == RegistrationStatus.Expired ? "Expired"
                    : r.Status == RegistrationStatus.Transferred ? "Transferred"
                    : r.Status == RegistrationStatus.RefundPending ? "RefundPending" : "Refunded",
                FinalPrice = r.FinalPrice, RegisteredAt = r.RegisteredAt,
                RaceKitCollected = r.RaceKitPickup != null
            });
    }

    public async Task UpdateLockedInfoAsync(int registrationId, UpdateLockedInfoRequest request, CancellationToken ct = default)
    {
        await using var tx = await _uow.BeginTransactionAsync(ct);
        if (!await _uow.TryLockAsync($"mrms:registration-locked-info:{registrationId}", ct))
            throw new BusinessRuleException("BR-08", "Đăng ký đang được sửa. Vui lòng thử lại.");
        var registration = await _registrations.GetTrackedWithDetailsAsync(registrationId, ct)
            ?? throw new NotFoundException("Không tìm thấy đăng ký.");
        _scope.EnsureCanManage(registration.Event.OrganizationId);
        // BR-32: Organizer không tự sửa họ tên / ngày sinh / giấy tờ trên đăng ký của chính mình (ngày sinh quyết định nhóm tuổi)
        _scope.EnsureNotOwnRegistration(registration.Participant.LinkedUserId, registration.RegisteredByUserId);
        await _scope.EnsureOrganizationActiveAsync(registration.Event.OrganizationId, ct);
        if (registration.Status is RegistrationStatus.Cancelled or RegistrationStatus.Expired or RegistrationStatus.Transferred
            or RegistrationStatus.Refunded)
            throw new BusinessRuleException("BR-08", "Không thể sửa đăng ký không còn hiệu lực.");

        var gender = ParseRequired<Gender>(request.Gender, "giới tính");
        var idType = ParseOptional<IdType>(request.IdType, "loại giấy tờ");
        var idNumber = Clean(request.IdNumber)?.ToUpperInvariant();
        IdentityRules.Validate(idType, idNumber);
        var age = registration.Event.Policy.AgeCalculationMode == AgeCalculationMode.CalendarYear
            ? registration.Event.EventDate.Year - request.DateOfBirth.Year
            : AgeCalculator.AgeAt(request.DateOfBirth, registration.Event.EventDate);
        if (age < registration.Category.MinAge || registration.Category.MaxAge is int maxAge && age > maxAge)
            throw new BusinessRuleException("BR-05", "Ngày sinh mới không đáp ứng độ tuổi của cự ly.");
        if (age >= 14 && (idType is null || idNumber is null))
            throw new BusinessRuleException("BR-05", "Người chạy từ 14 tuổi phải có loại và số giấy tờ.", 400);
        if (age < registration.Event.Policy.GuardianConsentUnderAge
            && (string.IsNullOrWhiteSpace(registration.GuardianName) || !registration.GuardianConsent))
            throw new BusinessRuleException("BR-06", "Ngày sinh mới yêu cầu xác nhận người giám hộ.");
        if (registration.Category.EscortRequiredUnderAge is int escortAge && age < escortAge
            && string.IsNullOrWhiteSpace(registration.EscortName))
            throw new BusinessRuleException("BR-06", "Ngày sinh mới yêu cầu người chạy kèm.");
        if (await _registrations.HasActiveForIdExceptAsync(registration.EventId, idType, idNumber, registrationId, ct))
            throw new BusinessRuleException("BR-01", "Số giấy tờ đã có đăng ký còn hiệu lực tại giải này.");

        var old = new { registration.FullName, registration.DateOfBirth, registration.Gender, registration.IdType, registration.IdNumber };
        registration.FullName = request.FullName.Trim();
        registration.DateOfBirth = request.DateOfBirth;
        registration.Gender = gender;
        registration.IdType = idType;
        registration.IdNumber = idNumber;
        await _audit.LogAsync("Registration.UpdateLockedInfo", nameof(Registration), registrationId.ToString(), old,
            new { request.FullName, request.DateOfBirth, request.Gender, request.IdType, request.IdNumber, request.Reason }, ct);
        try { await _uow.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            throw new BusinessRuleException("BR-01", "Số giấy tờ đã có đăng ký còn hiệu lực tại giải này.");
        }
        await tx.CommitAsync(ct);
    }

    public async Task<int> ExpireOverdueAsync(CancellationToken ct = default)
    {
        var now = _clock.Now;
        var ids = await _registrations.GetOverdueIdsAsync(now, ct);
        var count = 0;
        foreach (var id in ids)
        {
            await using var tx = await _uow.BeginTransactionAsync(ct);
            if (!await _uow.TryLockAsync($"mrms:payment-registration:{id}", ct))
                continue;
            // Nạp lại sau khi lấy cùng khóa với PaymentService để không hết hạn một thanh toán vừa thành công.
            var registration = await _registrations.GetTrackedWithDetailsAsync(id, ct);
            if (registration is null || registration.Status != RegistrationStatus.PendingPayment
                || registration.ExpiresAt is null || registration.ExpiresAt > now)
            {
                await tx.CommitAsync(ct);
                continue;
            }
            registration.Status = RegistrationStatus.Expired;
            registration.ExpiresAt = null;
            if (registration.CouponId is int couponId) await _coupons.ReleaseAsync(couponId, ct);
            await _uow.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            count++;
        }
        return count;
    }

    private async Task<Registration> LoadOwnedAsync(int id, CancellationToken ct)
    {
        var row = await _registrations.GetTrackedWithDetailsAsync(id, ct) ?? throw new NotFoundException("Không tìm thấy đăng ký.");
        if (row.RegisteredByUserId != _currentUser.UserId) throw new ForbiddenException("Đăng ký không thuộc tài khoản của bạn.");
        return row;
    }

    private static IEnumerable<ConsentRecord> BuildConsents(Registration r, int waiverVersion,
        CreateRegistrationRequest request, bool needsGuardian, DateTime now)
    {
        yield return New(ConsentType.Waiver, true);
        yield return New(ConsentType.HealthData, request.HealthDataConsent);
        yield return New(ConsentType.PhotoMarketing, request.PhotoMarketingConsent);
        yield return New(ConsentType.PublicListing, !request.HideFromPublicList);
        if (needsGuardian) yield return New(ConsentType.Guardian, request.GuardianConsent);

        ConsentRecord New(ConsentType type, bool granted) => new()
        {
            UserId = r.RegisteredByUserId, RegistrationId = r.RegistrationId, ConsentType = type,
            DocumentVersion = waiverVersion, Granted = granted, CreatedAt = now
        };
    }

    private static MyRegistrationDto ToMineDto(Registration r, DateTime now)
    {
        var deadline = r.Event.Policy.InfoEditDeadline ?? r.Event.RegistrationCloseAt;
        return new MyRegistrationDto
        {
            Id = r.RegistrationId, EventId = r.EventId, EventName = r.Event.Name, EventDate = r.Event.EventDate,
            CategoryId = r.CategoryId, CategoryName = r.Category.Name, ParticipantId = r.ParticipantId,
            RunnerName = r.FullName, IsDependent = r.Participant.Relationship != ParticipantRelationship.Self,
            Status = r.Status.ToString(), BibNumber = r.BibNumber, BibName = r.BibName,
            TShirtSize = r.TShirtSize.ToString(), Phone = r.Phone,
            EmergencyContactName = r.EmergencyContactName, EmergencyContactPhone = r.EmergencyContactPhone,
            ClubName = r.ClubName, HideFromPublicList = r.HideFromPublicList,
            OriginalPrice = r.OriginalPrice, DiscountAmount = r.DiscountAmount, FinalPrice = r.FinalPrice,
            RegisteredAt = r.RegisteredAt, ExpiresAt = r.ExpiresAt,
            QrToken = r.Status == RegistrationStatus.Confirmed ? r.QrToken : null,
            CanEditInfo = now < deadline && r.Status is RegistrationStatus.PendingPayment or RegistrationStatus.Confirmed,
            CanCancel = r.Status == RegistrationStatus.PendingPayment && r.ExpiresAt > now,
            RaceKitCollected = r.RaceKitPickup is not null
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static T ParseRequired<T>(string value, string label) where T : struct, Enum =>
        Enum.TryParse<T>(value, true, out var parsed) ? parsed : throw new BusinessRuleException("VALIDATION", $"{label} không hợp lệ.", 400);
    private static T? ParseOptional<T>(string? value, string label) where T : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null : ParseRequired<T>(value, label);
}

public class PaymentService : IPaymentService
{
    private readonly IRegistrationRepository _registrations;
    private readonly IPaymentRepository _payments;
    private readonly ICouponService _coupons;
    private readonly ISystemSettingsProvider _settings;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly IAuditLogger _audit;
    private readonly IUnitOfWork _uow;

    public PaymentService(IRegistrationRepository registrations, IPaymentRepository payments, ICouponService coupons,
        ISystemSettingsProvider settings, ICurrentUserService currentUser, IDateTimeProvider clock,
        IAuditLogger audit, IUnitOfWork uow)
    {
        _registrations = registrations; _payments = payments; _coupons = coupons; _settings = settings;
        _currentUser = currentUser; _clock = clock; _audit = audit; _uow = uow;
    }

    public async Task<PaymentResultDto> CreateAsync(CreatePaymentRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<PaymentPurpose>(request.Purpose, true, out var purpose) || purpose != PaymentPurpose.Registration)
            throw new BusinessRuleException("UC-41", "Đợt này chỉ hỗ trợ thanh toán phí đăng ký.", 400);
        if (!Enum.TryParse<PaymentProvider>(request.Provider, true, out var provider) || provider != PaymentProvider.Mock)
            throw new BusinessRuleException("UC-41", "Đợt này chỉ hỗ trợ cổng thanh toán Mock.", 400);
        if (!await _settings.GetBoolAsync("Payment.MockEnabled", true, ct))
            throw new BusinessRuleException("UC-41", "Thanh toán Mock đang tạm tắt.");

        await using var tx = await _uow.BeginTransactionAsync(ct);
        if (!await _uow.TryLockAsync($"mrms:payment-registration:{request.RegistrationId}", ct))
            throw new BusinessRuleException("BR-12", "Thanh toán đang được xử lý. Vui lòng thử lại.");
        var registration = await _registrations.GetTrackedWithDetailsAsync(request.RegistrationId, ct)
            ?? throw new NotFoundException("Không tìm thấy đăng ký.");
        if (registration.RegisteredByUserId != _currentUser.UserId)
            throw new ForbiddenException("Đăng ký không thuộc tài khoản của bạn.");

        var now = _clock.Now;
        if (registration.Status == RegistrationStatus.PendingPayment && registration.ExpiresAt <= now)
        {
            registration.Status = RegistrationStatus.Expired;
            registration.ExpiresAt = null;
            if (registration.CouponId is int couponId) await _coupons.ReleaseAsync(couponId, ct);
            await _uow.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw new BusinessRuleException("BR-13", "Đăng ký đã hết hạn giữ chỗ. Vui lòng đăng ký lại.");
        }
        if (registration.Status != RegistrationStatus.PendingPayment || registration.FinalPrice <= 0)
            throw new BusinessRuleException("BR-12", "Đăng ký không ở trạng thái chờ thanh toán.");
        if (await _payments.HasSuccessfulAsync(registration.RegistrationId, purpose, ct))
            throw new BusinessRuleException("BR-12", "Đăng ký đã được thanh toán.");

        var payment = new Payment
        {
            RegistrationId = registration.RegistrationId, Purpose = purpose, Provider = provider,
            Amount = registration.FinalPrice, Status = PaymentStatus.Success,
            TxnRef = $"MOCK-{Guid.NewGuid():N}", ProviderTransactionNo = $"MOCK{now:yyyyMMddHHmmssfff}",
            ResponseCode = "00", CreatedAt = now, PaidAt = now
        };
        await _payments.AddAsync(payment, ct);
        registration.Status = RegistrationStatus.Confirmed;
        registration.ConfirmedAt = now;
        registration.ExpiresAt = null;
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync("Payment.MockSuccess", nameof(Payment), payment.PaymentId.ToString(), null,
            new { payment.RegistrationId, payment.Amount, payment.TxnRef }, ct);
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new PaymentResultDto
        {
            PaymentId = payment.PaymentId, TxnRef = payment.TxnRef, Amount = payment.Amount,
            Provider = payment.Provider.ToString(), Status = payment.Status.ToString(),
            RegistrationStatus = registration.Status.ToString()
        };
    }
}
