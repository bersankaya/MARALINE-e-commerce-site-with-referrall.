using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using AlisverisSitesiFinal.Models;

[AllowAnonymous]
public class ResendEmailConfirmationModel : PageModel
{
    private readonly UserManager<Kullanici> _userManager;
    public ResendEmailConfirmationModel(UserManager<Kullanici> userManager) => _userManager = userManager;

    public string StatusMessage { get; set; } = "";

    public async Task OnGetAsync(string userId, string code)
    {
        if (userId == null || code == null) { StatusMessage = "Onay bilgisi eksik."; return; }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) { StatusMessage = "Kullanıcı bulunamadı."; return; }

        var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        var result = await _userManager.ConfirmEmailAsync(user, decoded);

        StatusMessage = result.Succeeded
            ? "E‑posta adresiniz doğrulandı. Giriş yapabilirsiniz."
            : "E‑posta doğrulama başarısız oldu. Lütfen yeni bir onay bağlantısı isteyin.";
    }
}
