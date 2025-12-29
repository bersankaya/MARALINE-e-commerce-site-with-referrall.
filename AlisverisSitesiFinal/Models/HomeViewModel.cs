using System.Collections.Generic;

namespace AlisverisSitesiFinal.Models
{
    public class HomeViewModel
    {
        public List<Urun> SliderUrunler { get; set; } = new();
        public List<Urun> PopulerUrunler { get; set; } = new();
        public List<Urun> YeniUrunler { get; set; } = new();
        public List<Urun> EnCokSatanlar { get; set; } = new();
        public List<Urun> AvantajliUrunler { get; set; } = new();
        public List<Kategori> Kategoriler { get; set; } = new();
        public List<Urun> TumUrunler { get; set; } =new();
        public string? Filtre { get; set; }


    }
}
