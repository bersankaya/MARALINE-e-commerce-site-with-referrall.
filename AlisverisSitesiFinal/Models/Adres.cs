using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlisverisSitesiFinal.Models
{
    public class Adres
    {
        public int Id { get; set; }

        // FORM’dan gelmez, server’da set edilir.
        public string KullaniciId { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string AdresBasligi { get; set; } = "Ev";

        [Required, StringLength(250)]
        public string AdresDetayi { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Il { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Ilce { get; set; } = string.Empty;

        [StringLength(10)]
        public string? PostaKodu { get; set; }

        // [Phone] KALDIRILDI — TR formatları yüzünden invalid düşürüyordu
        [StringLength(20)]
        public string? Telefon { get; set; }
        // ✅ Zorunlu T.C. Kimlik
        [Required(ErrorMessage = "T.C. Kimlik numarası zorunludur.")]
        [StringLength(11, MinimumLength = 11, ErrorMessage = "T.C. Kimlik numarası 11 haneli olmalıdır.")]
        [RegularExpression("^[0-9]{11}$", ErrorMessage = "T.C. Kimlik numarası sadece rakamlardan oluşmalıdır.")]
        public string? TCKimlik { get; set; }
        public bool IsVarsayilan { get; set; }

        [ForeignKey(nameof(KullaniciId))]
        public Kullanici? Kullanici { get; set; }
    }
}
