using MarathonRMS.BusinessObjects.Data;
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
}

public class UnitOfWork : IUnitOfWork
{
    private readonly MarathonDbContext _context;

    public UnitOfWork(MarathonDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        _context.Database.BeginTransactionAsync(ct);
}
