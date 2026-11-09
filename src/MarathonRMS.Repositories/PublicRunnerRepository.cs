using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DataAccessObjects;

namespace MarathonRMS.Repositories;

/// <summary>Phần A – UC-38.</summary>
public interface IPublicRunnerRepository
{
    IQueryable<Registration> QueryConfirmedPublic();
}

public class PublicRunnerRepository : IPublicRunnerRepository
{
    private readonly PublicRunnerDAO _dao;
    public PublicRunnerRepository(PublicRunnerDAO dao) => _dao = dao;

    public IQueryable<Registration> QueryConfirmedPublic() => _dao.QueryConfirmedPublic();
}
