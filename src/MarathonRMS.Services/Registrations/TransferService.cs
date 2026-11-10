using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.BusinessObjects.Enums;
using MarathonRMS.DTOs.Common;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.Repositories;
using MarathonRMS.Services.Common;
using Microsoft.AspNetCore.Identity;

namespace MarathonRMS.Services.Registrations;

public class TransferService : ITransferService
{
    private readonly IRegistrationRepository _registrations; private readonly IBibTransferRepository _transfers;
    private readonly IPaymentRepository _payments; private readonly IParticipantRepository _participants;
    private readonly IConsentRecordRepository _consents; private readonly UserManager<AppUser> _users;
    private readonly ICurrentUserService _current; private readonly ISystemSettingsProvider _settings;
    private readonly IDateTimeProvider _clock; private readonly IAuditLogger _audit; private readonly IUnitOfWork _uow;
    public TransferService(IRegistrationRepository registrations, IBibTransferRepository transfers, IPaymentRepository payments,
        IParticipantRepository participants, IConsentRecordRepository consents, UserManager<AppUser> users,
        ICurrentUserService current, ISystemSettingsProvider settings, IDateTimeProvider clock, IAuditLogger audit, IUnitOfWork uow)
    { _registrations=registrations; _transfers=transfers; _payments=payments; _participants=participants; _consents=consents; _users=users; _current=current; _settings=settings; _clock=clock; _audit=audit; _uow=uow; }

    public async Task<TransferDto> CreateAsync(int registrationId, CreateTransferRequest request, CancellationToken ct = default)
    {
        var me = RequireUser(); await using var tx = await _uow.BeginTransactionAsync(ct); await _uow.LockAsync($"mrms:transfer-registration:{registrationId}", ct);
        var r = await _registrations.GetTrackedWithDetailsAsync(registrationId, ct) ?? throw new NotFoundException("Không tìm thấy đăng ký.");
        if (r.RegisteredByUserId != me) throw new ForbiddenException("Đăng ký không thuộc tài khoản của bạn.");
        ValidateTransferable(r);
        if (r.RaceKitPickup is not null) throw new BusinessRuleException("BR-16", "Không thể chuyển suất sau khi đã nhận race kit.");
        if (await _transfers.HasOpenAsync(registrationId, ct)) throw new BusinessRuleException("BR-16", "Đăng ký đã có yêu cầu chuyển suất đang xử lý.");
        var recipient = await _users.FindByEmailAsync(request.RecipientEmail.Trim()) ?? throw new NotFoundException("Không tìm thấy tài khoản người nhận.");
        if (!recipient.IsActive || recipient.Id == me || !await _users.IsInRoleAsync(recipient, AppRoles.Member)) throw new BusinessRuleException("BR-16", "Người nhận không hợp lệ.", 400);
        var participant = await RequireEligibleParticipant(recipient.Id, r, ct);
        var t = new BibTransfer { RegistrationId=r.RegistrationId, FromUserId=me, ToUserId=recipient.Id, ToParticipantId=participant.ParticipantId, Status=TransferStatus.Pending, FeeAmount=r.Event.Policy.TransferPolicy == TransferPolicy.Paid ? r.Event.Policy.TransferFee : 0, RequestedAt=_clock.Now };
        await _transfers.AddAsync(t, ct); await _uow.SaveChangesAsync(ct); await _audit.LogAsync("Transfer.Create", nameof(BibTransfer), t.TransferId.ToString(), null, new { t.RegistrationId, t.ToUserId, t.FeeAmount }, ct); await _uow.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await MapAsync(t, me);
    }

    public async Task<IReadOnlyList<TransferDto>> GetIncomingAsync(CancellationToken ct = default) { var me=RequireUser(); var rows=await _transfers.GetIncomingAsync(me,ct); return await MapAsync(rows,me); }
    public async Task<IReadOnlyList<TransferDto>> GetOutgoingAsync(CancellationToken ct = default) { var me=RequireUser(); var rows=await _transfers.GetOutgoingAsync(me,ct); return await MapAsync(rows,me); }

