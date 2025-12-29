using System;

namespace AlisverisSitesiFinal.Models.ViewModels
{
    public class OdemeDagitimViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string AdSoyad { get; set; } = string.Empty;
        public string? IBAN { get; set; }

        public decimal ToplamKazanc { get; set; }          // Çekilebilir bakiye (azalır)
        public decimal AylikKazanilanPara { get; set; }     // Görsel amaçlı (azalmaz)

        // Son talep özeti
        public int? SonTalepId { get; set; }
        public decimal? SonTalepTutar { get; set; }
        public string? SonTalepDurum { get; set; }          // Beklemede/Onaylandi/Reddedildi
        public DateTime? SonTalepTarihi { get; set; }

        // Manuel ödeme formu için
        public decimal ManuelOdemeTutar { get; set; }
        public string? ManuelOdemeAciklama { get; set; }
    }
}
