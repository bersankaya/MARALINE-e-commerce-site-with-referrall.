using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity.UI.Services; // IEmailSender
using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;

namespace AlisverisSitesiFinal.Services
{
    public class OrderNotificationService : IOrderNotificationService
    {
        private readonly UygulamaDbContext _db;
        private readonly IEmailSender _email;
        private readonly ILogger<OrderNotificationService> _logger;

        public OrderNotificationService(
            UygulamaDbContext db,
            IEmailSender email,
            ILogger<OrderNotificationService> logger)
        {
            _db = db;
            _email = email;
            _logger = logger;
        }

        public async Task NotifySellersAsync(int siparisId)
        {
            // 1) Siparişi çek
            var siparis = await _db.Siparisler
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == siparisId);

            if (siparis == null)
            {
                _logger.LogWarning("NotifySellersAsync: Sipariş bulunamadı: {SiparisId}", siparisId);
                return;
            }

            // 2) Müşteriyi (Identity user) çek
            var musteri = string.IsNullOrWhiteSpace(siparis.IdentityUserId)
                ? null
                : await _db.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == siparis.IdentityUserId);

            // 3) Kalemler + ürün
            var kalemler = await _db.Set<SiparisKalemi>()
                .AsNoTracking()
                .Include(k => k.Urun)
                .Where(k => k.SiparisId == siparisId)
                .ToListAsync();

            if (kalemler.Count == 0)
            {
                _logger.LogInformation("NotifySellersAsync: Siparişte kalem yok: {SiparisId}", siparisId);
                return;
            }

            // 4) Satıcıya göre grupla (Urun.UserId satıcıyı tutuyor varsayımı)
            var gruplar = kalemler
                .Where(k => k.Urun != null && !string.IsNullOrEmpty(k.Urun.UserId))
                .GroupBy(k => k.Urun!.UserId!)
                .ToList();

            if (gruplar.Count == 0)
            {
                _logger.LogInformation("NotifySellersAsync: Satıcı bulunamadı (ürünlerde UserId eksik olabilir). SiparişId={SiparisId}", siparisId);
                return;
            }

            var saticiIdList = gruplar.Select(g => g.Key).Distinct().ToList();

            // 5) Satıcı e-postaları
            var saticilar = await _db.Users
                .Where(u => saticiIdList.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    AdSoyad = ((u.Ad ?? "") + " " + (u.Soyad ?? "")).Trim()
                })
                .ToListAsync();

            // 6) Her satıcıya kendi kalem listesini maille gönder
            foreach (var grp in gruplar)
            {
                var satici = saticilar.FirstOrDefault(x => x.Id == grp.Key);
                if (satici == null || string.IsNullOrWhiteSpace(satici.Email))
                {
                    _logger.LogWarning("NotifySellersAsync: Satıcı veya e-posta yok. SatıcıId={SatıcıId}", grp.Key);
                    continue;
                }

                var sb = new StringBuilder();
                sb.AppendLine("<div style='font-family:Arial,sans-serif;font-size:14px;color:#111'>");
                sb.AppendLine("<h2 style='margin:0 0 10px'>Yeni Sipariş Bildirimi</h2>");
                sb.AppendLine($"<p>Merhaba {(string.IsNullOrWhiteSpace(satici.AdSoyad) ? "Satıcı" : satici.AdSoyad)},</p>");
                sb.AppendLine("<p>Mağazanıza ait aşağıdaki ürün(ler) için yeni bir sipariş oluşturuldu:</p>");
                sb.AppendLine("<table cellpadding='6' cellspacing='0' style='border-collapse:collapse'>");
                sb.AppendLine("<thead><tr>");
                sb.AppendLine("<th align='left'>Ürün</th><th align='right'>Adet</th><th align='right'>Birim Fiyat</th>");
                sb.AppendLine("</tr></thead><tbody>");

                foreach (var k in grp)
                {
                    var urunAd = k.Urun?.Ad ?? "Ürün";
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(urunAd)}</td>");
                    sb.AppendLine($"<td align='right'>{k.Miktar}</td>");
                    sb.AppendLine($"<td align='right'>{k.BirimFiyat:C2}</td>");
                    sb.AppendLine("</tr>");
                }

                sb.AppendLine("</tbody></table>");
                sb.AppendLine("<hr style='border:none;border-top:1px solid #eee;margin:12px 0'/>");
                sb.AppendLine($"<p><strong>Sipariş No:</strong> {siparis.Id}<br/>");
                sb.AppendLine($"<strong>Tarih:</strong> {siparis.SiparisTarihi:dd.MM.yyyy HH:mm}<br/>");
                sb.AppendLine($"<strong>Toplam Tutar:</strong> {siparis.ToplamTutar:C2}</p>");

                if (musteri != null)
                {
                    var musteriAdSoyad = $"{musteri.Ad} {musteri.Soyad}".Trim();
                    if (!string.IsNullOrWhiteSpace(musteriAdSoyad))
                        sb.AppendLine($"<p><strong>Müşteri:</strong> {System.Net.WebUtility.HtmlEncode(musteriAdSoyad)}</p>");
                }

                sb.AppendLine("<p>Maraline Panelinizden siparişi görüntüleyebilirsiniz.</p>");
                sb.AppendLine("</div>");

                var subject = $"[Maraline] Yeni Sipariş #{siparis.Id}";
                try
                {
                    await _email.SendEmailAsync(satici.Email, subject, sb.ToString());
                    _logger.LogInformation("Sipariş bildirimi gönderildi. SiparişId={SiparisId}, Satıcı={Email}",
                        siparisId, satici.Email);
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "Sipariş bildirimi gönderilemedi. SiparişId={SiparisId}, Satıcı={Email}",
                        siparisId, satici.Email);
                }
            }
        }
    }
}