    public async Task<TransferDto> AcceptAsync(int transferId, AcceptTransferRequest request, CancellationToken ct = default)
    {
        var me=RequireUser(); if (!request.WaiverAccepted || !request.HealthDeclarationAccepted) throw new BusinessRuleException("BR-09","Phải chấp nhận miễn trừ và cam kết sức khỏe.",400);
        await using var tx=await _uow.BeginTransactionAsync(ct); await _uow.LockAsync($"mrms:transfer:{transferId}",ct);
        var t=await _transfers.GetTrackedAsync(transferId,ct) ?? throw new NotFoundException("Không tìm thấy yêu cầu chuyển suất.");
        if(t.ToUserId!=me) throw new ForbiddenException("Bạn không phải người nhận suất này."); if(t.Status!=TransferStatus.Pending) throw new BusinessRuleException("BR-16","Yêu cầu không còn chờ xử lý.");
        var r=t.Registration; ValidateTransferable(r); var recipient=await _users.FindByIdAsync(me);
        if(recipient is null||!recipient.IsActive||!await _users.IsInRoleAsync(recipient,AppRoles.Member)) throw new BusinessRuleException("BR-16","Tài khoản người nhận không còn hợp lệ.",400);
        var p=await RequireEligibleParticipant(me,r,ct);
        if ((!string.IsNullOrWhiteSpace(p.BloodType)||!string.IsNullOrWhiteSpace(p.MedicalNotes))&&!request.HealthDataConsent) throw new BusinessRuleException("BR-09","Phải đồng ý xử lý dữ liệu sức khỏe.",400);
        if(t.FeeAmount>0)
        {
            if(!request.Provider.Equals("Mock",StringComparison.OrdinalIgnoreCase)||!await _settings.GetBoolAsync("Payment.MockEnabled",true,ct)) throw new BusinessRuleException("PAYMENT","Phương thức thanh toán chưa được hỗ trợ.",400);
            await _payments.AddAsync(new Payment { RegistrationId=r.RegistrationId, Purpose=PaymentPurpose.TransferFee, Provider=PaymentProvider.Mock, Amount=t.FeeAmount, Status=PaymentStatus.Success, TxnRef=$"MOCK-TF-{Guid.NewGuid():N}", ProviderTransactionNo=Guid.NewGuid().ToString("N"), ResponseCode="00", CreatedAt=_clock.Now, PaidAt=_clock.Now },ct);
        }
        r.Status=RegistrationStatus.Transferred; r.TransferCount++; r.ExpiresAt=null; await _uow.SaveChangesAsync(ct);
        var n=CopyRegistration(r,p,me,recipient.Email,request,_clock.Now); await _registrations.AddAsync(n,ct); await _uow.SaveChangesAsync(ct);
        foreach(var c in BuildConsents(n,me,request,r.Event.WaiverVersion,_clock.Now)) await _consents.AddAsync(c,ct);
        t.Status=TransferStatus.Accepted; t.NewRegistrationId=n.RegistrationId; t.RespondedAt=_clock.Now;
        await _audit.LogAsync("Transfer.Accept",nameof(BibTransfer),t.TransferId.ToString(),null,new { t.NewRegistrationId,t.FeeAmount },ct); await _uow.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await MapAsync(t,me);
    }

    public Task RejectAsync(int id,CancellationToken ct=default)=>CloseAsync(id,TransferStatus.Rejected,true,ct);
    public Task CancelAsync(int id,CancellationToken ct=default)=>CloseAsync(id,TransferStatus.Cancelled,false,ct);
    public async Task<int> ExpireOverdueAsync(CancellationToken ct=default) { var rows=await _transfers.GetOverdueAsync(_clock.Now,ct); foreach(var t in rows){t.Status=TransferStatus.Expired;t.RespondedAt=_clock.Now;} if(rows.Count>0) await _uow.SaveChangesAsync(ct); return rows.Count; }

    private async Task CloseAsync(int id,TransferStatus status,bool recipient,CancellationToken ct)
    { var me=RequireUser(); await using var tx=await _uow.BeginTransactionAsync(ct); await _uow.LockAsync($"mrms:transfer:{id}",ct); var t=await _transfers.GetTrackedAsync(id,ct)??throw new NotFoundException("Không tìm thấy yêu cầu chuyển suất."); if((recipient?t.ToUserId:t.FromUserId)!=me) throw new ForbiddenException("Bạn không có quyền xử lý yêu cầu này."); if(t.Status!=TransferStatus.Pending) throw new BusinessRuleException("BR-16","Yêu cầu không còn chờ xử lý."); t.Status=status;t.RespondedAt=_clock.Now;await _audit.LogAsync($"Transfer.{status}",nameof(BibTransfer),id.ToString(),null,null,ct);await _uow.SaveChangesAsync(ct);await tx.CommitAsync(ct); }

