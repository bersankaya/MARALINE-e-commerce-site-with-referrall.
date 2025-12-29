// Models/SiparisKalemi.cs
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlisverisSitesiFinal.Models
{
    public enum SiparisKalemDurum
    {
        Beklemede = 0,   // Sipariş oluştu, satıcı görür
        SaticiOnayladi = 1,
        Kargolandı = 2,
        tamamlandı =3,
        Iptal = 4
    }
    public class SiparisKalemi
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SiparisId { get; set; }

        [ForeignKey("SiparisId")]
        public Siparis? Siparis { get; set; }

        [Required]
        public int UrunId { get; set; }

        [ForeignKey("UrunId")]
        public Urun? Urun { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Miktar en az 1 olmalıdır.")]
        public int Miktar { get; set; }

        [Required]
        [Range(0.01, 1000000)]
        [Precision(18, 2)]
        public decimal BirimFiyat { get; set; }

        [MaxLength(32)]
        public string? Renk { get; set; }

        [MaxLength(32)]
        public string? Beden { get; set; }   // zaten varsa kalsın

        [MaxLength(32)]
        public string? Boyut { get; set; }   // zaten varsa kalsın

        [MaxLength(32)]
        public string? Numara { get; set; }

        [MaxLength(64)]
        public string? Kapasite { get; set; }

        [MaxLength(64)]
        public string? Materyal { get; set; }

        [MaxLength(64)]
        public string? Desen { get; set; }

        [MaxLength(256)]
        public string? Aciklama { get; set; }

        public string SaticiId { get; set; } = string.Empty;

        // ✅ Satıcı iş akışı için kalem durumu
        public SiparisKalemDurum KalemDurum { get; set; } = SiparisKalemDurum.Beklemede;
        // Opsiyonel alanlar
        public string? MagazaNotu { get; set; }
        public string? KargoTakipNo { get; set; }
        

        [Precision(18, 2)]
        public decimal? SaticiTeklifAnlik { get; set; }      // satış anındaki teklif
        [Precision(18, 2)]
        public decimal? AdminFiyatAnlik { get; set; }        // satış anındaki admin fiyat
        [Precision(18, 2)]
        public decimal? RefFiyatAnlik { get; set; }          // satış anındaki referanslı fiyat
                                                             // ==== ÖDEME TEKRARINI ENGELLEYEN BAYRAK ====
        [Column(TypeName = "decimal(18,2)")]
        public decimal HizmetBedeli { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SirketKari { get; set; }

        public bool SaticiOdemeyeDahilMi { get; set; } = false;
        // (Opsiyonel) KDV alanları (e-fatura için faydalı)
        // KDV alanları
        [Column(TypeName = "decimal(5,2)")]   // ör. 20.00 gibi
        public decimal? KdvOrani { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? KdvTutari { get; set; }


    }
}
