using MarathonRMS.DTOs.Admin;
using MarathonRMS.DTOs.Events;
using MarathonRMS.DTOs.Results;
using MarathonRMS.DTOs.Registrations;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace MarathonRMS.API.OData;

/// <summary>
/// EDM model cho route "odata". Mỗi phần thêm entity set của mình vào đúng vùng bên dưới.
/// Entity set là DTO (không phải entity EF): controller kế thừa ODataController, trả IQueryable&lt;Dto&gt; từ Service
/// và gắn [EnableQuery] – OData dịch $filter/$orderby/$top/$skip/$count ra SQL.
/// Tên controller phải trùng tên entity set (VD AuditLogsController ↔ "AuditLogs").
/// </summary>
public static class EdmModelBuilder
{
    public static IEdmModel Build()
    {
        var builder = new ODataConventionModelBuilder();

        // ---------- Phần A ----------
        builder.EntitySet<AuditLogDto>("AuditLogs");

        // ---------- Phần B ----------
        builder.EntitySet<EventSummaryDto>("Events").EntityType.HasKey(e => e.Id);
        builder.EntitySet<RegistrationListItemDto>("Registrations").EntityType.HasKey(r => r.Id);

        // ---------- Phần A – GĐ3 ----------
        builder.EntitySet<PublicRunnerDto>("Participants");

        // ---------- Phần C ----------
        builder.EntitySet<LeaderboardRowDto>("Results").EntityType.HasKey(r => r.ResultId);

        return builder.GetEdmModel();
    }
}
