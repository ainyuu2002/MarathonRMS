using MarathonRMS.DTOs.Common;
using MarathonRMS.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.Services.Common;

/// <summary>Phân loại lỗi ràng buộc của SQL Server để trả thông báo đúng nguyên nhân thay vì lỗi 500 / thông báo chung.</summary>
public static class DbErrors
{
    /// <summary>Vi phạm UNIQUE (index 2601, constraint 2627) – thường do hai thao tác chạy cùng lúc.</summary>
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.GetBaseException() is SqlException { Number: 2601 or 2627 };

    /// <summary>Vi phạm CHECK / khóa ngoại (547) – dữ liệu liên quan vừa đổi trạng thái.</summary>
    public static bool IsConstraintViolation(DbUpdateException ex) =>
        ex.GetBaseException() is SqlException { Number: 547 };

    /// <summary>Lỗi ràng buộc ⇒ 409 kèm thông báo; lỗi khác giữ nguyên để GlobalExceptionHandler xử lý.</summary>
    public static Exception ToBusinessError(DbUpdateException ex, string uniqueMessage, string uniqueCode = ErrorCodes.Conflict)
    {
        if (IsUniqueViolation(ex)) return new BusinessRuleException(uniqueCode, uniqueMessage);
        if (IsConstraintViolation(ex))
            return new BusinessRuleException(ErrorCodes.Conflict, "Dữ liệu vừa được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.");
        return ex;
    }
}

/// <summary>Tên khóa dùng chung cho các thao tác ngày thi (IUnitOfWork.TryLockAsync).</summary>
public static class RaceDayLocks
{
    /// <summary>Mọi thao tác thay đổi kết quả của một giải: tính kết quả, bỏ cuộc, phạt, công bố.</summary>
    public static string Results(int eventId) => $"mrms:results:{eventId}";

    /// <summary>Gán BIB của một giải.</summary>
    public static string Bibs(int eventId) => $"mrms:bibs:{eventId}";

    /// <summary>Ghi thời gian của một đăng ký – hai trạm ghi cùng lúc cho một BIB phải lần lượt (BR-21).</summary>
    public static string Timing(int registrationId) => $"mrms:timing:{registrationId}";

    public static async Task LockAsync(this IUnitOfWork uow, string resource, CancellationToken ct)
    {
        if (!await uow.TryLockAsync(resource, ct))
            throw new BusinessRuleException(ErrorCodes.Conflict, "Đang có thao tác khác cập nhật dữ liệu này. Vui lòng thử lại sau ít giây.");
    }
}
