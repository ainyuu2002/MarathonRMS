using MarathonRMS.BusinessObjects.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MarathonRMS.Repositories;

/// <summary>
/// Lưu thay đổi và mở transaction trên DbContext dùng chung trong request.
/// Dùng cho các nghiệp vụ nhiều bước phải nguyên tử (VD đăng ký: đếm suất → coupon → tạo đăng ký – BR-04).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Khóa độc quyền theo tên (sp_getapplock) trong transaction đang mở – các thao tác cùng tên chạy lần lượt.
    /// Khóa tự nhả khi transaction commit/rollback. Trả false nếu chờ quá thời gian.
    /// </summary>
    Task<bool> TryLockAsync(string resource, CancellationToken ct = default);
}

public class UnitOfWork : IUnitOfWork
{
    private const int LockTimeoutMs = 15_000;
    private readonly MarathonDbContext _context;

    public UnitOfWork(MarathonDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        _context.Database.BeginTransactionAsync(ct);

    public async Task<bool> TryLockAsync(string resource, CancellationToken ct = default)
    {
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("TryLockAsync cần transaction đang mở.");
        var codes = await _context.Database.SqlQuery<int>(
                $"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {LockTimeoutMs}; SELECT @r AS [Value]")
            .ToListAsync(ct);
        return codes.Count > 0 && codes[0] >= 0;
    }
}
