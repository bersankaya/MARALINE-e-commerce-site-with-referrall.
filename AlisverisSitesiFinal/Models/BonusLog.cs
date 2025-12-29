using System;

namespace AlisverisSitesiFinal.Models
{
    public class BonusLog
    {
        public int Id { get; set; }
        public string KullaniciId { get; set; } = string.Empty;
        public DateTime Tarih { get; set; }
        public string Aciklama { get; set; } = string.Empty;
        public decimal Tutar { get; set; }  // Bonus miktarı
    }
}
