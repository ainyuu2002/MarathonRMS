using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

/// <summary>Phần A – UC-87.</summary>
public interface IPriceReferenceRepository
{
    Task<List<decimal>> GetRegularPricesAsync(Discipline discipline, decimal minKm, decimal maxKm, int? excludeEventId, CancellationToken ct = default);
}

public class PriceReferenceRepository : IPriceReferenceRepository
{
    private readonly PriceReferenceDAO _dao;
    public PriceReferenceRepository(PriceReferenceDAO dao) => _dao = dao;

    public Task<List<decimal>> GetRegularPricesAsync(Discipline discipline, decimal minKm, decimal maxKm, int? excludeEventId,
        CancellationToken ct = default) => _dao.GetRegularPricesAsync(discipline, minKm, maxKm, excludeEventId, ct);
}
