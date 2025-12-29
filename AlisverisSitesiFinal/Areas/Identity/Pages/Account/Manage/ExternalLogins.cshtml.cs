using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AlisverisSitesiFinal.Models;

namespace AlisverisSitesiFinal.Areas.Identity.Pages.Account.Manage
{
    public class ExternalLoginsModel : PageModel
    {
        private readonly UserManager<Kullanici> _userManager;
        private readonly SignInManager<Kullanici> _signInManager;

        public ExternalLoginsModel(
            UserManager<Kullanici> userManager,
            SignInManager<Kullanici> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public IList<UserLoginInfo> CurrentLogins { get; set; } = new List<UserLoginInfo>();

        public IList<AuthenticationScheme> OtherLogins { get; set; } = new List<AuthenticationScheme>();

        [TempData]
        public string? StatusMessage { get; set; }

        public bool ShowRemoveButton { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Kullanıcı bulunamadı.");
            }

            CurrentLogins = await _userManager.GetLoginsAsync(user);
            OtherLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync())
                .Where(auth => CurrentLogins.All(ul => auth.Name != ul.LoginProvider))
                .ToList();

            ShowRemoveButton = user.PasswordHash != null || CurrentLogins.Count > 1;

            return Page();
        }

        public async Task<IActionResult> OnPostRemoveLoginAsync(string loginProvider, string providerKey)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Kullanıcı bulunamadı.");
            }

            var result = await _userManager.RemoveLoginAsync(user, loginProvider, providerKey);
            if (!result.Succeeded)
            {
                StatusMessage = "Harici giriş bağlantısı kaldırılırken hata oluştu.";
                return RedirectToPage();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Harici giriş bağlantısı kaldırıldı.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostLinkLoginAsync(string provider)
        {
            // Kullanıcıyı yönlendirerek harici giriş sağlayıcısıyla bağlantı yapmasını sağlar
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Kullanıcı bulunamadı.");
            }

            // Dışa yönlendirme URL'si
            var redirectUrl = Url.Page("./ExternalLogins", pageHandler: "LinkLoginCallback");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, user.Id);

            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetLinkLoginCallbackAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Kullanıcı bulunamadı.");
            }

            var info = await _signInManager.GetExternalLoginInfoAsync(user.Id);
            if (info == null)
            {
                StatusMessage = "Harici giriş bilgileri alınamadı.";
                return RedirectToPage();
            }

            var result = await _userManager.AddLoginAsync(user, info);
            if (!result.Succeeded)
            {
                StatusMessage = "Harici giriş bağlantısı eklenirken hata oluştu.";
                return RedirectToPage();
            }

            // Oturumu yenile
            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Harici giriş bağlantısı eklendi.";
            return RedirectToPage();
        }
    }
}
