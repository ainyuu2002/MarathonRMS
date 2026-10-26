using MarathonRMS.DTOs.Common;
using MarathonRMS.WebApp.ApiClients;
using MarathonRMS.WebApp.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
                 ?? throw new InvalidOperationException("Thiếu cấu hình Api:BaseUrl.");

// ---------- Đăng nhập bằng cookie; JWT của API cất trong cookie (D3) ----------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "MarathonRMS.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = false; // hết hạn cùng JWT
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AppPolicies.SuperAdminOnly, p => p.RequireRole(AppRoles.SuperAdmin))
    .AddPolicy(AppPolicies.AdminOrAbove, p => p.RequireRole(AppRoles.Admin, AppRoles.SuperAdmin))
    .AddPolicy(AppPolicies.OrganizerOnly, p => p.RequireRole(AppRoles.Organizer))
    .AddPolicy(AppPolicies.StaffOrOrganizer, p => p.RequireRole(AppRoles.Staff, AppRoles.Organizer))
    .AddPolicy(AppPolicies.MemberOnly, p => p.RequireRole(AppRoles.Member));

// ---------- Phân quyền theo thư mục trang (BA mục 7.3) ----------
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/SuperAdmin", AppPolicies.SuperAdminOnly);
    options.Conventions.AuthorizeFolder("/Admin", AppPolicies.AdminOrAbove);
    options.Conventions.AuthorizeFolder("/Organizer", AppPolicies.OrganizerOnly);
    options.Conventions.AuthorizeFolder("/Staff", AppPolicies.StaffOrOrganizer);
    options.Conventions.AuthorizeFolder("/Member", AppPolicies.MemberOnly);
    options.Conventions.AuthorizePage("/Account/Logout");
});

// ---------- Gọi API: typed HttpClient + tự gắn JWT ----------
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<JwtForwardingHandler>();

void AddApiClient<TClient>() where TClient : class =>
    builder.Services.AddHttpClient<TClient>(c => c.BaseAddress = new Uri(apiBaseUrl))
        .AddHttpMessageHandler<JwtForwardingHandler>();

AddApiClient<AuthApiClient>();
AddApiClient<LegalApiClient>();
// Phần A
AddApiClient<ProfileApiClient>();
AddApiClient<OrganizationApiClient>();
AddApiClient<VenueApiClient>();
AddApiClient<AdminApiClient>();
AddApiClient<SuperAdminApiClient>();
// Phần B

// Phần C


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
