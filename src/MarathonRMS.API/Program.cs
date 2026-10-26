using System.Security.Claims;
using System.Text;
using MarathonRMS.API.Infrastructure;
using MarathonRMS.BusinessObjects.Data;
using MarathonRMS.BusinessObjects.Entities;
using MarathonRMS.DTOs.Common;
using MarathonRMS.Services;
using MarathonRMS.Services.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Database ----------
builder.Services.AddDbContext<MarathonDbContext>(options =>
    options.UseSqlServer(config.GetConnectionString("MarathonDb")));

// ---------- Identity (API là nơi duy nhất lưu tài khoản – D3) ----------
builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<MarathonDbContext>()
    .AddSignInManager();

// ---------- JWT ----------
var jwt = config.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
    throw new InvalidOperationException(
        "Thiếu Jwt:SigningKey (≥ 32 ký tự). Chạy: dotnet user-secrets set \"Jwt:SigningKey\" \"<chuỗi ngẫu nhiên>\" --project src/MarathonRMS.API");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = ClaimTypes.Role
        };
        // Tài khoản bị khóa (UC-11, UC-14) mất quyền ngay ở request kế tiếp, không chờ JWT hết hạn
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
                var user = userId is null ? null : await users.FindByIdAsync(userId);
                if (user is null || !user.IsActive)
                    context.Fail("Tài khoản không còn hoạt động.");
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AppPolicies.SuperAdminOnly, p => p.RequireRole(AppRoles.SuperAdmin))
    .AddPolicy(AppPolicies.AdminOrAbove, p => p.RequireRole(AppRoles.Admin, AppRoles.SuperAdmin))
    .AddPolicy(AppPolicies.OrganizerOnly, p => p.RequireRole(AppRoles.Organizer))
    .AddPolicy(AppPolicies.StaffOrOrganizer, p => p.RequireRole(AppRoles.Staff, AppRoles.Organizer))
    .AddPolicy(AppPolicies.MemberOnly, p => p.RequireRole(AppRoles.Member));

// ---------- Tầng nghiệp vụ ----------
builder.Services.AddMarathonServices(config);

// ---------- Web API + OData ----------
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers()
    // EDM model và route "odata" được thêm ở Giai đoạn 1 (Events, Registrations, Results…)
    .AddOData(options => options.Select().Filter().OrderBy().Expand().Count().SetMaxTop(100));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Marathon Race Management System API", Version = "v1" });
    var xml = Path.Combine(AppContext.BaseDirectory, "MarathonRMS.API.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Dán access token lấy từ POST /api/auth/login"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await DbInitializer.InitializeAsync(app.Services, config, app.Logger);

app.Run();
