using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AlisverisSitesiFinal.Models;

namespace AlisverisSitesiFinal.Areas.Identity.Pages.Account.Manage
{
    public class EmailModel : PageModel
    {
        private readonly UserManager<Kullanici> _userManager;
        private readonly SignInManager<Kullanici> _signInManager;

        public EmailModel(UserManager<Kullanici> userManager, SignInManager<Kullanici> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string? Email { get; private set; }

        public bool IsEmailConfirmed { get; private set; }

        [TempData]
        public string? StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = null!; // null uyarısını engellemek için

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Yeni E-posta")]
            public string NewEmail { get; set; } = null!;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound("Kullanıcı bulunamadı.");

            Email = await _userManager.GetEmailAsync(user);
            IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);

            Input = new InputModel
            {
                NewEmail = Email ?? string.Empty
            };

            return Page();
        }

        public async Task<IActionResult> OnPostChangeEmailAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound("Kullanıcı bulunamadı.");

            if (!ModelState.IsValid)
                return Page();

            var email = await _userManager.GetEmailAsync(user);
            if (Input.NewEmail != email)
            {
                var setEmailResult = await _userManager.SetEmailAsync(user, Input.NewEmail);
                if (!setEmailResult.Succeeded)
                {
                    StatusMessage = "E-posta değiştirme işlemi başarısız oldu.";
                    return Page();
                }

                await _signInManager.RefreshSignInAsync(user);
                StatusMessage = "E-posta başarıyla değiştirildi.";
                return RedirectToPage();
            }

            StatusMessage = "E-posta değişikliği yapılmadı.";
            return RedirectToPage();
        }
    }
}
