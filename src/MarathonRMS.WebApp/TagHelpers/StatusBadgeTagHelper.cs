using Microsoft.AspNetCore.Razor.TagHelpers;

namespace MarathonRMS.WebApp.TagHelpers;

/// <summary>
/// &lt;status-badge status="Pending" /&gt; ⇒ badge Bootstrap màu theo trạng thái, nhãn tiếng Việt.
/// Dùng chung cho mọi trạng thái của hệ thống (hồ sơ BTC, giải, đăng ký, thanh toán, kết quả…).
/// </summary>
[HtmlTargetElement("status-badge", TagStructure = TagStructure.WithoutEndTag)]
public class StatusBadgeTagHelper : TagHelper
{
    private static readonly Dictionary<string, (string Label, string Css)> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Chung
        ["Active"] = ("Hoạt động", "text-bg-success"),
        ["Inactive"] = ("Đã khóa", "text-bg-secondary"),
        // Hồ sơ BTC
        ["Pending"] = ("Chờ duyệt", "text-bg-warning"),
        ["Approved"] = ("Đã duyệt", "text-bg-success"),
        ["Rejected"] = ("Từ chối", "text-bg-danger"),
        // Giải
        ["Draft"] = ("Bản nháp", "text-bg-secondary"),
        ["Published"] = ("Đã công bố", "text-bg-info"),
        ["RegistrationOpen"] = ("Đang mở đăng ký", "text-bg-success"),
        ["RegistrationClosed"] = ("Đã đóng đăng ký", "text-bg-dark"),
        ["Postponed"] = ("Tạm hoãn", "text-bg-warning"),
        ["Completed"] = ("Đã kết thúc", "text-bg-primary"),
        ["Cancelled"] = ("Đã hủy", "text-bg-danger"),
        // Kết quả
        ["Provisional"] = ("Tạm thời", "text-bg-warning"),
        ["Official"] = ("Chính thức", "text-bg-success"),
        // Đăng ký / thanh toán
        ["PendingPayment"] = ("Chờ thanh toán", "text-bg-warning"),
        ["Confirmed"] = ("Đã xác nhận", "text-bg-success"),
        ["Expired"] = ("Hết hạn", "text-bg-secondary"),
        ["Transferred"] = ("Đã chuyển nhượng", "text-bg-secondary"),
        ["RefundPending"] = ("Chờ hoàn tiền", "text-bg-warning"),
        ["Refunded"] = ("Đã hoàn tiền", "text-bg-info"),
        ["Success"] = ("Thành công", "text-bg-success"),
        ["Failed"] = ("Thất bại", "text-bg-danger"),
        // Thành tích
        ["Finished"] = ("Hoàn thành", "text-bg-success"),
        ["DNF"] = ("DNF", "text-bg-warning"),
        ["DNS"] = ("DNS", "text-bg-secondary"),
        ["DSQ"] = ("DSQ", "text-bg-danger"),
        // Coupon
        ["Scheduled"] = ("Chưa tới hạn", "text-bg-info"),
        ["Exhausted"] = ("Hết lượt", "text-bg-secondary"),
    };

    public string? Status { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (label, css) = Status is not null && Map.TryGetValue(Status, out var m) ? m : (Status ?? "", "text-bg-light");
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", $"badge {css}");
        output.Content.SetContent(label);
    }
}
