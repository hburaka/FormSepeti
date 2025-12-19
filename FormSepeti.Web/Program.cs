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
.AddCookie(options =>
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
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Google:ClientId"] 
        ?? throw new InvalidOperationException("Google ClientId is missing");
    options.ClientSecret = builder.Configuration["Google:ClientSecret"] 
        ?? throw new InvalidOperationException("Google ClientSecret is missing");
    options.CallbackPath = "/signin-google";
    
    options.Scope.Add("https://www.googleapis.com/auth/spreadsheets");
    options.Scope.Add("https://www.googleapis.com/auth/drive.file");
    options.Scope.Add("email");
    options.Scope.Add("profile");
    
    options.AccessType = "offline";
    options.SaveTokens = true;
    
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

builder.Services.AddAuthorization();

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
builder.Services.AddSingleton<LoginAttemptService>();

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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Database migration error: {ex.Message}");
        }
    }
}

app.Run();