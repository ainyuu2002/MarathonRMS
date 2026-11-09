namespace MarathonRMS.DTOs.Admin;

// Phần A – GĐ3: UC-69 dashboard giải (Organizer), UC-71 dashboard hệ thống (Admin). Chỉ đọc, tổng hợp từ dữ liệu có sẵn.

/// <summary>UC-69 – số liệu một giải.</summary>
public class EventDashboardDto
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public string Status { get; set; } = "";
    public string ResultsStatus { get; set; } = "";

    public int Capacity { get; set; }
    public int Confirmed { get; set; }
    public int PendingPayment { get; set; }
    public int KitsCollected { get; set; }
    public int Male { get; set; }
    public int Female { get; set; }

    /// <summary>Tiền đã thu (thanh toán thành công) theo mục đích.</summary>
    public decimal RegistrationRevenue { get; set; }
    public decimal CategoryChangeRevenue { get; set; }
    public decimal TransferFeeRevenue { get; set; }
    public decimal OtherRevenue { get; set; }
    public decimal Refunded { get; set; }
    public decimal NetRevenue { get; set; }

    public int CouponUses { get; set; }
    public decimal CouponDiscount { get; set; }

    public IReadOnlyList<CategoryDashboardDto> Categories { get; set; } = Array.Empty<CategoryDashboardDto>();
    /// <summary>Số đăng ký mới theo ngày (đăng ký còn hiệu lực hoặc đã xử lý, không gồm hủy / hết hạn).</summary>
    public IReadOnlyList<DailyCountDto> RegistrationsByDay { get; set; } = Array.Empty<DailyCountDto>();
}

public class CategoryDashboardDto
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = "";
    public int Capacity { get; set; }
    public int Confirmed { get; set; }
    public int PendingPayment { get; set; }
    public int KitsCollected { get; set; }
    /// <summary>Số áo theo size (XS…XXL) của đăng ký đã xác nhận – để BTC đặt áo.</summary>
    public IReadOnlyDictionary<string, int> ShirtSizes { get; set; } = new Dictionary<string, int>();
    public int Finished { get; set; }
    public int Dnf { get; set; }
    public int Dns { get; set; }
    public int Dsq { get; set; }
}

public class DailyCountDto
{
    public DateOnly Date { get; set; }
    public int Count { get; set; }
}

/// <summary>UC-71 – số liệu toàn hệ thống.</summary>
public class SystemDashboardDto
{
    public int TotalUsers { get; set; }
    public int LockedUsers { get; set; }
    public IReadOnlyList<NamedCountDto> UsersByRole { get; set; } = Array.Empty<NamedCountDto>();
    public int ActiveOrganizations { get; set; }
    public int LockedOrganizations { get; set; }
    public int PendingApplications { get; set; }
    public IReadOnlyList<NamedCountDto> EventsByStatus { get; set; } = Array.Empty<NamedCountDto>();
    public int ActiveRegistrations { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalRefunded { get; set; }
    public int PendingRefunds { get; set; }
    public decimal PendingRefundAmount { get; set; }
    /// <summary>6 tháng gần nhất: số đăng ký mới và tiền thu.</summary>
    public IReadOnlyList<MonthlyStatDto> Monthly { get; set; } = Array.Empty<MonthlyStatDto>();
}

public class NamedCountDto
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
}

public class MonthlyStatDto
{
    /// <summary>"2026-11".</summary>
    public string Month { get; set; } = "";
    public int Registrations { get; set; }
    public decimal Revenue { get; set; }
}
