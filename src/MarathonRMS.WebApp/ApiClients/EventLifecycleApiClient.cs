using MarathonRMS.DTOs.Events;
using MarathonRMS.WebApp.Infrastructure;

namespace MarathonRMS.WebApp.ApiClients;

/// <summary>Phần A – UC-25 hoãn / hủy giải, UC-50 hoàn tiền.</summary>
public class EventLifecycleApiClient : ApiClientBase
{
    public EventLifecycleApiClient(HttpClient http) : base(http) { }

    public Task<EventLifecycleResultDto> PostponeAsync(int eventId, PostponeEventRequest request, CancellationToken ct = default) =>
        PostAsync<EventLifecycleResultDto>($"api/events/{eventId}/postpone", request, ct);

    public Task<EventLifecycleResultDto> CancelAsync(int eventId, CancelEventRequest request, CancellationToken ct = default) =>
        PostAsync<EventLifecycleResultDto>($"api/events/{eventId}/cancel", request, ct);

    public Task<List<RefundDto>> GetRefundsAsync(int eventId, CancellationToken ct = default) =>
        GetAsync<List<RefundDto>>($"api/events/{eventId}/refunds", ct);

    public Task<RefundDto> CompleteRefundAsync(int refundId, CompleteRefundRequest request, CancellationToken ct = default) =>
        PostAsync<RefundDto>($"api/refunds/{refundId}/complete", request, ct);

    public Task<RefundDto> RejectRefundAsync(int refundId, RejectRefundRequest request, CancellationToken ct = default) =>
        PostAsync<RefundDto>($"api/refunds/{refundId}/reject", request, ct);

    public Task<List<RefundDto>> GetMyRefundsAsync(CancellationToken ct = default) => GetAsync<List<RefundDto>>("api/refunds/me", ct);

    public Task<RefundDto> UpdateBankInfoAsync(int refundId, UpdateRefundBankInfoRequest request, CancellationToken ct = default) =>
        PutAsync<RefundDto>($"api/refunds/{refundId}/bank-info", request, ct);
}
