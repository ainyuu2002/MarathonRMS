using MarathonRMS.DTOs.Payments;
using MarathonRMS.DTOs.Registrations;

namespace MarathonRMS.Services.Registrations;

// Phần B – Đăng ký & thanh toán (Giai đoạn 1). Chỉ có interface; B viết implementation.

/// <summary>UC-39, UC-43, UC-44, UC-45, UC-48.</summary>
public interface IRegistrationService
{
    /// <summary>
    /// Tạo đăng ký trong MỘT transaction (UnitOfWork): BR-01 (1 đăng ký còn hiệu lực/người/giải, theo Participant và số giấy tờ),
    /// BR-02 (số người phụ thuộc), BR-03 (giải mở đăng ký), BR-04 (còn suất – concurrency qua RowVersion),
    /// BR-05/06 (tuổi, giám hộ, chạy kèm theo EventPolicy), BR-07 (snapshot), BR-09 (ConsentRecords),
    /// BR-10 (giá), BR-11 (coupon – GĐ2), BR-12 (giá 0 ⇒ Confirmed ngay), BR-13 (ExpiresAt theo phương thức).
    /// </summary>
    Task<MyRegistrationDto> CreateAsync(CreateRegistrationRequest request, CancellationToken ct = default);

    /// <summary>Đăng ký của tôi và của người phụ thuộc do tôi đăng ký.</summary>
    Task<IReadOnlyList<MyRegistrationDto>> GetMineAsync(CancellationToken ct = default);

    /// <summary>BR-08: chỉ trước InfoEditDeadline; chỉ người đã đăng ký (RegisteredByUserId).</summary>
    Task<MyRegistrationDto> UpdateInfoAsync(int registrationId, UpdateRegistrationInfoRequest request, CancellationToken ct = default);

    /// <summary>BR-14: chỉ hủy được đăng ký PendingPayment.</summary>
    Task CancelAsync(int registrationId, CancellationToken ct = default);

    /// <summary>Nguồn cho GET /odata/Registrations – chỉ đăng ký thuộc giải của BTC người gọi (BR-30).</summary>
    IQueryable<RegistrationListItemDto> QueryForOrganizer();

    /// <summary>BR-08: Organizer sửa trường đã khóa, ghi IAuditLogger (BR-36).</summary>
    Task UpdateLockedInfoAsync(int registrationId, UpdateLockedInfoRequest request, CancellationToken ct = default);

    /// <summary>Gọi bởi RegistrationExpiryWorker: chuyển PendingPayment quá hạn sang Expired, trả suất/coupon (BR-13). Trả số đăng ký đã xử lý.</summary>
    Task<int> ExpireOverdueAsync(CancellationToken ct = default);
}

/// <summary>UC-41 (Mock), UC-42 (VNPay – GĐ2).</summary>
public interface IPaymentService
{
    /// <summary>Tạo Payment; Mock ⇒ Success ngay và áp dụng theo Purpose (đăng ký ⇒ Confirmed). Chống xử lý trùng theo TxnRef.</summary>
    Task<PaymentResultDto> CreateAsync(CreatePaymentRequest request, CancellationToken ct = default);
}
