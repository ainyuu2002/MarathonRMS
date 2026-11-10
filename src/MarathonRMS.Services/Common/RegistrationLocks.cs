namespace MarathonRMS.Services.Common;

/// <summary>
/// Tên khóa (IUnitOfWork.TryLockAsync) quanh bảng Registrations.
/// Phần B: đăng ký giữ khóa cự ly, thanh toán / hết hạn / sửa thông tin giữ khóa từng đăng ký – mỗi luồng chỉ một khóa.
/// Phần A (đổi cự ly, chuyển nhượng, hủy giải) lấy khóa giải trước rồi mới lấy khóa của B ⇒ không có vòng chờ.
/// </summary>
public static class RegistrationLocks
{
    /// <summary>Phần A – mọi thay đổi đăng ký trong một giải (UC-25, 46, 47).</summary>
    public static string Event(int eventId) => $"mrms:registrations:{eventId}";

    /// <summary>Phần B – đếm suất khi đăng ký (UC-39).</summary>
    public static string Category(int categoryId) => $"mrms:registration-category:{categoryId}";

    /// <summary>Phần B – thanh toán, hết hạn giữ chỗ, hủy, sửa thông tin của một đăng ký (UC-41, 43–45).</summary>
    public static string Payment(int registrationId) => $"mrms:payment-registration:{registrationId}";
}
