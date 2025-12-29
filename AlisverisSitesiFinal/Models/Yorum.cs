using System;
using System.ComponentModel.DataAnnotations;

namespace AlisverisSitesiFinal.Models
{
    public class Yorum
    {
        public int Id { get; set; }

        [Required]
        public string KullaniciId { get; set; } = string.Empty;

        [Required]
        public int UrunId { get; set; }

        [Required]
        [StringLength(500)]
        public string Icerik { get; set; } = string.Empty;

        public DateTime Tarih { get; set; } = DateTime.Now;

        public Kullanici? Kullanici { get; set; }
        public Urun? Urun { get; set; }
        [Range(0, 5, ErrorMessage = "Puan 0 ile 5 arasında olmalı.")]
        public int Puan { get; set; } = 0; // mevcut kayıtlara sorun çıkmasın diye default 0
    }
}
