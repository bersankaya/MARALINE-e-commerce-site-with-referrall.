using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AlisverisSitesiFinal.Models;

namespace AlisverisSitesiFinal.Areas.Identity.Pages.Account.Manage
{
    public class DeletePersonalDataModel : PageModel
    {
        private readonly UserManager<Kullanici> _userManager;
        private readonly SignInManager<Kullanici> _signInManager;

        public DeletePersonalDataModel(UserManager<Kullanici> userManager, SignInManager<Kullanici> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = null!;

        [TempData]
        public string? StatusMessage { get; set; }

        public bool RequirePassword { get; private set; }

        public class InputModel
        {
            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Parola")]
            public string Password { get; set; } = null!;
        }

        public async Task<IActionResult> OnGet()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound("Kullanıcı bulunamadı.");

            RequirePassword = await _userManager.HasPasswordAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound("Kullanıcı bulunamadı.");

            RequirePassword = await _userManager.HasPasswordAsync(user);

            if (RequirePassword)
            {
                if (!await _userManager.CheckPasswordAsync(user, Input.Password))
                {
                    ModelState.AddModelError(string.Empty, "Parola yanlış.");
                    return Page();
                }
            }

            var result = await _userManager.DeleteAsync(user);
            var userId = await _userManager.GetUserIdAsync(user);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Kullanıcı silinemedi: {userId}");
            }

            await _signInManager.SignOutAsync();

            return Redirect("~/");
        }
    }
}
