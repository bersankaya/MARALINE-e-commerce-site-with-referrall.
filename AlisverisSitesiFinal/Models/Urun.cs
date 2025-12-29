// Models/Urun.cs
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlisverisSitesiFinal.Models
{
    public enum UrunDurum
    {
        Taslak = 0,             // Oluşturan kaydetti ama yayınlamadı
        AdminFiyatBekliyor = 1, // Satıcı teklif verdi, admin fiyatlandıracak
        Yayinda = 2,            // Admin fiyatları girip yayına aldı
        Reddedildi = 3
    }

    public class Urun
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Ürün adı gereklidir.")]
        [StringLength(100)]
        public string Ad { get; set; } = string.Empty;

        [StringLength(500)]
        public string Aciklama { get; set; } = string.Empty;

        // Satıcı formunda zorunlu olmayacak; controller içinde set edilecek
        [Precision(18, 2)]
        public decimal Fiyat { get; set; }

        [Required(ErrorMessage = "Stok adedi gereklidir.")]
        [Range(0, int.MaxValue)]
        public int StokAdedi { get; set; }

        [Display(Name = "Resim URL")]
        public string? ResimUrl { get; set; }

        public DateTime EklenmeTarihi { get; set; } = DateTime.Now;

        public string? UserId { get; set; } = string.Empty;

        [NotMapped]
        public IFormFile? ResimDosyasi { get; set; }

        public bool IsSlider { get; set; } = false;
        public bool IsPopular { get; set; } = false;
        public bool IsAvantajli { get; set; } = false;
        public bool IsCokSatan { get; set; } = false;

        [Required(ErrorMessage = "Kategori zorunludur.")]
        public int? KategoriId { get; set; }

        [ForeignKey("KategoriId")]
        public Kategori? Kategori { get; set; }
        public string? Etiket { get; set; }  // "Yeni", "İndirimli", vb.
        public List<Yorum> Yorumlar { get; set; } = new();


        // Satıcı teklifi
        [Range(0, 9_999_999)]
        [Precision(18, 2)]
        public decimal? SaticiTeklifFiyati { get; set; }

        // Admin fiyatları
        [Range(0, 9_999_999)]
        [Precision(18, 2)]
        public decimal? FiyatAdmin { get; set; }

        [Range(0, 9_999_999)]
        [Precision(18, 2)]
        public decimal? FiyatReferansli { get; set; }

        public bool YayindaMi { get; set; } = false;
        public UrunDurum Durum { get; set; } = UrunDurum.Taslak;

        [ForeignKey(nameof(UserId))]
        public Kullanici? Kullanici { get; set; }
         
        public int? StoreId { get; set; }         // yeni
        public Magaza? Store { get; set; }        // yeni

        // Seçim var mı bayrakları
        public bool BoyutSecimiVar { get; set; } = false;
        public bool BedenSecimiVar { get; set; } = false;
        public bool RenkSecimiVar { get; set; }
        public bool NumaraSecimiVar { get; set; }
        public bool KapasiteSecimiVar { get; set; }
        public bool MateryalSecimiVar { get; set; }
        public bool DesenSecimiVar { get; set; }
        // Virgülle ayrılmış seçenek listeleri
        public string? BedenSecenekleri { get; set; }
        public string? BoyutSecenekleri { get; set; }
        public string? RenkSecenekleri { get; set; }
        public string? NumaraSecenekleri { get; set; }
        public string? KapasiteSecenekleri { get; set; }
        public string? MateryalSecenekleri { get; set; }
        public string? DesenSecenekleri { get; set; }

    }
}
