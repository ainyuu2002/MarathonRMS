using System.Text.RegularExpressions;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;

namespace MarathonRMS.Services.Common;

/// <summary>
/// BR-07 – định dạng số giấy tờ dùng chung cho đăng ký (UC-39), sửa thông tin đã khóa (UC-48), nhận chuyển nhượng (UC-47).
/// Số sai định dạng làm kiểm tra trùng người theo giấy tờ (BR-01) mất tác dụng.
/// </summary>
public static class IdentityRules
{
    private static readonly Regex Cccd = new(@"^\d{12}$", RegexOptions.Compiled);
    private static readonly Regex Passport = new(@"^[A-Z0-9]{6,20}$", RegexOptions.Compiled);
    private static readonly Regex Other = new(@"^[A-Z0-9]{1,20}$", RegexOptions.Compiled);

    /// <summary>Kiểm tra số giấy tờ đã chuẩn hóa (trim + chữ hoa). Null = không khai số.</summary>
    public static void Validate(IdType? idType, string? idNumber)
    {
        if (idNumber is null) return;
        if (idType is null)
            throw new BusinessRuleException("BR-07", "Vui lòng chọn loại giấy tờ.", 400);
        var ok = idType switch
        {
            IdType.CCCD => Cccd.IsMatch(idNumber),
            IdType.Passport => Passport.IsMatch(idNumber),
            _ => Other.IsMatch(idNumber)
        };
        if (!ok)
            throw new BusinessRuleException("BR-07", idType switch
            {
                IdType.CCCD => "Số CCCD gồm 12 chữ số.",
                IdType.Passport => "Số hộ chiếu gồm 6–20 chữ cái không dấu hoặc chữ số.",
                _ => "Số giấy tờ chỉ gồm chữ cái không dấu và chữ số."
            }, 400);
    }
}
