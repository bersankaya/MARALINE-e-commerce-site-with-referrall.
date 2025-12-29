using System;

namespace AlisverisSitesiFinal.Models.ViewModels
{
    public class SaticiListItemVM
    {
        public int MagazaId { get; set; }
        public string MagazaAdi { get; set; } = "";
        public string OwnerUserId { get; set; } = "";

        public string SaticiAdSoyad { get; set; } = "";
        public string Email { get; set; } = "";
        public string SaticiRolu { get; set; } = ""; // Satici | SaticiAday | (rol yok)

        public bool AktifMi { get; set; }
        public DateTime OlusturmaTarihi { get; set; }

        public string? VergiNo { get; set; }
        public string? IBAN { get; set; }
        public string? Telefon { get; set; }
        public string? Adres { get; set; }
    }
}
