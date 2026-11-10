using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Events;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using MarathonRMS.Services.Events;
using MarathonRMS.Services.RaceDay;

namespace MarathonRMS.Tests.Unit;

public class SlugAndEnumTests
{
    [Theory]
    [InlineData("Hà Nội Marathon 2026", "ha-noi-marathon-2026")]
    [InlineData("  Đà Lạt Ultra Trail  ", "da-lat-ultra-trail")]
    [InlineData("VnExpress -- Marathon!!", "vnexpress-marathon")]
    [InlineData("!!!", "")]
    public void Slug_strips_vietnamese_marks(string input, string expected) => Assert.Equal(expected, SlugHelper.ToSlug(input));

    [Fact]
    public void Slug_is_limited_to_180_chars()
    {
        var slug = SlugHelper.ToSlug(string.Join(" ", Enumerable.Repeat("marathon", 40)));
        Assert.True(slug.Length <= 180);
        Assert.False(slug.EndsWith('-'));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void EnumParser_empty_is_null(string? value) => Assert.Null(EnumParser.ParseOptional<Discipline>(value));

    [Theory]
    [InlineData("road", Discipline.Road)]
    [InlineData(" Trail ", Discipline.Trail)]
    public void EnumParser_parses_names_ignoring_case(string value, Discipline expected) =>
        Assert.Equal(expected, EnumParser.ParseOptional<Discipline>(value));

    [Theory]
    [InlineData("7")]
    [InlineData("0")]
    [InlineData("Mountain")]
    public void EnumParser_rejects_numbers_and_unknown_with_400(string value)
    {
        var ex = Assert.Throws<BusinessRuleException>(() => EnumParser.ParseOptional<Discipline>(value));
        Assert.Equal(400, ex.StatusCode);
    }
}

public class AgeAndPrivacyTests
{
    [Theory]
    [InlineData("2008-11-15", "2026-11-15", 18)]
    [InlineData("2008-11-16", "2026-11-15", 17)]
    [InlineData("2000-02-29", "2026-02-28", 25)]
    [InlineData("2000-02-29", "2026-03-01", 26)]
    public void AgeAt_counts_full_years(string dob, string on, int expected) =>
        Assert.Equal(expected, AgeCalculator.AgeAt(DateOnly.Parse(dob), DateOnly.Parse(on)));

    [Fact]
    public void Calendar_year_mode_ignores_birthday_br05()
    {
        var dob = new DateOnly(2008, 12, 31);
        var race = new DateOnly(2026, 1, 1);
        Assert.Equal(18, RaceDayRules.AgeForEvent(dob, race, AgeCalculationMode.CalendarYear));
        Assert.Equal(17, RaceDayRules.AgeForEvent(dob, race, AgeCalculationMode.RaceDate));
    }

    private readonly PrivacyFilter _privacy = new();
    private static readonly DateOnly Race = new(2026, 11, 15);

    [Fact]
    public void Adult_name_is_public_br37() =>
        Assert.Equal("Nguyễn Văn A", _privacy.PublicDisplayName("Nguyễn Văn A", false, new DateOnly(2008, 11, 15), Race, 101));

    [Fact]
    public void Minor_name_is_hidden_by_default_br37() =>
        Assert.Equal("VĐV #101", _privacy.PublicDisplayName("Nguyễn Văn A", false, new DateOnly(2008, 11, 16), Race, 101));

    [Fact]
    public void Hidden_name_without_bib_is_anonymous() =>
        Assert.Equal("VĐV ẩn danh", _privacy.PublicDisplayName("Nguyễn Văn A", true, new DateOnly(1990, 1, 1), Race, null));

    [Theory]
    [InlineData("079201001234", "********1234")]
    [InlineData("12345", "*2345")]
    [InlineData(null, null)]
    public void Id_number_is_masked(string? input, string? expected) => Assert.Equal(expected, _privacy.MaskIdNumber(input));

    [Fact]
    public void Compact_removes_separators() => Assert.Equal("0912345678", RaceDayRules.Compact("091 234.56-78"));
}

public class HtmlSanitizerTests
{
    private readonly HtmlContentSanitizer _s = new();

