using System.ComponentModel.DataAnnotations;

namespace MarathonRMS.DTOs.Registrations;

public class ChangeCategoryRequest
{
    [Required] public int TargetCategoryId { get; set; }
    [Required, AllowedValues("Mock")] public string Provider { get; set; } = "Mock";
}

public class CategoryChangeResultDto
{
    public int RegistrationId { get; set; }
    public int OldCategoryId { get; set; }
    public string OldCategoryName { get; set; } = "";
    public int NewCategoryId { get; set; }
    public string NewCategoryName { get; set; } = "";
    public decimal PriceDifference { get; set; }
    public decimal ChangeFee { get; set; }
    public decimal ChargedAmount { get; set; }
    public int? PaymentId { get; set; }
    public int ChangeCount { get; set; }
}

public class CreateTransferRequest
{
    [Required, EmailAddress] public string RecipientEmail { get; set; } = "";
}

public class AcceptTransferRequest
{
    [Required, AllowedValues("Mock")] public string Provider { get; set; } = "Mock";
    [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải đồng ý văn bản miễn trừ trách nhiệm")]
    public bool WaiverAccepted { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "Bạn phải cam kết đủ sức khỏe tham gia")]
    public bool HealthDeclarationAccepted { get; set; }
    public bool HealthDataConsent { get; set; }
    public bool PhotoMarketingConsent { get; set; }
    public bool HideFromPublicList { get; set; }
}

public class TransferDto
{
    public int Id { get; set; }
    public int RegistrationId { get; set; }
    public string EventName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public int? BibNumber { get; set; }
    public string FromEmail { get; set; } = "";
    public string ToEmail { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal FeeAmount { get; set; }
    public int? NewRegistrationId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public bool CanAccept { get; set; }
    public bool CanReject { get; set; }
    public bool CanCancel { get; set; }
    public bool CanPay { get; set; }
}

public class RefundDto
{
    public int Id { get; set; }
    public int RegistrationId { get; set; }
    public int EventId { get; set; }
    public string RunnerName { get; set; } = "";
    public string? Email { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "";
    public string? BankInfo { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? TransactionRef { get; set; }
}

public class CompleteRefundRequest
{
    [Required, StringLength(100)] public string TransactionRef { get; set; } = "";
}

public class RejectRefundRequest
{
    [Required, StringLength(500)] public string Reason { get; set; } = "";
}
