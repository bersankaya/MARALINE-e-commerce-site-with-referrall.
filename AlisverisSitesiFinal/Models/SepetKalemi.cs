using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlisverisSitesiFinal.Models
{
    public class SepetKalemi
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string KullaniciId { get; set; } = string.Empty;

        [ForeignKey("KullaniciId")]
        public Kullanici? Kullanici { get; set; }

        [Required]
        public int UrunId { get; set; }

        [ForeignKey("UrunId")]
        public Urun? Urun { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Miktar en az 1 olmalıdır.")]
        public int Miktar { get; set; } = 1;


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
    


}
}
