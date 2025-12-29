using System.ComponentModel.DataAnnotations;

namespace AlisverisSitesiFinal.Models
{
    public class Kategori
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Kategori Adı")]
        public string Ad { get; set; } = null!;

        public string? ImageUrl { get; set; }


    }
}
