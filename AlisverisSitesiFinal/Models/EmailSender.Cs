using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace AlisverisSitesiFinal.Services // Namespace'i projenizin ad alanına göre ayarlayın. Örneğin: AlisverisSitesiFinal.Data veya AlisverisSitesiFinal
{
    public class EmailSender : IEmailSender
    {
        // Bu metod aslında bir e-posta göndermez, sadece bir Task döner.
        // Gerçek e-posta gönderme mantığını buraya ekleyebilirsiniz.
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Örneğin, e-postayı konsola yazdırabilirsiniz (sadece geliştirme için)
            // Console.WriteLine($"Email: {email}, Subject: {subject}, Message: {htmlMessage}");
            return Task.CompletedTask;
        }
    }
}