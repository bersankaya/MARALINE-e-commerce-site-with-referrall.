using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AlisverisSitesiFinal.Controllers
{
    [AllowAnonymous]
    public class PhoneConfirmController : Controller
    {
        private readonly UserManager<Kullanici> _userManager;
        private readonly ISmsSender _sms;

        public PhoneConfirmController(UserManager<Kullanici> userManager, ISmsSender sms)
        {
            _userManager = userManager;
            _sms = sms;
        }

        [HttpGet]
        public IActionResult Verify(string userId, string phone)
        {
            return View(new PhoneVerifyVm { UserId = userId, Phone = phone });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(PhoneVerifyVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _userManager.FindByIdAsync(vm.UserId!);
            if (user == null) { ModelState.AddModelError("", "Kullanıcı bulunamadı."); return View(vm); }

            var result = await _userManager.ChangePhoneNumberAsync(user, vm.Phone!, vm.Code!);
            if (result.Succeeded)
            {
                // Telefon onaylandı → girişe yönlendir
                if (user.EmailConfirmed)
                    TempData["Success"] = "Telefon doğrulandı. Giriş yapabilirsiniz.";
                else
                    TempData["Success"] = "Telefon doğrulandı. Şimdi e-posta onayını tamamlayın.";

                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            ModelState.AddModelError("", "Kod geçersiz veya süresi doldu.");
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resend(string userId, string phone)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var code = await _userManager.GenerateChangePhoneNumberTokenAsync(user, phone);
            await _sms.SendSmsAsync(phone, $"Yeni doğrulama kodun: {code}");
            TempData["Info"] = "Kod tekrar gönderildi.";
            return RedirectToAction(nameof(Verify), new { userId, phone });
        }
    }

    public class PhoneVerifyVm
    {
        public string? UserId { get; set; }
        public string? Phone { get; set; }
        public string? Code { get; set; }
    }
}
