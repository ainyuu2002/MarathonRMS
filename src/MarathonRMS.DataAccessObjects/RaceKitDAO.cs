using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Truy vấn quầy race kit (UC-52, UC-53). Không chứa quy tắc nghiệp vụ và không tự SaveChanges.</summary>
public class RaceKitDAO : BaseDAO<RaceKitPickup>
{
    public RaceKitDAO(MarathonDbContext context) : base(context) { }

    public Task<Event?> GetEventWithPolicyAsync(int eventId, CancellationToken ct = default) =>
        Context.Events.AsNoTracking()
            .Include(e => e.Policy)
            .FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    /// <summary>Tìm đăng ký trong giải theo đúng số BIB, mã QR, số giấy tờ hoặc số điện thoại.</summary>
    public Task<List<Registration>> SearchAsync(int eventId, int? bibNumber, string keyword, string compactKeyword, int take,
        CancellationToken ct = default) =>
        Context.Registrations.AsNoTracking()
            .Include(r => r.Category)
            .Include(r => r.Participant)
            .Include(r => r.RaceKitPickup)
            .Where(r => r.EventId == eventId &&
                        ((bibNumber != null && r.BibNumber == bibNumber) ||
                         r.QrToken == keyword ||
                         r.IdNumber == keyword ||
                         r.Phone == compactKeyword))
            .OrderBy(r => r.BibNumber).ThenBy(r => r.FullName)
            .Take(take)
            .ToListAsync(ct);

    public Task<Registration?> GetRegistrationForPickupAsync(int registrationId, CancellationToken ct = default) =>
        Context.Registrations.AsNoTracking()
            .Include(r => r.Event).ThenInclude(e => e.Policy)
            .Include(r => r.Participant)
            .Include(r => r.RaceKitPickup)
            .FirstOrDefaultAsync(r => r.RegistrationId == registrationId, ct);

    public Task<string?> GetUserFullNameAsync(string userId, CancellationToken ct = default) =>
        Context.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
}
