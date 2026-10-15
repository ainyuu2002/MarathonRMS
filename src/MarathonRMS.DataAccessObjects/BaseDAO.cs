using MarathonRMS.BusinessObjects.Data;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>
/// DAO cơ sở. Quy tắc (BA mục 7.2):
/// - Nhận DbContext qua constructor (scoped) – KHÔNG dùng Singleton / new MarathonDbContext().
/// - KHÔNG gọi SaveChanges – việc lưu do UnitOfWork thực hiện ở tầng Service.
/// </summary>
public abstract class BaseDAO<TEntity> where TEntity : class
{
    protected readonly MarathonDbContext Context;
    protected DbSet<TEntity> Set => Context.Set<TEntity>();

    protected BaseDAO(MarathonDbContext context) => Context = context;

    /// <summary>Truy vấn chỉ đọc – dùng cho danh sách, OData.</summary>
    public virtual IQueryable<TEntity> Query() => Set.AsNoTracking();

    public virtual ValueTask<TEntity?> FindAsync(params object[] keys) => Set.FindAsync(keys);

    public virtual async Task AddAsync(TEntity entity, CancellationToken ct = default) => await Set.AddAsync(entity, ct);

    public virtual void Update(TEntity entity) => Set.Update(entity);

    public virtual void Remove(TEntity entity) => Set.Remove(entity);
}
