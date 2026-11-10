using System.Globalization;

namespace MarathonRMS.WebApp.Infrastructure;

/// <summary>
/// Định dạng số kiểu Việt Nam (500.000đ) chỉ ở chỗ hiển thị. Không đổi CurrentCulture của WebApp vì model binding
/// đọc số thập phân từ form theo culture hiện tại (ô number gửi "21.1").
/// </summary>
public static class VnFormat
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("vi-VN");
}
