using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AlisverisSitesiFinal.Areas.Identity.Pages.Account
{
    [Authorize]
    public class ConfirmPhoneNumberModel : PageModel
    {
        private readonly UserManager<Kullanici> _userManager;
        private readonly ISmsSender _smsSender;

        public ConfirmPhoneNumberModel(UserManager<Kullanici> userManager, ISmsSender smsSender)
        {
            _userManager = userManager;
            _smsSender = smsSender;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Telefon zorunludur.")]
            [Display(Name = "Telefon Numarası")]
            [RegularExpression(@"^(?:\+90|0)?5\d{9}$", ErrorMessage = "Geçerli bir Türk GSM numarası giriniz (örn: 05XXXXXXXXX).")]
            public string PhoneNumber { get; set; } = string.Empty;

            [Display(Name = "SMS Kodu")]
            [StringLength(8, MinimumLength = 4)]
            public string? Code { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Eğer kullanıcıda telefon kayıtlıysa input'a koy
            if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
                Input.PhoneNumber = user.PhoneNumber;

            return Page();
        }

        // 1) Kodu gönder
        public async Task<IActionResult> OnPostSendCodeAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Normalize phone: boşlukları sil, +90/0 formatlarını tekilleştir
            var normalized = Regex.Replace(Input.PhoneNumber, @"\s+", "");
            if (normalized.StartsWith("+90"))
                normalized = "0" + normalized.Substring(3);

            // Kullanıcının telefonunu henüz değiştirmedik; token üretirken hedef numarayı veriyoruz.
            var token = await _userManager.GenerateChangePhoneNumberTokenAsync(user, normalized);

            // SMS gönder
            await _smsSender.SendSmsAsync(normalized, $"Maraline doğrulama kodunuz: {token}");

            // Telefonu geçici olarak input'ta saklıyoruz (hidden)
            Input.PhoneNumber = normalized;

            StatusMessage = "Doğrulama kodu SMS olarak gönderildi.";
            return Page();
        }

        // 2) Kodu doğrula
        public async Task<IActionResult> OnPostVerifyAsync()
        {
            // Code zorunlu
            if (string.IsNullOrWhiteSpace(Input.Code))
            {
                ModelState.AddModelError("Input.Code", "Kod zorunludur.");
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var normalized = Regex.Replace(Input.PhoneNumber ?? "", @"\s+", "");
            if (normalized.StartsWith("+90"))
                normalized = "0" + normalized.Substring(3);

            var isValid = await _userManager.VerifyChangePhoneNumberTokenAsync(user, Input.Code!, normalized);
            if (!isValid)
            {
                ModelState.AddModelError("Input.Code", "Kod geçersiz veya süresi doldu.");
                return Page();
            }

            // Doğrulandı → kullanıcıya yaz
            var result = await _userManager.ChangePhoneNumberAsync(user, normalized, Input.Code!);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                return Page();
            }

            // Flag’i set et (ChangePhoneNumberAsync zaten PhoneNumber'ı set eder ve confirmed yapar)
            // Ancak güvence için tekrar oku:
            user = await _userManager.GetUserAsync(User);
            StatusMessage = "Telefon numaranız başarıyla doğrulandı.";
            return RedirectToPage(); // sayfayı temizle
        }
    }
}
