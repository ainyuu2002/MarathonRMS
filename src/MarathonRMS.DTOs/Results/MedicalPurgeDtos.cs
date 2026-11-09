namespace MarathonRMS.DTOs.Results;

/// <summary>Kết quả một lần tự xóa dữ liệu y tế (UC-91, BR-52).</summary>
public class MedicalPurgeSummaryDto
{
    public int RetentionDays { get; set; }
    public DateTime RanAt { get; set; }
    public IReadOnlyList<MedicalPurgeEventDto> Events { get; set; } = Array.Empty<MedicalPurgeEventDto>();
}

public class MedicalPurgeEventDto
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateTime? OfficialAt { get; set; }
    /// <summary>Số đăng ký đã xóa nhóm máu / ghi chú y tế.</summary>
    public int Registrations { get; set; }
}
