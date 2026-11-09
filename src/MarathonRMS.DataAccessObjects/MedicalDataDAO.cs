using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarathonRMS.DataAccessObjects;

/// <summary>Truy vấn / xóa dữ liệu y tế trong snapshot đăng ký (UC-91, BR-52). Không chứa quy tắc nghiệp vụ.</summary>
public class MedicalDataDAO : BaseDAO<Registration>
{
    public MedicalDataDAO(MarathonDbContext context) : base(context) { }

    /// <summary>Giải có kết quả chính thức từ trước mốc <paramref name="officialBefore"/> mà vẫn còn dữ liệu y tế.</summary>
    public Task<List<Event>> GetDueEventsAsync(DateTime officialBefore, CancellationToken ct = default) =>
        Context.Events.AsNoTracking()
            .Where(e => e.ResultsStatus == ResultsStatus.Official && e.ResultsOfficialAt != null &&
                        e.ResultsOfficialAt <= officialBefore &&
                        Context.Registrations.Any(r => r.EventId == e.EventId && (r.BloodType != null || r.MedicalNotes != null)))
            .OrderBy(e => e.ResultsOfficialAt)
            .ToListAsync(ct);

    /// <summary>Xóa nhóm máu và ghi chú y tế trong snapshot đăng ký của giải (cập nhật thẳng DB). Trả số đăng ký đã xóa.</summary>
    public Task<int> PurgeEventAsync(int eventId, CancellationToken ct = default) =>
        Set.Where(r => r.EventId == eventId && (r.BloodType != null || r.MedicalNotes != null))
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.BloodType, (string?)null)
                .SetProperty(r => r.MedicalNotes, (string?)null), ct);
}
