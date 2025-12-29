using System;
using System.ComponentModel.DataAnnotations;

namespace AlisverisSitesiFinal.Models
{
    public enum BasvuruDurum { Beklemede = 0, Onaylandi = 1, Reddedildi = 2 }

    public class MagazaBasvurusu
    {
        public int Id { get; set; }

        // Başvuru, login olan bir kullanıcıyla yapılır (önerilen)
        [Required]
        public string KullaniciId { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string MagazaAdi { get; set; } = string.Empty;

        [MaxLength(200)]
        public string VergiNo { get; set; } = string.Empty;

        [MaxLength(34)]
        public string? IBAN { get; set; } // ödeme için

        [MaxLength(500)]
        public string? Aciklama { get; set; }

        public DateTime BasvuruTarihi { get; set; } = DateTime.Now;
        public BasvuruDurum Durum { get; set; } = BasvuruDurum.Beklemede;
    }
}
