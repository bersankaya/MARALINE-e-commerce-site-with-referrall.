using System.ComponentModel.DataAnnotations;

namespace AlisverisSitesiFinal.Models.ViewModels
{
    public class AdminSaticiVM
    {
        // Kullanıcı
        [Required, StringLength(50)]
        public string Ad { get; set; } = "";

        [Required, StringLength(50)]
        public string Soyad { get; set; } = "";

        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6)]
        public string Sifre { get; set; } = "";

        [Display(Name = "Onaylı Satıcı (Satici rolü)")]
        public bool OnayliSatici { get; set; } = true;

        // Mağaza
        [Display(Name = "Mağaza Adı")]
        public string? MagazaAdi { get; set; }

        [StringLength(11)]
        [Display(Name = "Vergi No")]
        public string? VergiNo { get; set; }

        [Display(Name = "IBAN")]
        [Required]
        public string IBAN { get; set; } = string.Empty;

        [Phone, Display(Name = "Telefon")]
        public string? Telefon { get; set; }

        [Display(Name = "Adres")]
        public string? Adres { get; set; }

        [Display(Name = "Mağaza Aktif mi? (Yayın durumu)")]
        public bool AktifMi { get; set; } = true;
    }
}
