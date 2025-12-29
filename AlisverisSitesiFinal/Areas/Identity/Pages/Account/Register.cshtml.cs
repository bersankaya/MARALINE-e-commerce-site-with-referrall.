using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Services;
// using AlisverisSitesiFinal.Services; // SMS kullanmıyoruz
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace AlisverisSitesiFinal.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<Kullanici> _signInManager;
        private readonly UserManager<Kullanici> _userManager;
        private readonly IUserEmailStore<Kullanici> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        // private readonly ISmsSender _smsSender; // SMS kapalı
        private readonly UygulamaDbContext _context;
        private readonly ReferralConfig _config;
        private readonly IKpsPublicService _kps;
        private readonly IOptions<KpsOptions> _kpsOptions; // ← KPS ayarları
        private readonly RoleManager<IdentityRole> _roleManager;

        public RegisterModel(
            UserManager<Kullanici> userManager,
            IUserStore<Kullanici> userStore,
            SignInManager<Kullanici> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            // ISmsSender smsSender,   // SMS kapalı
            UygulamaDbContext context,
            IOptions<ReferralConfig> config,
            RoleManager<IdentityRole> roleManager, IKpsPublicService kps, IOptions<KpsOptions> kpsOptions)
        {
            _userManager = userManager;
            _emailStore = (IUserEmailStore<Kullanici>)userStore;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            // _smsSender = smsSender; // SMS kapalı
            _context = context;
            _config = config.Value;
            _roleManager = roleManager;
            _kps = kps;
            _kpsOptions = kpsOptions;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }
        public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();

        public class InputModel
        {
            [Required, Display(Name = "Ad")]
            public string Ad { get; set; } = string.Empty;

            [Required, Display(Name = "Soyad")]
            public string Soyad { get; set; } = string.Empty;

            [Required, EmailAddress, Display(Name = "E-posta")]
            public string Email { get; set; } = string.Empty;

            // E-posta tekrarı (server-side Compare da var, ekstra koruma altta)
            [Required, EmailAddress, Display(Name = "E-posta (tekrar)")]
            [Compare(nameof(Email), ErrorMessage = "E-posta adresleri aynı olmalıdır.")]
            public string EmailConfirm { get; set; } = string.Empty;

            //[Required, Display(Name = "TC Kimlik No")]
            //[RegularExpression(@"^[1-9][0-9]{9}[02468]$", ErrorMessage = "Geçerli bir TC Kimlik No girin.")]
            //public string TcKimlikNo { get; set; } = string.Empty;

            //[Required, Range(1900, 2100), Display(Name = "Doğum Yılı")]
            //public int? BirthYear { get; set; }

            [Required, Display(Name = "Telefon Numarası")]
            public string PhoneNumber { get; set; } = string.Empty;

            [Required, DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password), Display(Name = "Şifreyi Onayla")]
            [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [Required, Display(Name = "Referans Kodu")]
            public string ReferralCode { get; set; } = string.Empty;

            public string? TargetRole { get; set; }

            // ✅ SÖZLEŞME ONAYI (zorunlu)
            [Display(Name = "Maraline Kullanıcı Sözleşmesi ve Bonus Politikası’nı okudum, kabul ediyorum.")]
            [Range(typeof(bool), "true", "true", ErrorMessage = "Kaydı tamamlamak için sözleşmeyi kabul etmelisiniz.")]
            public bool AcceptTerms { get; set; }

            public string AcceptedPolicyVersion { get; set; } = "v1.0";
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        // ---- Telefon normalize ----
        private static string? NormalizeGsm(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var digits = Regex.Replace(raw, "[^0-9]", "");
            if (digits.StartsWith("90")) digits = digits[2..];
            if (!digits.StartsWith("0")) digits = "0" + digits;
            return (digits.Length == 11 && digits.StartsWith("05")) ? digits : null;
        }

        // ✅ TC Kimlik No algoritması
        private static bool ValidateTCKN(string? tc)
        {
            if (string.IsNullOrWhiteSpace(tc)) return false;
            if (tc.Length != 11) return false;
            if (!tc.All(char.IsDigit)) return false;
            if (tc[0] == '0') return false;

            int[] d = new int[11];
            for (int i = 0; i < 11; i++) d[i] = tc[i] - '0';

            int oddSum = d[0] + d[2] + d[4] + d[6] + d[8];
            int evenSum = d[1] + d[3] + d[5] + d[7];
            int digit10 = ((oddSum * 7) - evenSum) % 10;
            if (digit10 < 0) digit10 += 10;
            if (digit10 != d[9]) return false;

            int total10 = 0;
            for (int i = 0; i < 10; i++) total10 += d[i];
            int digit11 = total10 % 10;
            if (digit11 != d[10]) return false;

            if ((d[10] % 2) != 0) return false;

            return true;
        }

        // 🔎 Hataları üstte göstermek için
        private void DumpModelErrorsToTempData()
        {
            var all = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? e.Exception?.Message : e.ErrorMessage)
                .Where(s => !string.IsNullOrWhiteSpace(s));
            if (all.Any())
                TempData["ErrorMessage"] = string.Join("<br/>", all);
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            // 🔒 Kamuya açık kayıt sayfasında rolü zorla "Musteri" yap.
            // Yalnızca admin oturumu açıkken manuel olarak farklı rol verilebilir.
            bool adminOturum = User?.Identity?.IsAuthenticated == true && User.IsInRole("Admin");
            var hedefRol = adminOturum
                ? (string.IsNullOrWhiteSpace(Input.TargetRole) ? "Musteri" : Input.TargetRole!.Trim())
                : "Musteri";

            // E-posta tekrarını ek güvenlikle kontrol et
            if (!string.Equals(Input.Email?.Trim(), Input.EmailConfirm?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("Input.EmailConfirm", "E-posta adresleri aynı olmalıdır.");
            }

            // Müşteri ise TCKN & Doğum yılı zorunlu
            //if (hedefRol == "Musteri")
            //{
            //    if (string.IsNullOrWhiteSpace(Input.TcKimlikNo))
            //        ModelState.AddModelError("Input.TcKimlikNo", "TC Kimlik No zorunludur.");

            //    if (!Input.BirthYear.HasValue || Input.BirthYear.Value < 1900 || Input.BirthYear.Value > 2100)
            //        ModelState.AddModelError("Input.BirthYear", "Doğum yılı geçersiz.");
            //}

            ReturnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
           
            if (!ModelState.IsValid) { DumpModelErrorsToTempData(); return Page(); }

            // 1) Email tekillik
            var normalizedEmail = Input.Email!.Trim().ToUpperInvariant();
            if (await _userManager.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail))
            {
                ModelState.AddModelError("Input.Email", "Bu e-posta zaten kayıtlı.");
                DumpModelErrorsToTempData(); return Page();
            }

            // 2) Telefon normalize + tekillik (AspNetUsers.PhoneNumber)
            var phoneNorm = NormalizeGsm(Input.PhoneNumber);
            if (phoneNorm is null)
            {
                ModelState.AddModelError("Input.PhoneNumber", "Geçerli bir Türk GSM numarası girin (05XXXXXXXXX).");
                DumpModelErrorsToTempData(); return Page();
            }
            if (await _userManager.Users.AnyAsync(u => u.PhoneNumber == phoneNorm))
            {
                ModelState.AddModelError("Input.PhoneNumber", "Bu telefon numarası zaten kayıtlı.");
                DumpModelErrorsToTempData(); return Page();
            }

            // 3) TCKN + KPS (yalnızca Musteri)
            //string? tc = null;
            //if (hedefRol == "Musteri")
            //{
                //tc = Regex.Replace(Input.TcKimlikNo ?? "", "[^0-9]", "");
                //if (!ValidateTCKN(tc))
                //{
                //    ModelState.AddModelError("Input.TcKimlikNo", "TC Kimlik No geçersiz (algoritma doğrulamasından geçmedi).");
                //    DumpModelErrorsToTempData(); return Page();
                //}
                //if (await _userManager.Users.AnyAsync(u => u.TcKimlikNo == tc))
                //{
                //    ModelState.AddModelError("Input.TcKimlikNo", "Bu TC Kimlik numarası zaten kayıtlı.");
                //    DumpModelErrorsToTempData(); return Page();
                //}

                //bool kpsOk = false;
                //try
                //{
                //    kpsOk = await _kps.VerifyAsync(
                //        //tckn: long.Parse(tc),
                //        ad: Input.Ad?.Trim() ?? "",
                //        soyad: Input.Soyad?.Trim() ?? "");
                //    //dogumYili: Input.BirthYear!.Value
                //}
                //catch
                //{
                //    // ===== SOFT-FAIL: Servise ulaşılamazsa sadece uyarı ver, kayıt devam etsin =====
                //    if (_kpsOptions.Value.Enforce)
                //    {
                //        ModelState.AddModelError("Input.TcKimlikNo",
                //            "Kimlik doğrulama servisine şu anda ulaşılamıyor. Lütfen biraz sonra tekrar deneyin.");
                //        DumpModelErrorsToTempData(); return Page();
                //    }
                //    else
                //    {
                //        TempData["WarningMessage"] = "KPS doğrulaması şu an yapılamadı (servise ulaşılamadı). Kayıt tamamlandı.";
                //    }
                //}

                //// ===== SOFT-FAIL: KPS false dönerse Enforce=false ise kayıt devam etsin =====
                //if (!kpsOk && _kpsOptions.Value.Enforce)
                //{
                //    ModelState.AddModelError("Input.TcKimlikNo",
                //        "Kimlik bilgileri doğrulanamadı. Lütfen Ad, Soyad ve Doğum Yılı'nı TC ile uyumlu girin.");
                //    DumpModelErrorsToTempData(); return Page();
                //}
                //else if (!kpsOk && !_kpsOptions.Value.Enforce)
                //{
                //    TempData["WarningMessage"] = "KPS doğrulaması şu an yapılamadı/başarısız. Kayıt tamamlandı.";
                //}
            //}

            // 4) Sponsor (aktiflik + limit + pasif temizleme)
            var refCode = (Input.ReferralCode ?? "").Trim().ToUpperInvariant();
            var sponsor = await _userManager.Users
                .FirstOrDefaultAsync(u => u.ReferansKodu != null && u.ReferansKodu.ToUpper() == refCode);

            if (sponsor == null)
            {
                ModelState.AddModelError("Input.ReferralCode", "Geçersiz referans kodu.");
                DumpModelErrorsToTempData(); return Page();
            }


            bool isAdminSponsor = await _userManager.IsInRoleAsync(sponsor, "Admin");

            // ❗ Admin hariç: sponsor referans kodu aktif ve eşik (HasMetReferralThreshold) geçilmiş olmalı
            if (!isAdminSponsor)
            {
                if (!sponsor.IsReferralCodeActive || !sponsor.HasMetReferralThreshold)
                {
                    ModelState.AddModelError("Input.ReferralCode", "Bu referans kodu henüz aktif değil.");
                    DumpModelErrorsToTempData(); return Page();
                }
            }

            // Etkin limit: Özel > (Admin için AdminReferralLimit >0?) > Genel
            int sponsorLimit =
                (sponsor.OzelReferansLimiti.HasValue && sponsor.OzelReferansLimiti.Value > 0)
                    ? sponsor.OzelReferansLimiti.Value
                    : (isAdminSponsor
                        ? (_config.AdminReferralLimit > 0 ? _config.AdminReferralLimit : _config.ReferralLimit)
                        : _config.ReferralLimit);

            int toplamChildCount = await _context.Users.CountAsync(u => u.SponsorId == sponsor.Id);

            if (toplamChildCount >= sponsorLimit)
            {
                var oldestPassive = await _context.Users
                    .Where(u => u.SponsorId == sponsor.Id && !u.HasMetReferralThreshold)
                    .OrderBy(u => u.KayitTarihi)
                    .FirstOrDefaultAsync();

                if (oldestPassive != null)
                {
                    bool hasOrder = await _context.Siparisler.AnyAsync(s => s.IdentityUserId == oldestPassive.Id);
                    if (!hasOrder)
                    {
                        using var tx = await _context.Database.BeginTransactionAsync();
                        _context.Users.Remove(oldestPassive);
                        await _context.SaveChangesAsync();

                        toplamChildCount = await _context.Users.CountAsync(u => u.SponsorId == sponsor.Id);
                        if (toplamChildCount >= sponsorLimit)
                        {
                            await tx.RollbackAsync();
                            ModelState.AddModelError("Input.ReferralCode", "Bu referans kodu için referans limiti dolu.");
                            DumpModelErrorsToTempData(); return Page();
                        }
                        await tx.CommitAsync();
                    }
                    else
                    {
                        ModelState.AddModelError("Input.ReferralCode", "Bu referans kodu için referans limiti dolu.");
                        DumpModelErrorsToTempData(); return Page();
                    }
                }
                else
                {
                    ModelState.AddModelError("Input.ReferralCode", "Bu referans kodu için referans limiti dolu.");
                    DumpModelErrorsToTempData(); return Page();
                }
            }

            if (!Input.AcceptTerms)
            {
                ModelState.AddModelError(nameof(Input.AcceptTerms),
                    "Kaydı tamamlamak için sözleşmeyi kabul etmelisiniz.");
                DumpModelErrorsToTempData(); return Page();
            }

            if (!ModelState.IsValid)
            {
                DumpModelErrorsToTempData();
                return Page();
            }
            // 5) Kullanıcı oluştur
            var user = new Kullanici
            {
                UserName = Input.Email.Trim(),
                Email = Input.Email.Trim(),
                
                //TcKimlikNo = (hedefRol == "Musteri") ? tc : null,
                Ad = (Input.Ad ?? string.Empty).Trim(),
                Soyad = (Input.Soyad ?? string.Empty).Trim(),
                PhoneNumber = phoneNorm!, // ✅ Artık aktif
                KayitTarihi = DateTime.Now,
                ReferansKodu = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                IsReferralCodeActive = false,
                HasMetReferralThreshold = false,
                UsedBackupReferral = false,
                SponsorId = sponsor.Id
            };

            var result = await _userManager.CreateAsync(user, Input.Password);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);
                DumpModelErrorsToTempData(); return Page();
            }

            _logger.LogInformation("Yeni kullanıcı oluşturuldu: {Email}", user.Email);

            await _userManager.AddToRoleAsync(user, "Musteri");
            await _signInManager.RefreshSignInAsync(user);

            // 6) E-posta onayı
            try
            {
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var codeEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, code = codeEncoded },
                    protocol: Request.Scheme);

                await _emailSender.SendEmailAsync(
                    user.Email!,
                    "E-posta Onayı",
                    $"Merhaba {user.Ad}, hesabınızı onaylamak için " +
                    $"<a href='{HtmlEncoder.Default.Encode(callbackUrl!)}'>buraya tıkla</a>.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kayıt e-postası gönderilemedi. UserId={UserId}", user.Id);
                TempData["ErrorMessage"] = "E-posta bildirimi şu an gönderilemedi; daha sonra deneyebilirsiniz.";
            }

            // 7) SMS (kapalı)
            // ...

            return RedirectToPage("./RegisterConfirmation", new { email = user.Email, returnUrl = ReturnUrl });
        }
    }
}
