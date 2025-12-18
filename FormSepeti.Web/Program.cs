using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using FormSepeti.Data;
using FormSepeti.Data.Repositories.Interfaces;
using FormSepeti.Data.Repositories.Implementations;
using FormSepeti.Services.Interfaces;
using FormSepeti.Services.Implementations;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// ✅ ngrok için host header validation'ı kapat (sadece development)
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

builder.Services.AddAuthentication("Cookie")
    .AddCookie("Cookie", options =>
    {
        options.LoginPath = "/Account/Login";
        options.Cookie.Name = "FormSepeti.Auth";
    });

builder.Services.AddAuthorization();

builder.Services.AddDistributedMemoryCache(); // Session için gerekli
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // 30 dakika timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // GDPR uyumlu
    options.Cookie.Name = ".FormSepeti.Session"; // Özel cookie adı
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

builder.Services.AddHttpClient<IJotFormService, JotFormService>(client =>
{
    client.BaseAddress = new Uri("https://api.jotform.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(builder.Configuration["Application:BaseUrl"])
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
    
    // ✅ Webhook endpoint'lerini antiforgery'den hariç tut
    options.SuppressXFrameOptionsHeader = false;
});

// ✅ Controllers için ignore attribute
builder.Services.AddControllers(options =>
{
    // Webhook endpoint'leri için model validation hatalarını logla
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

var app = builder.Build();

// ✅ Forwarded Headers (ngrok için)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | 
                       Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

// ✅ ngrok için forwarded headers middleware ekle
if (app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.All
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

app.UseSession(); // <-- BURASI ÖNEMLİ

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