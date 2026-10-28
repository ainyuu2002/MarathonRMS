using MarathonRMS.DTOs.Events;

namespace MarathonRMS.Services.Events;

// Phần B – Giải & thiết lập giải (Giai đoạn 1). Chỉ có interface; B viết implementation, DAO/Repository
// tương ứng và đăng ký DI trong ServiceCollectionExtensions.
// Mọi thao tác ghi: kiểm tra Organizer thuộc BTC sở hữu giải (BR-30) và BR-46 khi giải đã mở đăng ký.

/// <summary>UC-18, UC-20, UC-34, UC-35.</summary>
public interface IEventService
{
    /// <summary>
    /// Nguồn cho GET /odata/Events: chỉ giải đã Published trở đi (không Draft).
    /// Trả IQueryable chiếu sang DTO bằng Select để OData dịch $filter/$orderby ra SQL.
    /// </summary>
    IQueryable<EventSummaryDto> QueryPublic();

    /// <summary>Danh sách giải của BTC người gọi (mọi trạng thái) cho trang Organizer.</summary>
    IQueryable<EventSummaryDto> QueryMine();

    /// <summary>Chi tiết + cự ly + giá hiện tại (BR-10) + suất còn lại (BR-04). Giải Draft chỉ BTC sở hữu xem được.</summary>
    Task<EventDetailDto> GetDetailAsync(int eventId, CancellationToken ct = default);

    /// <summary>Tạo giải Draft + EventPolicy khởi tạo từ PolicyDefault.* (SystemSettings). Lọc DescriptionHtml (BR-50).</summary>
    Task<EventDetailDto> CreateAsync(UpsertEventRequest request, CancellationToken ct = default);

    Task<EventDetailDto> UpdateAsync(int eventId, UpsertEventRequest request, CancellationToken ct = default);

    /// <summary>Chỉ xóa được giải Draft chưa có đăng ký (BR-31).</summary>
    Task DeleteAsync(int eventId, CancellationToken ct = default);

    /// <summary>Chuyển trạng thái theo luồng hợp lệ; mở đăng ký phải qua BR-38/BR-47.</summary>
    Task ChangeStatusAsync(int eventId, ChangeEventStatusRequest request, CancellationToken ct = default);
}

/// <summary>UC-19 – chính sách giải (BR-46).</summary>
public interface IEventPolicyService
{
    Task<EventPolicyDto> GetAsync(int eventId, CancellationToken ct = default);
    Task<EventPolicyDto> UpdateAsync(int eventId, UpdateEventPolicyRequest request, CancellationToken ct = default);
    Task<EventWaiverDto> GetWaiverAsync(int eventId, CancellationToken ct = default);
    Task<EventWaiverDto> UpdateWaiverAsync(int eventId, UpdateEventWaiverRequest request, CancellationToken ct = default);
}

/// <summary>UC-21 – cự ly (BR-19 dải BIB không chồng lấn, BR-39 StartAt, BR-46 không giảm Capacity dưới số đăng ký).</summary>
public interface IRaceCategoryService
{
    Task<IReadOnlyList<RaceCategoryDto>> GetByEventAsync(int eventId, CancellationToken ct = default);
    Task<RaceCategoryDto> CreateAsync(int eventId, UpsertRaceCategoryRequest request, CancellationToken ct = default);
    Task<RaceCategoryDto> UpdateAsync(int categoryId, UpsertRaceCategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(int categoryId, CancellationToken ct = default);
}

/// <summary>UC-22 – mức giá (BR-47 phủ kín không chồng lấn, BR-48 hợp lệ, BR-49 cảnh báo).</summary>
public interface IPriceTierService
{
    Task<IReadOnlyList<PriceTierDto>> GetByCategoryAsync(int categoryId, CancellationToken ct = default);
    Task<PriceTierDto> CreateAsync(int categoryId, UpsertPriceTierRequest request, CancellationToken ct = default);
    Task<PriceTierDto> UpdateAsync(int priceTierId, UpsertPriceTierRequest request, CancellationToken ct = default);
    Task DeleteAsync(int priceTierId, CancellationToken ct = default);
}

/// <summary>UC-23 – nhóm tuổi (khóa khi kết quả Provisional – BR-46c).</summary>
public interface IAgeGroupService
{
    Task<IReadOnlyList<AgeGroupDto>> GetByEventAsync(int eventId, CancellationToken ct = default);
    Task<AgeGroupDto> CreateAsync(int eventId, UpsertAgeGroupRequest request, CancellationToken ct = default);
    Task<AgeGroupDto> UpdateAsync(int ageGroupId, UpsertAgeGroupRequest request, CancellationToken ct = default);
    Task DeleteAsync(int ageGroupId, CancellationToken ct = default);
}

/// <summary>UC-24 – checkpoint + cut-off tại trạm (BR-39).</summary>
public interface ICheckpointService
{
    Task<IReadOnlyList<CheckpointDto>> GetByCategoryAsync(int categoryId, CancellationToken ct = default);
    Task<CheckpointDto> CreateAsync(int categoryId, UpsertCheckpointRequest request, CancellationToken ct = default);
    Task<CheckpointDto> UpdateAsync(int checkpointId, UpsertCheckpointRequest request, CancellationToken ct = default);
    Task DeleteAsync(int checkpointId, CancellationToken ct = default);
}
