using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.AspNetCore.Identity;

namespace MarathonRMS.BusinessObjects.Data;

/// <summary>
/// Dữ liệu tĩnh nạp qua migration (HasData). Tài khoản demo và dữ liệu giải mẫu
/// được tạo lúc chạy bởi DbInitializer (API) vì cần băm mật khẩu.
/// </summary>
public static class SeedData
{
    // Tên role phải khớp MarathonRMS.DTOs.Common.AppRoles
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Organizer = "Organizer";
    public const string Staff = "Staff";
    public const string Member = "Member";

    private static readonly DateTime SeedTime = new(2026, 10, 15, 0, 0, 0);

    public static readonly IdentityRole[] Roles =
    {
        Role("0b8f0e6c-1a11-4c6e-9d01-000000000001", SuperAdmin),
        Role("0b8f0e6c-1a11-4c6e-9d01-000000000002", Admin),
        Role("0b8f0e6c-1a11-4c6e-9d01-000000000003", Organizer),
        Role("0b8f0e6c-1a11-4c6e-9d01-000000000004", Staff),
        Role("0b8f0e6c-1a11-4c6e-9d01-000000000005", Member),
    };

    private static IdentityRole Role(string id, string name) =>
        new() { Id = id, Name = name, NormalizedName = name.ToUpperInvariant(), ConcurrencyStamp = id };

    public static readonly PenaltyReason[] PenaltyReasons =
    {
        new() { Code = "SHORTCUT", Name = "Đi tắt, không chạy đúng cung đường", DefaultAction = PenaltyAction.DSQ },
        new() { Code = "MISSED_CHECKPOINT", Name = "Không qua checkpoint bắt buộc", DefaultAction = PenaltyAction.DSQ },
        new() { Code = "BIB_TRANSFER", Name = "Chuyển nhượng hoặc dùng BIB trái phép", DefaultAction = PenaltyAction.DSQ },
        new() { Code = "MULTIPLE_BIB", Name = "Đeo nhiều BIB / chạy hộ", DefaultAction = PenaltyAction.DSQ },
        new() { Code = "UNAUTHORISED_ASSIST", Name = "Nhận hỗ trợ trái phép (pacer, xe, người ngoài)", DefaultAction = PenaltyAction.DSQ },
        new() { Code = "WHEELED_DEVICE", Name = "Dùng phương tiện có bánh xe", DefaultAction = PenaltyAction.DSQ },
        new() { Code = "FALSE_INFO", Name = "Khai sai thông tin cá nhân (tuổi, giới tính…)", DefaultAction = PenaltyAction.DSQ },
        new() { Code = "PRO_IN_AGEGROUP", Name = "VĐV chuyên nghiệp đăng ký nhóm tuổi phong trào", DefaultAction = PenaltyAction.DSQ },
        new() { Code = "UNDERAGE", Name = "Không đủ điều kiện tuổi", DefaultAction = PenaltyAction.DSQ },
        new() { Code = "REFUSE_MEDICAL", Name = "Không tuân thủ yêu cầu của đội y tế", DefaultAction = PenaltyAction.DSQ },
        new() { Code = "MISCONDUCT", Name = "Hành vi phi thể thao", DefaultAction = PenaltyAction.DSQ },
        new() { Code = "MISSING_GEAR", Name = "Thiếu vật dụng bắt buộc", DefaultAction = PenaltyAction.TimePenalty, DefaultMinutes = 30 },
        new() { Code = "LITTERING", Name = "Xả rác trên cung đường", DefaultAction = PenaltyAction.TimePenalty, DefaultMinutes = 15 },
        new() { Code = "BIB_NOT_VISIBLE", Name = "Không đeo BIB đúng quy định", DefaultAction = PenaltyAction.Warning },
        new() { Code = "OTHER", Name = "Lý do khác (ghi rõ trong ghi chú)", DefaultAction = PenaltyAction.Warning },
    };

    public static readonly SystemSetting[] SystemSettings =
    {
        S("Payment.MockEnabled", "true", SettingDataType.Bool, "Bật thanh toán giả lập"),
        S("Payment.VnPayEnabled", "false", SettingDataType.Bool, "Bật VNPay sandbox"),
        S("Payment.HoldMinutes.Mock", "15", SettingDataType.Int, "Thời gian giữ chỗ khi thanh toán Mock (phút) – BR-13"),
        S("Payment.HoldMinutes.VnPay", "15", SettingDataType.Int, "Thời gian giữ chỗ khi thanh toán VNPay (phút) – BR-13"),
        S("Price.MinNonZero", "10000", SettingDataType.Decimal, "Giá tối thiểu nếu khác 0 (VND) – BR-48"),
        S("Price.Max", "20000000", SettingDataType.Decimal, "Giá tối đa (VND) – BR-48"),
        S("Fee.Max", "1000000", SettingDataType.Decimal, "Phí chuyển nhượng/đổi cự ly/khiếu nại tối đa (VND) – BR-48"),
        S("Media.MaxFileSizeMB", "5", SettingDataType.Int, "Dung lượng ảnh tối đa – BR-51"),
        S("Media.MaxPerEvent", "200", SettingDataType.Int, "Số ảnh tối đa mỗi giải – BR-51"),
        S("Privacy.MedicalRetentionDays", "30", SettingDataType.Int, "Số ngày giữ dữ liệu y tế sau khi kết quả chính thức – BR-52"),
        S("Platform.FeePercentDefault", "0", SettingDataType.Decimal, "Phí nền tảng mặc định (%)"),
        S("PolicyDefault.GuardianConsentUnderAge", "18", SettingDataType.Int, "Mặc định chính sách giải mới"),
        S("PolicyDefault.MaxDependentsPerAccount", "5", SettingDataType.Int, "Mặc định chính sách giải mới"),
        S("PolicyDefault.ComplaintWindowMinutes", "1440", SettingDataType.Int, "Mặc định chính sách giải mới"),
        S("PolicyDefault.TransferPolicy", "Forbidden", SettingDataType.String, "Mặc định chính sách giải mới"),
    };

    private static SystemSetting S(string key, string value, SettingDataType type, string description) =>
        new() { Key = key, Value = value, DataType = type, Description = description };

    public static readonly LegalDocument[] LegalDocuments =
    {
        new()
        {
            DocumentId = 1, DocType = LegalDocType.Terms, Version = 1, PublishedAt = SeedTime,
            Content = "<h2>Điều khoản sử dụng</h2><p>Phiên bản 1 – nội dung mẫu. SuperAdmin cập nhật tại trang quản trị.</p>"
        },
        new()
        {
            DocumentId = 2, DocType = LegalDocType.Privacy, Version = 1, PublishedAt = SeedTime,
            Content = "<h2>Chính sách quyền riêng tư</h2><p>Phiên bản 1 – nội dung mẫu, căn cứ Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 và Nghị định 356/2025/NĐ-CP.</p>"
        },
    };
}
