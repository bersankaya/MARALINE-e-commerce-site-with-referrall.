namespace AlisverisSitesiFinal.Models
{
    public enum HizmetBedeliModu
    {
        Sabit = 0,
        DagitilanBonusaEsitle = 1
    }

    public class ReferralConfig
    {
        public decimal BonusMiktari { get; set; } = 200m;
        public int ReferralLimit { get; set; } = 2;
        public int AdminReferralLimit { get; set; } = 10;
        public decimal EarningCap { get; set; } = 20000m;
        public bool AdminHasEarningCap { get; set; } = false;
        public decimal AktiflikHarcamaLimiti { get; set; } = 5000;

        public HizmetBedeliModu HizmetBedeliModu { get; set; } = HizmetBedeliModu.DagitilanBonusaEsitle;
        public decimal SabitHizmetBedeli { get; set; } = 200m;    // Mod Sabit ise
        public decimal MinHizmetBedeli { get; set; } = 0m;        // Alt limit
    }
}
