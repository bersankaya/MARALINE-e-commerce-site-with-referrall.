// Areas/Identity/Pages/Account/Login.cshtml.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Services;
namespace AlisverisSitesiFinal.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<Kullanici> _signInManager;
        private readonly UserManager<Kullanici> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly GoogleReCaptchaService _captcha;
        public LoginModel(
            SignInManager<Kullanici> signInManager,
            UserManager<Kullanici> userManager,
            ILogger<LoginModel> logger,
            IEmailSender emailSender, GoogleReCaptchaService captcha)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _emailSender = emailSender;
            _captcha = captcha;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();
        public string ReturnUrl { get; set; } = string.Empty;

        [TempData]
        public string ErrorMessage { get; set; } = string.Empty;

        // Bilgi bandı için
        [TempData]
        public string ResendStatus { get; set; } = string.Empty;

        public class InputModel
        {
            [Required(ErrorMessage = "E-posta adresi zorunludur.")]
            [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
            [Display(Name = "E-posta")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Şifre zorunludur.")]
            [DataType(DataType.Password)]
            [Display(Name = "Şifre")]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Beni Hatırla?")]
            public bool RememberMe { get; set; }
        }

        // Yeniden onay formu için ayrı binding (Required DEĞİL; login'de val. dışı bırakacağız)
        [BindProperty]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
        public string ResendEmail { get; set; } = string.Empty;

        public async Task OnGetAsync(string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
                ModelState.AddModelError(string.Empty, ErrorMessage);

            returnUrl ??= Url.Content("~/");

            // Harici giriş cookie'sini temizle
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
           

            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            var captchaToken = Request.Form["g-recaptcha-response"].ToString();
            if (!await _captcha.VerifyAsync(captchaToken))
            {
                ModelState.AddModelError(string.Empty, "Robot doğrulamasını geçemediniz.");
                return Page();
            }
            // 🔧 Login sırasında ResendEmail'i validasyondan çıkar
            ModelState.Remove(nameof(ResendEmail));

            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "E-posta adresi kayıtlı değil.");
                return Page();
            }

            if (string.IsNullOrEmpty(user.UserName))
            {
                ModelState.AddModelError(string.Empty, "Kullanıcı adı eksik. Sistem yöneticisine başvurun.");
                return Page();
            }

            // ✅ E-posta onayı şart
            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                ModelState.AddModelError(string.Empty,
                    "E-posta adresiniz henüz onaylanmamış. Aşağıdaki formdan onay e-postasını tekrar gönderebilirsiniz.");
                return Page();
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                _logger.LogInformation("Kullanıcı giriş yaptı: {Email}", Input.Email);

                // Sadece güvenli local URL'lere izin ver
                string safeUrl = (returnUrl != null
                                  && Url.IsLocalUrl(returnUrl)
                                  && !returnUrl.Contains("/Account/Logout", System.StringComparison.OrdinalIgnoreCase))
                                 ? returnUrl
                                 : Url.Content("~/");

                return LocalRedirect(safeUrl);
            }

            if (result.RequiresTwoFactor)
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, Input.RememberMe });

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Kullanıcı hesabı kilitlendi: {Email}", Input.Email);
                return RedirectToPage("./Lockout");
            }

            ModelState.AddModelError(string.Empty, "Geçersiz giriş denemesi.");
            return Page();
        }

        // ✅ Kayıtlı ama onaysız hesaplar için onay mailini yeniden gönder
        public async Task<IActionResult> OnPostResendConfirmationAsync()
        {
            // Bu handler kendi validasyonunu yapacak
            ModelState.Clear();

            if (string.IsNullOrWhiteSpace(ResendEmail))
            {
                ResendStatus = "Lütfen e-posta adresinizi yazın.";
                return Page();
            }

            var email = ResendEmail.Trim();
            var user = await _userManager.FindByEmailAsync(email);

            // Güvenlik için aynı mesajı verebilirsin ama talebinle uyumlu olsun diye açıkça yazıyorum
            if (user == null)
            {
                ResendStatus = "Bu e-posta ile kayıt bulunamadı.";
                return Page();
            }

            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                ResendStatus = "Bu e-posta zaten onaylanmış.";
                return Page();
            }

            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var codeEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = user.Id, code = codeEncoded },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(
                user.Email!,
                "Maraline — E‑posta Onayı",
                $"Merhaba {user.Ad}, hesabınızı onaylamak için <a href='{callbackUrl}'>buraya tıklayın</a>.");

            ResendStatus = "Onay e-postası gönderildi. Gelen kutunuzu ve spam klasörünü kontrol edin.";
            return Page();
        }
    }
}
