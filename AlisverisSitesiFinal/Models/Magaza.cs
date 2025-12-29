using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlisverisSitesiFinal.Models
{
    public class Magaza
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Ad { get; set; } = string.Empty;

        // Mağaza sahibi (AspNetUsers FK)
        [Required]
        public string OwnerUserId { get; set; } = string.Empty;

        [StringLength(50)]
        public string? VergiNo { get; set; }

        [StringLength(34)]
        public string? IBAN { get; set; }

        [StringLength(40)]
        public string? Telefon { get; set; }

        [StringLength(200)]
        public string? Adres { get; set; }

        public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;

        public bool AktifMi { get; set; } = true;

        // Navigation
        [ForeignKey(nameof(OwnerUserId))]
        public Kullanici? Sahip { get; set; }

        public List<Urun> Urunler { get; set; } = new();
    }
}
