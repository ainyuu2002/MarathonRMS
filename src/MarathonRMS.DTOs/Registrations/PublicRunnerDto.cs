namespace MarathonRMS.DTOs.Registrations;

/// <summary>
/// Phần A – UC-38 (GĐ3): một dòng danh sách VĐV công khai – nguồn của GET /odata/Participants.
/// Chỉ dữ liệu tối thiểu (BR-37): tên công khai, BIB, cự ly, giới tính, CLB. Không ngày sinh, giấy tờ, SĐT, y tế.
/// </summary>
public class PublicRunnerDto
{
    /// <summary>RegistrationId.</summary>
    public int Id { get; set; }
    public int EventId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public int? BibNumber { get; set; }
    /// <summary>Họ tên, hoặc "VĐV #BIB" khi người chạy chọn ẩn tên / dưới 18 tuổi tại ngày thi.</summary>
    public string DisplayName { get; set; } = "";
    public string Gender { get; set; } = "";
    public string? ClubName { get; set; }
}
