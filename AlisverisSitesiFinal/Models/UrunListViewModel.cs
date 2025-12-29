namespace AlisverisSitesiFinal.Models
{
    public class UrunListViewModel
    {
        public List<Urun> TumUrunler { get; set; } = new();
        public List<Urun> SliderUrunler { get; set; } = new();
        public string? Filtre { get; set; }
        public int? KategoriId { get; set; } // ← eksik olan burası

    }
}
