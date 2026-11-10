using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Events;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.Events;

/// <summary>
/// Phần A – UC-87 (GĐ3): khoảng giá tham khảo khi Organizer nhập mức giá (BA mục 0.5).
/// Nền tảng không áp giá – chỉ gợi ý "thấp nhất – trung vị – cao nhất" giá Regular của các giải cùng loại,
/// theo loại đường chạy + nhóm cự ly. Chưa đủ dữ liệu ⇒ dùng bảng khảo sát trong BA.
/// </summary>
public interface IPriceReferenceService
{
    Task<PriceReferenceDto> GetAsync(string discipline, decimal distanceKm, int? excludeEventId = null, CancellationToken ct = default);

    /// <summary>BR-49 (ý 2): giá lệch khoảng tham khảo quá 50% ⇒ câu cảnh báo, ngược lại null.</summary>
    string? Warn(PriceReferenceDto reference, decimal price);
}

public class PriceReferenceService : IPriceReferenceService
{
    /// <summary>Số cự ly tối thiểu trên hệ thống để dùng số liệu thật thay cho bảng khảo sát.</summary>
    private const int MinSamples = 3;

    /// <summary>Bảng khảo sát (BA mục 0.5): VnExpress Hà Nội 2026 (đường phố), Cúc Phương Jungle Paths 2026 (trail). Đơn vị đồng.</summary>
    private static readonly Dictionary<(Discipline, string), (decimal Min, decimal Median, decimal Max)> Survey = new()
    {
        [(Discipline.Road, "≤ 5K")] = (420_000, 600_000, 790_000),
        [(Discipline.Road, "10K")] = (480_000, 700_000, 950_000),
        [(Discipline.Road, "21K")] = (660_000, 900_000, 1_150_000),
        [(Discipline.Road, "42K")] = (780_000, 1_050_000, 1_350_000),
        [(Discipline.Trail, "10K")] = (1_232_000, 1_232_000, 1_232_000),
        [(Discipline.Trail, "42K")] = (2_816_000, 2_816_000, 2_816_000),
        [(Discipline.Trail, "> 70K")] = (3_960_000, 3_960_000, 3_960_000),
    };

    private readonly IPriceReferenceRepository _repo;
    public PriceReferenceService(IPriceReferenceRepository repo) => _repo = repo;

    /// <summary>Nhóm cự ly theo BA: ≤ 5K, 10K, 21K, 42K, 43–70K, > 70K.</summary>
    public static string BandOf(decimal km) => km switch
    {
        <= 5m => "≤ 5K",
        <= 10.5m => "10K",
        <= 21.5m => "21K",
        <= 42.5m => "42K",
        <= 70m => "43–70K",
        _ => "> 70K"
    };

    /// <summary>(cận dưới – không gồm, cận trên – gồm) của nhóm cự ly; cự ly tối đa 1000 km.</summary>
    private static (decimal LowExclusive, decimal HighInclusive) BandRange(string band) => band switch
    {
        "≤ 5K" => (0m, 5m),
        "10K" => (5m, 10.5m),
        "21K" => (10.5m, 21.5m),
        "42K" => (21.5m, 42.5m),
        "43–70K" => (42.5m, 70m),
        _ => (70m, 1000m)
    };

    public async Task<PriceReferenceDto> GetAsync(string discipline, decimal distanceKm, int? excludeEventId = null, CancellationToken ct = default)
    {
        var d = EnumParser.ParseOptional<Discipline>(discipline)
                ?? throw new BusinessRuleException(ErrorCodes.Validation, "Thiếu loại đường chạy.", 400);
        if (distanceKm <= 0) throw new BusinessRuleException(ErrorCodes.Validation, "Cự ly phải lớn hơn 0.", 400);
        var band = BandOf(distanceKm);
        var (low, high) = BandRange(band);

        // Giá "Regular" của mỗi cự ly = mức giá có khung thời gian dài nhất (bỏ giá 0 – cự ly miễn phí không làm tham khảo)
        var prices = (await _repo.GetRegularPricesAsync(d, low, high, excludeEventId, ct)).Where(p => p > 0).OrderBy(p => p).ToList();
        var dto = new PriceReferenceDto { Discipline = d.ToString(), DistanceBand = band, SampleCount = prices.Count };
        if (prices.Count >= MinSamples)
        {
            dto.Source = "System";
            dto.Min = prices[0];
            dto.Max = prices[^1];
            dto.Median = prices.Count % 2 == 1 ? prices[prices.Count / 2] : (prices[prices.Count / 2 - 1] + prices[prices.Count / 2]) / 2;
        }
        else if (Survey.TryGetValue((d, band), out var s))
        {
            dto.Source = "Survey";
            (dto.Min, dto.Median, dto.Max) = (s.Min, s.Median, s.Max);
        }
        else dto.Source = "None";
        return dto;
    }

    public string? Warn(PriceReferenceDto r, decimal price)
    {
        if (r.Min is not decimal min || r.Max is not decimal max || price == 0) return null;
        if (price < min * 0.5m)
            return $"Giá thấp hơn khoảng tham khảo {r.DistanceBand} quá 50% (tham khảo {min:N0}đ – {max:N0}đ).";
        if (price > max * 1.5m)
            return $"Giá cao hơn khoảng tham khảo {r.DistanceBand} quá 50% (tham khảo {min:N0}đ – {max:N0}đ).";
        return null;
    }
}
