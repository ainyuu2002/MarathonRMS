using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;

namespace MarathonRMS.Services.Registrations;

public class RefundService : IRefundService
{
    private readonly IRefundRepository _refunds; private readonly IEventRepository _events; private readonly IOrganizationScope _scope;
    private readonly ICurrentUserService _user; private readonly IDateTimeProvider _clock; private readonly IAuditLogger _audit; private readonly IUnitOfWork _uow;
    public RefundService(IRefundRepository refunds,IEventRepository events,IOrganizationScope scope,ICurrentUserService user,IDateTimeProvider clock,IAuditLogger audit,IUnitOfWork uow)
    { _refunds=refunds;_events=events;_scope=scope;_user=user;_clock=clock;_audit=audit;_uow=uow; }
    public async Task<IReadOnlyList<RefundDto>> GetByEventAsync(int eventId,CancellationToken ct=default)
    { var ev=await _events.GetTrackedAsync(eventId,ct)??throw new NotFoundException("Không tìm thấy giải.");_scope.EnsureCanManage(ev.OrganizationId);return (await _refunds.GetByEventAsync(eventId,ct)).Select(Map).ToList(); }
    public Task CompleteAsync(int id,CompleteRefundRequest request,CancellationToken ct=default)=>ProcessAsync(id,true,request.TransactionRef,null,ct);
    public Task RejectAsync(int id,RejectRefundRequest request,CancellationToken ct=default)=>ProcessAsync(id,false,null,request.Reason,ct);
    private async Task ProcessAsync(int id,bool complete,string? transactionRef,string? reason,CancellationToken ct)
    { await using var tx=await _uow.BeginTransactionAsync(ct);await _uow.LockAsync($"mrms:refund:{id}",ct);var r=await _refunds.GetTrackedAsync(id,ct)??throw new NotFoundException("Không tìm thấy yêu cầu hoàn tiền.");_scope.EnsureCanManage(r.Registration.Event.OrganizationId);await _scope.EnsureOrganizationActiveAsync(r.Registration.Event.OrganizationId,ct);if(r.Status!=RefundStatus.Pending)throw new BusinessRuleException("BR-15","Yêu cầu hoàn tiền đã được xử lý.");r.Status=complete?RefundStatus.Completed:RefundStatus.Rejected;r.ProcessedAt=_clock.Now;r.ProcessedById=_user.UserId;r.TransactionRef=complete?transactionRef!.Trim():null;r.BankInfo=null;r.Registration.Status=complete?RegistrationStatus.Refunded:RegistrationStatus.Cancelled;r.Registration.CancelledAt=complete?null:_clock.Now;await _audit.LogAsync(complete?"Refund.Complete":"Refund.Reject",nameof(Refund),id.ToString(),null,new{r.Status,r.TransactionRef,Reason=reason},ct);await _uow.SaveChangesAsync(ct);await tx.CommitAsync(ct); }
    private static RefundDto Map(Refund r)=>new(){Id=r.RefundId,RegistrationId=r.RegistrationId,EventId=r.Registration.EventId,RunnerName=r.Registration.FullName,Email=r.Registration.Email,Amount=r.Amount,Reason=r.Reason.ToString(),Status=r.Status.ToString(),BankInfo=r.BankInfo,ProcessedAt=r.ProcessedAt,TransactionRef=r.TransactionRef};
}