    private void ValidateTransferable(Registration r)
    { var p=r.Event.Policy; if(r.Status!=RegistrationStatus.Confirmed||p.TransferPolicy==TransferPolicy.Forbidden||p.TransferDeadline is null||_clock.Now>=p.TransferDeadline||r.TransferCount>=p.MaxTransfers) throw new BusinessRuleException("BR-16","Đăng ký không còn đủ điều kiện chuyển suất."); if(r.EntryType!=EntryType.Public&&p.LockSpecialEntries) throw new BusinessRuleException("BR-18","Suất đặc biệt đã bị khóa chuyển nhượng."); }
    private async Task<Participant> RequireEligibleParticipant(string userId,Registration r,CancellationToken ct)
    { var p=await _participants.GetSelfAsync(userId,ct)??throw new BusinessRuleException("BR-16","Người nhận chưa có hồ sơ chính chủ.",400); if(p.IsDeleted||p.DateOfBirth is null||p.Gender is null||p.TShirtSize is null||string.IsNullOrWhiteSpace(p.FullName)||string.IsNullOrWhiteSpace(p.Phone)||string.IsNullOrWhiteSpace(p.Nationality)||string.IsNullOrWhiteSpace(p.EmergencyContactName)||string.IsNullOrWhiteSpace(p.EmergencyContactPhone)) throw new BusinessRuleException("BR-16","Hồ sơ người nhận chưa đầy đủ.",400); var age=r.Event.Policy.AgeCalculationMode==AgeCalculationMode.CalendarYear?r.Event.EventDate.Year-p.DateOfBirth.Value.Year:AgeCalculator.AgeAt(p.DateOfBirth.Value,r.Event.EventDate); if(age<r.Category.MinAge||r.Category.MaxAge is int max&&age>max) throw new BusinessRuleException("BR-05","Người nhận không đủ điều kiện tuổi."); if(age<r.Event.Policy.GuardianConsentUnderAge||r.Category.EscortRequiredUnderAge is int escort&&age<escort) throw new BusinessRuleException("BR-06","Luồng chuyển suất chưa hỗ trợ hồ sơ cần giám hộ/chạy kèm."); if(await _registrations.HasActiveForParticipantAsync(r.EventId,p.ParticipantId,ct)||await _registrations.HasActiveForIdAsync(r.EventId,p.IdType,p.IdNumber,ct)) throw new BusinessRuleException("BR-01","Người nhận đã có đăng ký còn hiệu lực tại giải."); return p; }
    private static Registration CopyRegistration(Registration r,Participant p,string userId,string? email,AcceptTransferRequest q,DateTime now)=>new(){EventId=r.EventId,CategoryId=r.CategoryId,ParticipantId=p.ParticipantId,RegisteredByUserId=userId,EntryType=r.EntryType,Status=RegistrationStatus.Confirmed,BibNumber=r.BibNumber,OriginalPrice=r.OriginalPrice,DiscountAmount=r.DiscountAmount,CoinDiscountAmount=r.CoinDiscountAmount,FinalPrice=r.FinalPrice,QrToken=Guid.NewGuid().ToString("N"),RegisteredAt=now,ConfirmedAt=now,TransferCount=r.TransferCount,HideFromPublicList=q.HideFromPublicList,FullName=p.FullName,BibName=p.FullName.Length>16?p.FullName[..16]:p.FullName,DateOfBirth=p.DateOfBirth!.Value,Gender=p.Gender!.Value,IdType=p.IdType,IdNumber=p.IdNumber,Nationality=p.Nationality!,Phone=p.Phone!,Email=email,TShirtSize=p.TShirtSize!.Value,EmergencyContactName=p.EmergencyContactName!,EmergencyContactPhone=p.EmergencyContactPhone!,BloodType=p.BloodType,MedicalNotes=p.MedicalNotes,ClubName=p.ClubName,WaiverAccepted=true,HealthDeclarationAccepted=true};
    private static IEnumerable<ConsentRecord> BuildConsents(Registration r,string userId,AcceptTransferRequest q,int waiverVersion,DateTime now){yield return New(ConsentType.Waiver,true,waiverVersion);yield return New(ConsentType.HealthData,q.HealthDataConsent,1);yield return New(ConsentType.PhotoMarketing,q.PhotoMarketingConsent,1);yield return New(ConsentType.PublicListing,!q.HideFromPublicList,1); ConsentRecord New(ConsentType type,bool granted,int version)=>new(){UserId=userId,RegistrationId=r.RegistrationId,ConsentType=type,DocumentVersion=version,Granted=granted,CreatedAt=now};}
    private string RequireUser()=>_current.UserId??throw new ForbiddenException("Bạn cần đăng nhập.");
    private async Task<IReadOnlyList<TransferDto>> MapAsync(IEnumerable<BibTransfer> rows,string me){var list=new List<TransferDto>();foreach(var x in rows)list.Add(await MapAsync(x,me));return list;}
    private async Task<TransferDto> MapAsync(BibTransfer x,string me){var from=await _users.FindByIdAsync(x.FromUserId);var to=await _users.FindByIdAsync(x.ToUserId);return new(){Id=x.TransferId,RegistrationId=x.RegistrationId,EventName=x.Registration.Event.Name,CategoryName=x.Registration.Category.Name,BibNumber=x.Registration.BibNumber,FromEmail=from?.Email??"",ToEmail=to?.Email??"",Status=x.Status.ToString(),FeeAmount=x.FeeAmount,NewRegistrationId=x.NewRegistrationId,RequestedAt=x.RequestedAt,RespondedAt=x.RespondedAt,CanAccept=x.Status==TransferStatus.Pending&&x.ToUserId==me,CanReject=x.Status==TransferStatus.Pending&&x.ToUserId==me,CanCancel=x.Status==TransferStatus.Pending&&x.FromUserId==me,CanPay=false};}
}
