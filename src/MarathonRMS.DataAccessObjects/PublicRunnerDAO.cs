using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Phần A – UC-38: đăng ký đã xác nhận của các giải đã công bố (không nháp, không hủy). Chỉ đọc.</summary>
public class PublicRunnerDAO : BaseDAO<Registration>
{
    public PublicRunnerDAO(MarathonDbContext context) : base(context) { }

    public IQueryable<Registration> QueryConfirmedPublic() =>
        Set.AsNoTracking().Where(r => r.Status == RegistrationStatus.Confirmed &&
                                      r.Event.Status != EventStatus.Draft && r.Event.Status != EventStatus.Cancelled);
}
