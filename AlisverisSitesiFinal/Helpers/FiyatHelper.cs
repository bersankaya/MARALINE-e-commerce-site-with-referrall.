using AlisverisSitesiFinal.Models;

namespace AlisverisSitesiFinal.Helpers
{
    public static class FiyatHelper
    {
        public static decimal? GetGorunecekFiyat(Kullanici? user, Urun urun)
        {
            // Eğer kullanıcı giriş yapmış ve referans avantajına sahipse
            if (user != null && (user.HasMetReferralThreshold || user.IsReferralCodeActive))
            {
                return urun.FiyatReferansli ?? urun.FiyatAdmin;
            }

            // Aksi halde normal fiyat
            return urun.FiyatAdmin ?? urun.FiyatReferansli;
        }
    }
}
