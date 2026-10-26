using System.Globalization;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Admin;
using MarathonRMS.DTOs.Common;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.Admin;

/// <summary>UC-15 – SuperAdmin xem/sửa cấu hình hệ thống. Chỉ sửa giá trị của khóa có sẵn (khóa do code đọc).</summary>
public interface ISystemSettingService
{
    Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken ct = default);
    Task<SystemSettingDto> UpdateAsync(string key, UpdateSettingRequest request, CancellationToken ct = default);
}

public class SystemSettingService : ISystemSettingService
{
    private readonly ISystemSettingRepository _settings;
    private readonly ISystemSettingsProvider _provider;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogger _audit;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    public SystemSettingService(ISystemSettingRepository settings, ISystemSettingsProvider provider, ICurrentUserService currentUser,
        IAuditLogger audit, IDateTimeProvider clock, IUnitOfWork uow)
    {
        _settings = settings;
        _provider = provider;
        _currentUser = currentUser;
        _audit = audit;
        _clock = clock;
        _uow = uow;
    }

    public async Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken ct = default) =>
        (await _settings.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task<SystemSettingDto> UpdateAsync(string key, UpdateSettingRequest request, CancellationToken ct = default)
    {
        var setting = await _settings.GetTrackedAsync(key, ct) ?? throw new NotFoundException($"Không có cấu hình '{key}'.");
        var value = Normalize(setting, request.Value.Trim());
        Validate(setting.Key, value);

        var old = setting.Value;
        if (old == value) return ToDto(setting);
        setting.Value = value;
        setting.UpdatedById = _currentUser.UserId;
        setting.UpdatedAt = _clock.Now;
        await _audit.LogAsync("Setting.Update", nameof(SystemSetting), key, new { Value = old }, new { Value = value }, ct);
        await _uow.SaveChangesAsync(ct);
        _provider.Invalidate();
        return ToDto(setting);
    }

    /// <summary>Kiểm tra kiểu dữ liệu và chuẩn hóa (bool ⇒ "true"/"false", số ⇒ dạng invariant).</summary>
    private static string Normalize(SystemSetting setting, string value)
    {
        string Fail(string msg) => throw new BusinessRuleException(ErrorCodes.Validation, msg, 400);
        return setting.DataType switch
        {
            SettingDataType.Bool => bool.TryParse(value, out var b) ? b.ToString().ToLowerInvariant() : Fail("Giá trị phải là true hoặc false."),
            SettingDataType.Int => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) && i >= 0
                ? i.ToString(CultureInfo.InvariantCulture) : Fail("Giá trị phải là số nguyên không âm."),
            SettingDataType.Decimal => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d >= 0
                ? d.ToString(CultureInfo.InvariantCulture) : Fail("Giá trị phải là số không âm (dấu chấm thập phân)."),
            _ => value
        };
    }

    /// <summary>Ràng buộc riêng của một số khóa.</summary>
    private static void Validate(string key, string value)
    {
        static void Fail(string msg) => throw new BusinessRuleException(ErrorCodes.Validation, msg, 400);
        decimal Num() => decimal.Parse(value, CultureInfo.InvariantCulture);

        if (key.StartsWith("Payment.HoldMinutes.") && (Num() < 5 || Num() > 2880)) Fail("Thời gian giữ chỗ phải từ 5 đến 2880 phút.");
        if (key == "Platform.FeePercentDefault" && Num() > 100) Fail("Phí nền tảng tối đa 100%.");
        if (key == "Media.MaxFileSizeMB" && (Num() < 1 || Num() > 20)) Fail("Dung lượng ảnh từ 1 đến 20 MB.");
        if (key == "PolicyDefault.GuardianConsentUnderAge" && Num() > 21) Fail("Ngưỡng giám hộ tối đa 21 tuổi.");
        if (key == "PolicyDefault.MaxDependentsPerAccount" && Num() > 10) Fail("Tối đa 10 người phụ thuộc.");
        if (key == "PolicyDefault.ComplaintWindowMinutes" && (Num() < 10 || Num() > 20160)) Fail("Cửa sổ khiếu nại từ 10 đến 20160 phút.");
        if (key == "PolicyDefault.TransferPolicy" && !Enum.TryParse<TransferPolicy>(value, out _)) Fail("Giá trị phải là Forbidden, Free hoặc Paid.");
        if ((key is "Price.MinNonZero" or "Price.Max" or "Fee.Max") && Num() % 1000 != 0) Fail("Số tiền phải là bội số 1.000đ.");
    }

    private static SystemSettingDto ToDto(SystemSetting s) => new()
    {
        Key = s.Key, Value = s.Value, DataType = s.DataType.ToString(), Description = s.Description, UpdatedAt = s.UpdatedAt
    };
}
