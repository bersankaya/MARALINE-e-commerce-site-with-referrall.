using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlisverisSitesiFinal.Models
{
    public class ParaCekmeTalebi
    {
        public int Id { get; set; }

        [Required]
        public string KullaniciId { get; set; } = string.Empty;

        [Required]
        public string IBANSahibiAdSoyad { get; set; } = string.Empty;

        [Required]
        public string IBAN { get; set; } = string.Empty;

        [Required]
        [Range(1, double.MaxValue, ErrorMessage = "Tutar 1 TL'den büyük olmalıdır.")]
        public decimal Tutar { get; set; }

        public DateTime TalepTarihi { get; set; } = DateTime.Now;
        public DateTime? OnayTarihi { get; set; }


        public bool OnaylandiMi { get; set; } = false;
        public bool ReddedildiMi { get; set; } = false;

        [NotMapped]
        public string KullaniciAdSoyad { get; set; } = string.Empty;
        public string? AdminNotu { get; set; }
    }
}
