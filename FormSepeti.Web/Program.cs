using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using FormSepeti.Data;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Data.Repositories.Implementations;
using FormSepeti.Services.Interfaces;
using FormSepeti.Services.Implementations;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.OAuth; // ✅ BU SATIRI EKLEYİN
using FormSepeti.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.AllowSynchronousIO = true;
    });
}

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var conn = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(opts =>
    opts.UseSqlServer(conn));

// ✅ Authentication & Authorization - GÜVENLİK GÜÇLENDİRMESİ
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    
    // ✅ GÜVENLIK AYARLARI
    options.Cookie.Name = ".FormSepeti.Auth";
    options.Cookie.HttpOnly = true;  // ✅ JavaScript erişimini engelle (XSS koruması)
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // ✅ Sadece HTTPS
    options.Cookie.SameSite = SameSiteMode.Lax;  // ✅ CSRF koruması (Strict yerine Lax - Google callback için)
    options.Cookie.IsEssential = true;
    
    // ✅ OTURUM SÜRESİ - 30 gün → 8 saat
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;  // Aktif kullanımda süre yenilenir
    
    // ✅ LOGOUT SONRASI YÖNLENDİRME
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            context.Response.Redirect(options.LoginPath);
            return Task.CompletedTask;
        }
    };
})
.AddCookie("AdminScheme", options =>
{
    options.LoginPath = "/Admin/Account/Login";
    options.LogoutPath = "/Admin/Account/Logout";
    options.AccessDeniedPath = "/Admin/Account/AccessDenied";
    
    // ✅ ADMIN GÜVENLİK AYARLARI
    options.Cookie.Name = ".FormSepeti.Admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;  // Admin için daha katı
    options.Cookie.IsEssential = true;
    
    // ✅ ADMIN OTURUM SÜRESİ - Daha kısa (4 saat)
    options.ExpireTimeSpan = TimeSpan.FromHours(4);
    options.SlidingExpiration = true;
    
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            // Admin area için özel yönlendirme
            if (context.Request.Path.StartsWithSegments("/Admin"))
            {
                context.Response.Redirect("/Admin/Account/Login");
            }
            else
            {
                context.Response.StatusCode = 401;
            }
            return Task.CompletedTask;
        }
    };
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Google:ClientId"] 
        ?? throw new InvalidOperationException("Google ClientId is missing");
    options.ClientSecret = builder.Configuration["Google:ClientSecret"] 
        ?? throw new InvalidOperationException("Google ClientSecret is missing");
    options.CallbackPath = "/signin-google";
    
    // ✅ SCOPELARI EKLE
    options.Scope.Add("https://www.googleapis.com/auth/spreadsheets");
    options.Scope.Add("https://www.googleapis.com/auth/drive.file");
    options.Scope.Add("email");
    options.Scope.Add("profile"); // ✅ Profil bilgisi için (zaten var muhtemelen)
    
    options.AccessType = "offline";
    options.SaveTokens = true;
    
    // ✅ CLAIMS'E PHOTO URL EKLE
    options.ClaimActions.MapJsonKey("urn:google:picture", "picture");
    options.ClaimActions.MapJsonKey("urn:google:name", "name");
    
    options.Events.OnCreatingTicket = context =>
    {
        var tokens = context.Properties.GetTokens().ToList();
        
        if (!string.IsNullOrEmpty(context.AccessToken))
        {
            tokens.Add(new AuthenticationToken
            {
                Name = "access_token",
                Value = context.AccessToken
            });
        }
        
        if (!string.IsNullOrEmpty(context.RefreshToken))
        {
            tokens.Add(new AuthenticationToken
            {
                Name = "refresh_token",
                Value = context.RefreshToken
            });
        }
        
        context.Properties.StoreTokens(tokens);
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    // Admin policy - sadece admin cookie ile
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.AuthenticationSchemes.Add("AdminScheme");
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("Role", "SuperAdmin", "Admin", "Editor");
    });

    // SuperAdmin policy
    options.AddPolicy("SuperAdminOnly", policy =>
    {
        policy.AuthenticationSchemes.Add("AdminScheme");
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("Role", "SuperAdmin");
    });
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".FormSepeti.Session";
});

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserGoogleSheetsRepository, UserGoogleSheetsRepository>();
builder.Services.AddScoped<IFormRepository, FormRepository>();
builder.Services.AddScoped<IFormGroupRepository, FormGroupRepository>();
builder.Services.AddScoped<IFormGroupMappingRepository, FormGroupMappingRepository>();
builder.Services.AddScoped<IPackageRepository, PackageRepository>();
builder.Services.AddScoped<IUserPackageRepository, UserPackageRepository>();
builder.Services.AddScoped<IFormSubmissionRepository, FormSubmissionRepository>();
builder.Services.AddScoped<IEmailLogRepository, EmailLogRepository>();

// Admin Repositories
builder.Services.AddScoped<IAdminUserRepository, AdminUserRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();

// Services
builder.Services.AddScoped<IGoogleSheetsService, GoogleSheetsService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IJotFormService, JotFormService>();
builder.Services.AddScoped<IIyzicoPaymentService, IyzicoPaymentService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPackageService, PackageService>();
builder.Services.AddScoped<IFormService, FormService>();
builder.Services.AddScoped<ILoginAttemptService, LoginAttemptService>(); // Buraya ekledim
// ✅ YENİ - Rate Limiting Servisi (Singleton olmalı - uygulama boyunca tek instance)
builder.Services.AddSingleton<ILoginAttemptService, LoginAttemptService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

builder.Services.AddHttpClient<IJotFormService, JotFormService>(client =>
{
    var baseUrl = builder.Configuration["JotForm:ApiBaseUrl"] ?? "https://panel.kolaytik.com/API";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        var baseUrl = builder.Configuration["Application:BaseUrl"] ?? "https://localhost:7099";
        policy.WithOrigins(baseUrl)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.SuppressXFrameOptionsHeader = false;
});

builder.Services.AddControllers(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.All
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

// ✅ YENİ: Token auto-refresh middleware
app.UseGoogleTokenRefresh();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "admin",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "api",
    pattern: "api/{controller}/{action}/{id?}");

app.MapRazorPages();

if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            if (dbContext.Database.GetPendingMigrations().Any())
            {
                dbContext.Database.Migrate();
                Console.WriteLine("✓ Database migrations applied successfully");
            }
            
            // ✅ YENİ: Admin seeder'ı çalıştır
            await FormSepeti.Web.Data.AdminSeeder.SeedDefaultAdminAsync(dbContext);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Database migration error: {ex.Message}");
        }
    }
}

// ✅ YENİ: LoginAttemptService cleanup timer
var loginAttemptService = app.Services.GetRequiredService<ILoginAttemptService>();
var timer = new System.Threading.Timer(_ =>
{
    if (loginAttemptService is LoginAttemptService service)
    {
        service.CleanupExpiredAttempts();
    }
}, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));

app.Run();