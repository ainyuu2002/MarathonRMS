using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.Events;

// Phần A – UC-25 hoãn / hủy giải (BR-15), UC-50 hoàn tiền.

/// <summary>Hoãn giải: dời sang ngày mới, mọi mốc gắn với ngày thi dời theo cùng số ngày.</summary>
public class PostponeEventRequest
{
    [Required] public DateOnly NewEventDate { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập lý do hoãn"), StringLength(1000, MinimumLength = 10, ErrorMessage = "Lý do từ 10 đến 1000 ký tự")]
    public string Reason { get; set; } = "";
}

/// <summary>Hủy giải. Hoàn tiền nếu chính sách giải là Refund, hoặc BTC tự chọn hoàn tiền dù chính sách không bắt buộc.</summary>
public class CancelEventRequest
{
    [Required(ErrorMessage = "Vui lòng nhập lý do hủy"), StringLength(1000, MinimumLength = 10, ErrorMessage = "Lý do từ 10 đến 1000 ký tự")]
    public string Reason { get; set; } = "";
    public bool RefundPaidRegistrations { get; set; }
}

public class EventLifecycleResultDto
{
    public int EventId { get; set; }
    public string Status { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public int ShiftedDays { get; set; }
    public int CancelledUnpaid { get; set; }
    public int RefundsCreated { get; set; }
    public decimal RefundTotal { get; set; }
    public int CancelledWithoutRefund { get; set; }
}

public class RefundDto
{
    public int Id { get; set; }
    public int RegistrationId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string RunnerName { get; set; } = "";
    public int? BibNumber { get; set; }
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "";
    public string? BankInfo { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? TransactionRef { get; set; }
}

/// <summary>VĐV khai thông tin nhận tiền (ngân hàng, số tài khoản, chủ tài khoản).</summary>
public class UpdateRefundBankInfoRequest
{
    [Required(ErrorMessage = "Vui lòng nhập thông tin tài khoản nhận tiền"), StringLength(300, MinimumLength = 10)]
    public string BankInfo { get; set; } = "";
}

public class CompleteRefundRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mã giao dịch chuyển khoản"), StringLength(100, MinimumLength = 3)]
    public string TransactionRef { get; set; } = "";
}

public class RejectRefundRequest
{
    [Required(ErrorMessage = "Vui lòng nhập lý do"), StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = "";
}
