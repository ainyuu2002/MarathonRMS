namespace MarathonRMS.DTOs.Events;

/// <summary>Phần A – UC-87: khoảng giá tham khảo theo loại đường chạy + nhóm cự ly (BA mục 0.5).</summary>
public class PriceReferenceDto
{
    public string Discipline { get; set; } = "";
    /// <summary>"≤ 5K", "10K", "21K", "42K", "43–70K", "> 70K".</summary>
    public string DistanceBand { get; set; } = "";
    /// <summary>"System" (giá các giải trên hệ thống), "Survey" (bảng khảo sát khi chưa đủ dữ liệu), "None".</summary>
    public string Source { get; set; } = "None";
    public int SampleCount { get; set; }
    public decimal? Min { get; set; }
    public decimal? Median { get; set; }
    public decimal? Max { get; set; }
}
