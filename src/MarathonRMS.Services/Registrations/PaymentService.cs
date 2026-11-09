using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Payments;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.Registrations;

/// <summary>
/// UC-41 – thanh toán giả lập (Mock): thành công ngay và xác nhận đăng ký. VNPay (UC-42) là GĐ2 – chưa hỗ trợ.
/// Phí chuyển nhượng / đổi cự ly / khiếu nại do nghiệp vụ tương ứng tạo thanh toán riêng.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly IRegistrationRepository _registrations;
    private readonly IPaymentRepository _payments;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemSettingsProvider _settings;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    public PaymentService(IRegistrationRepository registrations, IPaymentRepository payments, ICurrentUserService currentUser,
        ISystemSettingsProvider settings, IDateTimeProvider clock, IUnitOfWork uow)
    {
        _registrations = registrations;
        _payments = payments;
        _currentUser = currentUser;
        _settings = settings;
        _clock = clock;
        _uow = uow;
    }

    public async Task<PaymentResultDto> CreateAsync(CreatePaymentRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Chưa đăng nhập.");
        var purpose = EnumParser.ParseOptional<PaymentPurpose>(request.Purpose) ?? PaymentPurpose.Registration;
        var provider = EnumParser.ParseOptional<PaymentProvider>(request.Provider) ?? PaymentProvider.Mock;
        if (purpose != PaymentPurpose.Registration)
            throw new BusinessRuleException(ErrorCodes.Validation, "Khoản phí này được thanh toán ngay trong thao tác tương ứng.", 400);
        if (provider != PaymentProvider.Mock)
            throw new BusinessRuleException(ErrorCodes.Validation, "Cổng VNPay chưa được hỗ trợ – vui lòng dùng thanh toán thử (Mock).", 400);
        if (!await _settings.GetBoolAsync("Payment.MockEnabled", true, ct))
            throw new BusinessRuleException(ErrorCodes.Validation, "Thanh toán thử đang tắt.", 400);

        var probe = await _registrations.GetHeaderAsync(request.RegistrationId, ct);
        if (probe is null || probe.RegisteredByUserId != userId) throw new NotFoundException("Không tìm thấy đăng ký.");

        // Hai lần bấm "Thanh toán" cùng lúc chạy lần lượt; lần sau thấy đăng ký đã Confirmed
        await using var tx = await _uow.BeginTransactionAsync(ct);
        await _uow.LockAsync(RegistrationRules.Lock(probe.EventId), ct);
        var r = await _registrations.GetTrackedAsync(request.RegistrationId, ct) ?? throw new NotFoundException("Không tìm thấy đăng ký.");
        var now = _clock.Now;

        if (r.Status == RegistrationStatus.Confirmed)
            throw new BusinessRuleException(ErrorCodes.Conflict, "Đăng ký đã được thanh toán.");
        if (r.Status != RegistrationStatus.PendingPayment)
            throw new BusinessRuleException("BR-13", "Đăng ký không còn chờ thanh toán (đã hủy hoặc hết hạn giữ chỗ).");
        if (r.ExpiresAt is DateTime expires && now >= expires)
            throw new BusinessRuleException("BR-13", "Đã hết thời gian giữ chỗ – vui lòng đăng ký lại.");

        var payment = new Payment
        {
            RegistrationId = r.RegistrationId,
            Purpose = PaymentPurpose.Registration,
            Provider = PaymentProvider.Mock,
            Amount = r.FinalPrice,
            Status = PaymentStatus.Success,
            TxnRef = $"MRMS{r.RegistrationId}-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}",
            ProviderTransactionNo = "MOCK" + now.ToString("yyMMddHHmmssfff"),
            ResponseCode = "00",
            CreatedAt = now,
            PaidAt = now
        };
        await _payments.AddAsync(payment, ct);
        r.Status = RegistrationStatus.Confirmed;
        r.ConfirmedAt = now;
        r.ExpiresAt = null;
        await _uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new PaymentResultDto
        {
            PaymentId = payment.PaymentId,
            TxnRef = payment.TxnRef,
            Amount = payment.Amount,
            Provider = payment.Provider.ToString(),
            Status = payment.Status.ToString(),
            RegistrationStatus = r.Status.ToString()
        };
    }
}
