using AlisverisSitesiFinal;
using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
// Yerelleştirme
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuGet.Packaging;
using QuestPDF.Infrastructure;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Konfig dosyalarını yükle
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<ReferralConfig>(builder.Configuration.GetSection("ReferralConfig"));

// ---- DB: TEK KAYIT ----
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection yok.");
builder.Services.AddDbContext<UygulamaDbContext>(opt => opt.UseSqlServer(connStr));

// Program.cs veya Startup.cs -> ConfigureServices eşleniği
builder.Services.AddScoped<ReferralBonusHandler>();

//payTR
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.SlidingExpiration = true;
});

builder.Services.Configure<PayTROptions>(builder.Configuration.GetSection("PayTR"));
builder.Services.AddHttpClient();
builder.Services.AddScoped<PayTRService>();
builder.Services.Configure<KpsOptions>(builder.Configuration.GetSection("KPS"));
builder.Services.AddHttpClient<IKpsPublicService, KpsPublicService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Identity
builder.Services.AddDefaultIdentity<Kullanici>(o =>
{
    o.SignIn.RequireConfirmedAccount = false;
    o.SignIn.RequireConfirmedEmail = false;
    o.Password.RequireDigit = false;
    o.Password.RequireLowercase = false;
    o.Password.RequireNonAlphanumeric = false;
    o.Password.RequireUppercase = false;
    o.Password.RequiredLength = 3;
    o.User.RequireUniqueEmail = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<UygulamaDbContext>()
.AddErrorDescriber<TurkishIdentityErrorDescriber>() // Türkçe metinler
.AddDefaultTokenProviders()
.AddDefaultUI();

// DI: PDF servisini ekle
builder.Services.AddScoped<InvoicePdfService>();

// Servisler
builder.Services.Configure<GoogleReCaptchaSettings>(
    builder.Configuration.GetSection("GoogleReCaptcha"));
builder.Services.AddHttpClient();
builder.Services.AddScoped<GoogleReCaptchaService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50MB
});
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddTransient<IOrderNotificationService, OrderNotificationService>();

// Yetkiler
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("SellerOnly", p => p.RequireRole("Satici"));
    options.AddPolicy("CanPurchase", policy => policy.RequireAssertion(ctx =>
        ctx.User?.Identity?.IsAuthenticated == true &&
        !ctx.User.IsInRole("Admin") &&
        !ctx.User.IsInRole("Satici") &&
        !ctx.User.IsInRole("SaticiAday")));
});
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);

// Yerelleştirme Servisleri (Views + DataAnnotations)
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(SharedResource));
    });

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new TranslateModelStateErrorsPageFilter());
});

builder.Services.AddRazorPages();

var app = builder.Build();

// tr-TR varsayılan kültür
var supportedCultures = new[] { new CultureInfo("tr-TR") };
var locOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("tr-TR"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};

app.UseRequestLocalization(locOptions);

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseMigrationsEndPoint();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ---- MIGRATION: DÜŞÜRMEYECEK ŞEKİLDE ----
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
    db.Database.Migrate();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "DB migrate hata verdi.");
}

app.Run();
