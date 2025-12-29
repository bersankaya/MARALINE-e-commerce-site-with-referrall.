using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using Microsoft.EntityFrameworkCore;

namespace AlisverisSitesiFinal.Services
{
    public class InvoiceService
    {
        private readonly UygulamaDbContext _db;
        public InvoiceService(UygulamaDbContext db) { _db = db; }

        // Şimdilik "mock": numara üretip siparişe yazar. Entegratör bağlanınca burada gerçek API'yi çağıracaksın.
        public async Task<bool> CreateForOrderAsync(int siparisId)
        {
            var s = await _db.Siparisler.FirstOrDefaultAsync(x => x.Id == siparisId);
            if (s == null) return false;

            // zaten kesilmişse tekrar yazma
            if (string.Equals(s.FaturaDurumu, "Kesildi", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(s.EFaturaNo))
                return true;

            s.FaturaDurumu = "Kesildi";
            s.EFaturaNo = $"MAL{DateTime.Now:yyyyMMdd}-{siparisId:D6}";
            s.EFaturaTarihi = DateTime.Now;

            _db.Siparisler.Update(s);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
