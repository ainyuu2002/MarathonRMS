using MarathonRMS.DTOs.Events;
using MarathonRMS.DTOs.Registrations;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace MarathonRMS.WebApp.Pages.Member;

public class RegistrationChangesModel : BasePageModel
{
    private readonly RegistrationApiClient _registrations; private readonly EventApiClient _events;
    public RegistrationChangesModel(RegistrationApiClient registrations,EventApiClient events){_registrations=registrations;_events=events;}
    [BindProperty(SupportsGet=true)] public int Id {get;set;}
    [BindProperty] public int TargetCategoryId {get;set;}
    [BindProperty] public string RecipientEmail {get;set;}="";
    public MyRegistrationDto? Registration {get;private set;} public List<RaceCategoryDto> Categories {get;private set;}=new();
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)=>await LoadAsync(ct);
    public async Task<IActionResult> OnPostChangeCategoryAsync(CancellationToken ct)
    { try{await _registrations.ChangeCategoryAsync(Id,new(){TargetCategoryId=TargetCategoryId},ct);StatusMessage="Đã đổi cự ly thành công.";return RedirectToPage("/Member/Registrations");}catch(ApiException ex){return await HandleApiErrorAsync(ex)??await LoadAsync(ct);} }
    public async Task<IActionResult> OnPostTransferAsync(CancellationToken ct)
    { try{await _registrations.CreateTransferAsync(Id,new(){RecipientEmail=RecipientEmail},ct);StatusMessage="Đã gửi yêu cầu chuyển suất.";return RedirectToPage("/Member/Transfers");}catch(ApiException ex){return await HandleApiErrorAsync(ex)??await LoadAsync(ct);} }
    private async Task<IActionResult> LoadAsync(CancellationToken ct)
    { try{Registration=(await _registrations.GetMineAsync(ct)).FirstOrDefault(x=>x.Id==Id);if(Registration is null)return NotFound();Categories=(await _events.GetCategoriesAsync(Registration.EventId,ct)).Where(x=>x.Id!=Registration.CategoryId).ToList();return Page();}catch(ApiException ex){return await HandleApiErrorAsync(ex)??Page();} }
}
