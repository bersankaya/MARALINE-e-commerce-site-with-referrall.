using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlisverisSitesiFinal.Models
{
    public class Kullanici : IdentityUser
    {
        public string? Ad { get; set; } = string.Empty;
        public string? Soyad { get; set; } = string.Empty;

        public string? TcKimlikNo { get; set; } = string.Empty;

        [NotMapped]
        public string AdSoyad => $"{Ad} {Soyad}";

        [Required(ErrorMessage = "Referans kodu zorunludur.")]
        public string? ReferansKodu { get; set; } = string.Empty;
        public string? SponsorId { get; set; }
        public Kullanici? Sponsor { get; set; }
        public ICollection<Kullanici> Referrals { get; set; } = new List<Kullanici>();

        public decimal ToplamHarcama { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ToplamKazanc { get; set; } = 0m; // Değiştirilmeyen gerçek kazanç

        public decimal ToplamKazanilanPara { get; set; } = 0;
        public decimal AylikKazanilanPara { get; set; } = 0;
        public DateTime KayitTarihi { get; set; } = DateTime.Now;

        public bool HasMetReferralThreshold { get; set; } = false;
        public bool IsReferralCodeActive { get; set; } = true;
        public bool HasTriggeredAdminInitialDirectPairBonus { get; set; } = false;
        public int ActiveDirectReferralCount { get; set; } = 0;
        public bool UsedBackupReferral { get; set; } = false;
        public int? OzelReferansLimiti { get; set; } // null ise genel limit geçerli
                                                     
        public ICollection<Adres>? Adresler { get; set; }

        public string IBAN { get; set; } = string.Empty;


    }
}
