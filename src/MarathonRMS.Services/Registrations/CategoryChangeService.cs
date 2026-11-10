using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.Registrations;

public class CategoryChangeService : ICategoryChangeService
{
    private readonly IRegistrationRepository _registrations; private readonly IPaymentRepository _payments;
    private readonly ICurrentUserService _user; private readonly ISystemSettingsProvider _settings;
    private readonly IDateTimeProvider _clock; private readonly IAuditLogger _audit; private readonly IUnitOfWork _uow;
    public CategoryChangeService(IRegistrationRepository registrations, IPaymentRepository payments,
        ICurrentUserService user, ISystemSettingsProvider settings, IDateTimeProvider clock, IAuditLogger audit, IUnitOfWork uow)
    { _registrations = registrations; _payments = payments; _user = user; _settings = settings; _clock = clock; _audit = audit; _uow = uow; }

    public async Task<CategoryChangeResultDto> ChangeAsync(int registrationId, ChangeCategoryRequest request, CancellationToken ct = default)
    {
        var userId = _user.UserId ?? throw new ForbiddenException("Bạn cần đăng nhập.");
        if (!request.Provider.Equals("Mock", StringComparison.OrdinalIgnoreCase) || !await _settings.GetBoolAsync("Payment.MockEnabled", true, ct))
            throw new BusinessRuleException("PAYMENT", "Phương thức thanh toán chưa được hỗ trợ.", 400);
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync($"mrms:registration-change:{registrationId}", ct);
        var r = await _registrations.GetTrackedWithDetailsAsync(registrationId, ct) ?? throw new NotFoundException("Không tìm thấy đăng ký.");
        if (r.RegisteredByUserId != userId) throw new ForbiddenException("Đăng ký không thuộc tài khoản của bạn.");
        var now = _clock.Now; var policy = r.Event.Policy;
        if (r.Status != RegistrationStatus.Confirmed) throw new BusinessRuleException("BR-17", "Chỉ đăng ký đã xác nhận mới được đổi cự ly.");
        if (r.EntryType != EntryType.Public && policy.LockSpecialEntries) throw new BusinessRuleException("BR-18", "Suất đặc biệt đã bị khóa thay đổi.");
        if (policy.CategoryChangeDeadline is null || now >= policy.CategoryChangeDeadline || r.ChangeCount >= policy.MaxCategoryChanges)
            throw new BusinessRuleException("BR-17", "Đã quá hạn hoặc hết số lần đổi cự ly.");
        var target = await _registrations.GetCategoryForRegistrationAsync(request.TargetCategoryId, ct) ?? throw new NotFoundException("Không tìm thấy cự ly mới.");
        if (target.EventId != r.EventId || target.CategoryId == r.CategoryId) throw new BusinessRuleException("BR-17", "Cự ly mới không hợp lệ.", 400);
        await _uow.LockAsync($"mrms:registration-category:{target.CategoryId}", ct);
        if (await _registrations.CountActiveAsync(target.CategoryId, ct) >= target.Capacity) throw new BusinessRuleException("BR-04", "Cự ly mới đã hết suất.");
        var age = r.Event.Policy.AgeCalculationMode == AgeCalculationMode.CalendarYear ? r.Event.EventDate.Year - r.DateOfBirth.Year : AgeCalculator.AgeAt(r.DateOfBirth, r.Event.EventDate);
        if (age < target.MinAge || target.MaxAge is int max && age > max) throw new BusinessRuleException("BR-05", "Người chạy không đủ điều kiện tuổi của cự ly mới.");
        if (age < r.Event.Policy.GuardianConsentUnderAge && !r.GuardianConsent || target.EscortRequiredUnderAge is int escort && age < escort && string.IsNullOrWhiteSpace(r.EscortName))
            throw new BusinessRuleException("BR-06", "Đăng ký thiếu thông tin giám hộ/chạy kèm cho cự ly mới.");
        var oldTier = r.Category.PriceTiers.SingleOrDefault(x => x.ValidFrom <= now && now < x.ValidTo) ?? throw new BusinessRuleException("BR-10", "Cự ly cũ không có mức giá hiện hành.");
        var newTier = target.PriceTiers.SingleOrDefault(x => x.ValidFrom <= now && now < x.ValidTo) ?? throw new BusinessRuleException("BR-10", "Cự ly mới không có mức giá hiện hành.");
        var difference = Math.Max(0, newTier.Price - oldTier.Price); var amount = difference + policy.CategoryChangeFee; int? paymentId = null;
        if (amount > 0)
        {
            var payment = new Payment { RegistrationId = r.RegistrationId, Purpose = PaymentPurpose.CategoryChange, Provider = PaymentProvider.Mock, Amount = amount, Status = PaymentStatus.Success, TxnRef = $"MOCK-CC-{Guid.NewGuid():N}", ProviderTransactionNo = Guid.NewGuid().ToString("N"), ResponseCode = "00", CreatedAt = now, PaidAt = now };
            await _payments.AddAsync(payment, ct); await _uow.SaveChangesAsync(ct); paymentId = payment.PaymentId;
        }
        var oldId = r.CategoryId; var oldName = r.Category.Name;
        r.CategoryId = target.CategoryId; r.Category = target; r.BibNumber = null; r.ChangeCount++;
        await _audit.LogAsync("Registration.ChangeCategory", nameof(Registration), r.RegistrationId.ToString(), new { CategoryId = oldId }, new { r.CategoryId, Charged = amount }, ct);
        await _uow.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new CategoryChangeResultDto { RegistrationId = r.RegistrationId, OldCategoryId = oldId, OldCategoryName = oldName, NewCategoryId = target.CategoryId, NewCategoryName = target.Name, PriceDifference = difference, ChangeFee = policy.CategoryChangeFee, ChargedAmount = amount, PaymentId = paymentId, ChangeCount = r.ChangeCount };
    }
}
