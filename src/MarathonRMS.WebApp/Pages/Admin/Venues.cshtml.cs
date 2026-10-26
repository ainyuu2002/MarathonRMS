using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Pages.Shared;

namespace MarathonRMS.WebApp.Pages.Admin;

public class VenuesModel : VenuesPageModel
{
    public VenuesModel(VenueApiClient venues) : base(venues) { }
}
