using System.Collections.Generic;

namespace AlisverisSitesiFinal.Models
{
    public class BenimReferanslarimViewModel
    {
        public Kullanici Kullanici { get; set; } = new Kullanici();
        public List<Kullanici> Referanslar { get; set; } = new();
        public List<BonusLog> BonusLoglar { get; set; } = new();

        public ReferralNode ReferralTreeRoot { get; set; } = new ReferralNode();
        public decimal ToplamKazanilanPara { get; set; }
        public decimal EarningCap { get; set; }
        public decimal AktiflikHarcamaLimiti { get; set; }

    }

    public class ReferralNode
    {
        public string AdSoyad { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal ToplamHarcama { get; set; }
        public bool AktifMi { get; set; }
        public string KayitTarihi { get; set; } = string.Empty;
        public List<ReferralNode> Cocuklar { get; set; } = new();
    }

}
