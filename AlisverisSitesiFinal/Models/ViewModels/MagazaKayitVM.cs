namespace AlisverisSitesiFinal.Models.ViewModels
{
    public class MagazaKayitVM
    {
        // Kullanıcı bilgileri
        public string Ad { get; set; } = "";
        public string Soyad { get; set; } = "";
        public string Email { get; set; } = "";
        public string Sifre { get; set; } = "";

        // Mağaza bilgileri
        public string MagazaAdi { get; set; } = "";
        public string VergiNo { get; set; } = "";
        public string? IBAN { get; set; }
        public string? Aciklama { get; set; }
    }
}
