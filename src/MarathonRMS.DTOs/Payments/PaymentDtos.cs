using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.Payments;

/// <summary>UC-41 / UC-42: tạo thanh toán cho một đăng ký. Đơn 0đ không cần gọi – đã Confirmed ngay (BR-12).</summary>
public class CreatePaymentRequest
{
    [Required] public int RegistrationId { get; set; }
    [Required, AllowedValues("Registration", "CategoryChange", "TransferFee", "ComplaintFee")]
    public string Purpose { get; set; } = "Registration";
    [Required, AllowedValues("Mock", "VnPay")] public string Provider { get; set; } = "Mock";
}

public class PaymentResultDto
{
    public int PaymentId { get; set; }
    public string TxnRef { get; set; } = "";
    public decimal Amount { get; set; }
    public string Provider { get; set; } = "";
    public string Status { get; set; } = "";
    /// <summary>Chỉ có với VNPay: URL chuyển hướng sang cổng thanh toán.</summary>
    public string? PaymentUrl { get; set; }
    /// <summary>Trạng thái đăng ký sau khi xử lý (Mock thành công ⇒ Confirmed).</summary>
    public string RegistrationStatus { get; set; } = "";
}