    [Theory]
    [InlineData("<p>Hi<script>alert(1)</script></p>", "script")]
    [InlineData("<img src=\"https://x/a.png\" onerror=\"alert(1)\">", "onerror")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "javascript:")]
    [InlineData("<iframe src=\"https://evil\"></iframe>", "iframe")]
    [InlineData("<p style=\"background:url(javascript:alert(1))\">x</p>", "style")]
    [InlineData("<svg onload=alert(1)></svg>", "svg")]
    public void Dangerous_html_is_removed_br50(string html, string forbidden)
    {
        var clean = _s.Sanitize(html) ?? "";
        Assert.DoesNotContain(forbidden, clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Safe_formatting_is_kept() =>
        Assert.Equal("<p><strong>Đường</strong> chạy <a href=\"https://mrms.vn\">xem</a></p>",
            _s.Sanitize("<p><strong>Đường</strong> chạy <a href=\"https://mrms.vn\">xem</a></p>"));

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Blank_is_null(string? html) => Assert.Null(_s.Sanitize(html));
}

/// <summary>UC-87 – khoảng giá tham khảo, BR-49 (cảnh báo giá).</summary>
public class PriceReferenceTests
{
    [Theory]
    [InlineData(3, "≤ 5K")]
    [InlineData(5, "≤ 5K")]
    [InlineData(5.01, "10K")]
    [InlineData(10.5, "10K")]
    [InlineData(21.0975, "21K")]
    [InlineData(42.195, "42K")]
    [InlineData(50, "43–70K")]
    [InlineData(70, "43–70K")]
    [InlineData(100, "> 70K")]
    public void BandOf_groups_distances_uc87(double km, string band) => Assert.Equal(band, PriceReferenceService.BandOf((decimal)km));

    private static readonly PriceReferenceDto Ref = new() { DistanceBand = "10K", Min = 100_000, Max = 200_000 };

    [Theory]
    [InlineData(49_000, true)]
    [InlineData(50_000, false)]
    [InlineData(300_000, false)]
    [InlineData(301_000, true)]
    [InlineData(0, false)]
    public void Warn_when_price_is_more_than_50_percent_outside_br49(int price, bool warns)
    {
        var svc = new PriceReferenceService(new FakeRepo());
        Assert.Equal(warns, svc.Warn(Ref, price) is not null);
    }

    [Fact]
    public void Warn_without_reference_is_null() =>
        Assert.Null(new PriceReferenceService(new FakeRepo()).Warn(new PriceReferenceDto(), 1));

    [Fact]
    public async Task Uses_system_prices_with_three_samples_and_median()
    {
        var svc = new PriceReferenceService(new FakeRepo(400_000, 0, 200_000, 300_000, 500_000));
        var dto = await svc.GetAsync("Road", 10);
        Assert.Equal("System", dto.Source);
        Assert.Equal(4, dto.SampleCount); // giá 0 bị bỏ
        Assert.Equal(200_000, dto.Min);
        Assert.Equal(350_000, dto.Median);
        Assert.Equal(500_000, dto.Max);
    }

    [Fact]
    public async Task Falls_back_to_survey_with_few_samples()
    {
        var dto = await new PriceReferenceService(new FakeRepo(500_000)).GetAsync("road", 42.195m);
        Assert.Equal("Survey", dto.Source);
        Assert.Equal(1_050_000, dto.Median);
    }

    [Fact]
    public async Task Unknown_band_has_no_reference()
    {
        var dto = await new PriceReferenceService(new FakeRepo()).GetAsync("Trail", 3);
        Assert.Equal("None", dto.Source);
        Assert.Null(dto.Min);
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("Road", 0)]
    [InlineData("Road", -5)]
    [InlineData("Swim", 10)]
    public async Task Invalid_input_is_400(string discipline, int km)
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => new PriceReferenceService(new FakeRepo()).GetAsync(discipline, km));
        Assert.Equal(400, ex.StatusCode);
    }

    private sealed class FakeRepo : IPriceReferenceRepository
    {
        private readonly decimal[] _prices;
        public FakeRepo(params decimal[] prices) => _prices = prices;
        public Task<List<decimal>> GetRegularPricesAsync(Discipline discipline, decimal minKm, decimal maxKm, int? excludeEventId,
            CancellationToken ct = default) => Task.FromResult(_prices.ToList());
    }
}
