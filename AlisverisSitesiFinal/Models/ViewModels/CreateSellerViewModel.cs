using System.ComponentModel.DataAnnotations;

namespace AlisverisSitesiFinal.Models.ViewModels
{
    public class CreateSellerViewModel
    {
        // --- Kullanıcı (AspNetUsers) ---
        [Required, StringLength(50)]
        public string Ad { get; set; } = "";

        [Required, StringLength(50)]
        public string Soyad { get; set; } = "";

        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, MinLength(6)]
        public string Sifre { get; set; } = "";

        public bool OnayliSatici { get; set; } = true; // işaretliyse direkt "Satici" rolü ver

        // --- Mağaza (Magazalar) ---
        [Display(Name = "Mağaza Adı")]
        [Required, StringLength(120)]
        public string MagazaAdi { get; set; } = "";

        [StringLength(50)]
        public string? VergiNo { get; set; }

        [StringLength(34)]
        public string? IBAN { get; set; }

        [StringLength(40)]
        public string? Telefon { get; set; }

        [StringLength(200)]
        public string? Adres { get; set; }

        public bool AktifMi { get; set; } = true;
    }
}
