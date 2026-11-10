using System.Globalization;

namespace MarathonRMS.Services.Common;

/// <summary>Định dạng số tiền trong thông báo nghiệp vụ theo kiểu Việt Nam (500.000đ), không phụ thuộc culture của máy chủ.</summary>
public static class VnFormat
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("vi-VN");
}
