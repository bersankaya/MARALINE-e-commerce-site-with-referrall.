
using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using Microsoft.EntityFrameworkCore;

namespace AlisverisSitesiFinal.Services
{
    public class ReferralTreeBuilder
    {
        private readonly UygulamaDbContext _context;

        public ReferralTreeBuilder(UygulamaDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Belirtilen kullanıcının referans ağacını oluşturur (tüm seviyeler).
        /// </summary>
        public async Task<ReferralNode> BuildTreeAsync(Kullanici rootUser)
        {
            if (rootUser == null)
                return null!;

            var rootNode = new ReferralNode
            {
                AdSoyad = rootUser.Ad + " " + rootUser.Soyad,
                Email = rootUser.Email!,
                ToplamHarcama = rootUser.ToplamHarcama,
                AktifMi = rootUser.HasMetReferralThreshold,
                KayitTarihi = rootUser.KayitTarihi.ToString("dd.MM.yyyy"),
                Cocuklar = new List<ReferralNode>()
            };

            await AddChildrenRecursiveAsync(rootNode, rootUser.Id);
            return rootNode;
        }

        /// <summary>
        /// Her referansın çocuklarını rekürsif olarak ekler.
        /// </summary>
        private async Task AddChildrenRecursiveAsync(ReferralNode parentNode, string sponsorId)
        {
            var children = await _context.Users
                .Where(u => u.SponsorId == sponsorId)
                .OrderBy(u => u.KayitTarihi)
                .ToListAsync();

            foreach (var child in children)
            {
                var childNode = new ReferralNode
                {
                    AdSoyad = child.Ad + " " + child.Soyad,
                    Email = child.Email!,
                    ToplamHarcama = child.ToplamHarcama,
                    AktifMi = child.HasMetReferralThreshold,
                    KayitTarihi = child.KayitTarihi.ToString("dd.MM.yyyy"),
                    Cocuklar = new List<ReferralNode>()
                };

                parentNode.Cocuklar.Add(childNode);

                // Rekürsif olarak alt çocukları da ekle
                await AddChildrenRecursiveAsync(childNode, child.Id);
            }
        }
    }
}
