using System;

namespace AlisverisSitesiFinal.Models.ViewModels
{
    public class DavetRaporSatir
    {
        public string UserId { get; set; } = "";
        public string Email { get; set; } = "";
        public string? AdSoyad { get; set; }
        public string Rol { get; set; } = "Musteri";
        public int EtkinLimit { get; set; }
        public string LimitKaynak { get; set; } = "Genel"; // Özel/Admin/Genel
        public int ToplamCocuk { get; set; }
        public int AktifCocuk { get; set; }
        public int PasifCocuk { get; set; }
    }
}
