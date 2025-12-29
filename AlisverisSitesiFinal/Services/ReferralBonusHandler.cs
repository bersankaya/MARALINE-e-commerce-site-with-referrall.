using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AlisverisSitesiFinal.Services
{
    public class ReferralBonusHandler
    {
        private readonly UygulamaDbContext _context;
        private readonly UserManager<Kullanici> _userManager;
        private readonly ReferralConfig _config;

        // Recalc sırasında pasif bakiyeyi otomatik doldurmayı BASKILAMAK için bayrak
        private bool _suppressMonthlyGrant = false;

        public ReferralBonusHandler(
            UygulamaDbContext context,
            UserManager<Kullanici> userManager,
            IOptions<ReferralConfig> config)
        {
            _context = context;
            _userManager = userManager;
            _config = config.Value;
        }

        // ==== PUBLIC ====
        public async Task HandleReferralAsync(Kullanici yeniKullanici, bool force = false)
            => await HandleReferralInternalAsync(yeniKullanici, force, orderRefKey: null);

        // ==== CORE ====
        private async Task HandleReferralInternalAsync(Kullanici yeniKullanici, bool force, string? orderRefKey)
        {
            if (string.IsNullOrEmpty(yeniKullanici.SponsorId)) return;

            // Eşiği henüz geçmemişse, yalnızca force true ise ya da gerçek harcama ile geçildiyse devam
            if (!yeniKullanici.HasMetReferralThreshold || force)
            {
                if (yeniKullanici.ToplamHarcama < _config.AktiflikHarcamaLimiti && !force)
                    return;

                if (!yeniKullanici.HasMetReferralThreshold)
                {
                    // KULLANICI ARTIK AKTİF: Eşik geçildiğinde kendi referans kodu da aktifleşir
                    yeniKullanici.HasMetReferralThreshold = true;
                    yeniKullanici.IsReferralCodeActive = true; // ✅ EKLENDİ
                    _context.Users.Update(yeniKullanici);
                    await _context.SaveChangesAsync();
                }
            }

            var sponsor = await _context.Users.FirstOrDefaultAsync(u => u.Id == yeniKullanici.SponsorId);
            if (sponsor == null) return;

            bool isAdmin = await _userManager.IsInRoleAsync(sponsor, "Admin");
            int referralLimit = sponsor.OzelReferansLimiti ?? 2;

            // Pasif referans limiti kontrolü
            var pasifRefList = await _context.Users
                .Where(u => u.SponsorId == sponsor.Id && !u.HasMetReferralThreshold)
                .OrderBy(u => u.KayitTarihi)
                .ToListAsync();

            int aktifCountNow = await _context.Users
                .CountAsync(u => u.SponsorId == sponsor.Id && u.HasMetReferralThreshold);

            if (aktifCountNow >= referralLimit && pasifRefList.Any())
            {
                var detach = pasifRefList.First();
                detach.SponsorId = null;
                _context.Users.Update(detach);
                await _context.SaveChangesAsync();
            }

            // Aktif sayıyı güncelle
            aktifCountNow = await _context.Users
                .CountAsync(u => u.SponsorId == sponsor.Id && u.HasMetReferralThreshold);

            sponsor.ActiveDirectReferralCount = aktifCountNow;
            _context.Users.Update(sponsor);
            await _context.SaveChangesAsync();

            // CAP kontrolü: SADECE motor (doğrudan+zincir) loglarının toplamına göre
            var engineSoFar = await GetEngineTotalAsync(sponsor.Id);
            bool capReached = engineSoFar >= _config.EarningCap;

            // Doğrudan bonus (2/4/6/...) — admin alamaz, CAP'i geçmişse durur
            if (!isAdmin && !capReached && aktifCountNow >= 2 && aktifCountNow % 2 == 0)
            {
                int pairCount = aktifCountNow;
                string baseAciklama = $"Doğrudan bonus - pair-{pairCount}";
                string aciklama = string.IsNullOrWhiteSpace(orderRefKey) ? baseAciklama : $"{baseAciklama} - {orderRefKey}";

                bool bonusZatenVerildi = await _context.BonusLoglari
                    .AnyAsync(b => b.KullaniciId == sponsor.Id && b.Aciklama.StartsWith(baseAciklama));

                if (!bonusZatenVerildi)
                {
                    // Logla → ardından cüzdanı loglardan türet
                    _context.BonusLoglari.Add(new BonusLog
                    {
                        KullaniciId = sponsor.Id,
                        Tutar = _config.BonusMiktari,
                        Tarih = DateTime.Now,
                        Aciklama = aciklama
                    });
                    await _context.SaveChangesAsync();

                    await RecalcUserWalletAsync(sponsor);
                    await _context.SaveChangesAsync();

                    await CheckAndGrantPassiveIncomeAsync(sponsor); // (recalc sırasında baskılanır)
                    await HandleChainBonusAsync_WithOrderKey(sponsor.SponsorId, sponsor.Id, pairCount, orderRefKey);
                }
            }
        }

        private async Task HandleChainBonusAsync(string? sponsorId, string pairOwnerId, int pairCount)
            => await HandleChainBonusAsync_WithOrderKey(sponsorId, pairOwnerId, pairCount, orderRefKey: null);

        private async Task HandleChainBonusAsync_WithOrderKey(string? sponsorId, string pairOwnerId, int pairCount, string? orderRefKey)
        {
            int seviye = 1;
            while (!string.IsNullOrEmpty(sponsorId) && seviye <= 10)
            {
                var sponsor = await _context.Users.FindAsync(sponsorId);
                if (sponsor == null) break;

                bool isAdmin = await _userManager.IsInRoleAsync(sponsor, "Admin");
                var engineSoFar = await GetEngineTotalAsync(sponsor.Id);
                bool capReached = engineSoFar >= _config.EarningCap;

                if (!isAdmin && !capReached)
                {
                    string baseAciklama = $"{seviye}. seviye zincir bonus - {pairOwnerId} - pair-{pairCount}";
                    string aciklama = string.IsNullOrWhiteSpace(orderRefKey) ? baseAciklama : $"{baseAciklama} - {orderRefKey}";

                    bool bonusZatenVerildi = await _context.BonusLoglari
                        .AnyAsync(b => b.KullaniciId == sponsor.Id && b.Aciklama.StartsWith(baseAciklama));

                    if (!bonusZatenVerildi)
                    {
                        _context.BonusLoglari.Add(new BonusLog
                        {
                            KullaniciId = sponsor.Id,
                            Tutar = _config.BonusMiktari,
                            Tarih = DateTime.Now,
                            Aciklama = aciklama
                        });
                        await _context.SaveChangesAsync();

                        await RecalcUserWalletAsync(sponsor);
                        await _context.SaveChangesAsync();

                        await CheckAndGrantPassiveIncomeAsync(sponsor);
                    }
                }

                sponsorId = sponsor.SponsorId;
                seviye++;
            }
        }

        // ==== PASİF HAK ====
        // CAP'e ulaşınca: bilgi logu + (recalc sırasında DEĞİLSE) AYLIK bakiyeyi ilk kez CAP'e doldur.
        private async Task CheckAndGrantPassiveIncomeAsync(Kullanici user)
        {
            if (await _userManager.IsInRoleAsync(user, "Admin")) return;

            var engineSoFar = await GetEngineTotalAsync(user.Id);
            if (engineSoFar < _config.EarningCap) return;

            // Bilgi logu (bir kez)
            bool infoLogged = await _context.BonusLoglari
                .AnyAsync(b => b.KullaniciId == user.Id &&
                               b.Tutar == 0m &&
                               b.Aciklama.StartsWith("Pasif gelir hakkı kazanıldı"));
            if (!infoLogged)
            {
                _context.BonusLoglari.Add(new BonusLog
                {
                    KullaniciId = user.Id,
                    Tutar = 0m,
                    Tarih = DateTime.Now,
                    Aciklama = $"Pasif gelir hakkı kazanıldı (aylık {_config.EarningCap:N0} TL)"
                });
                await _context.SaveChangesAsync();
            }

            // Recalc esnasında AylikKazanilanPara'yı doldurma (baskı)
            if (_suppressMonthlyGrant) return;

            // İlk kez dolum (mevcut 0 ise)
            if (user.AylikKazanilanPara <= 0m)
            {
                user.AylikKazanilanPara = _config.EarningCap;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }
        }

        // === AYLIK YÜKLEME ===
        // Her ayın 1'i/30 günde bir çağrılır: hak sahiplerinin AylikKazanilanPara'sını CAP'e çeker (ayda bir).
        public async Task<int> RefillMonthlyPassiveAsync()
        {
            // Bu akışta pasif yükleme AÇIK kalsın (recalc baskısından bağımsız)
            var prev = _suppressMonthlyGrant;
            _suppressMonthlyGrant = false;
            try
            {
                var now = DateTime.Now;
                var monthStart = new DateTime(now.Year, now.Month, 1);
                var monthEnd = monthStart.AddMonths(1);

                var users = await _context.Users.ToListAsync();
                int refilled = 0;

                foreach (var u in users)
                {
                    // Admin asla pasif alamaz
                    if (await _userManager.IsInRoleAsync(u, "Admin"))
                    {
                        if (u.AylikKazanilanPara != 0m)
                        {
                            u.AylikKazanilanPara = 0m;
                            _context.Users.Update(u);
                        }
                        continue;
                    }

                    // Pasif hak: motor (doğrudan+zincir) log toplamı CAP'e ulaşmış olmalı
                    var engineSoFar = await _context.BonusLoglari
                        .Where(b => b.KullaniciId == u.Id &&
                                    (b.Aciklama.StartsWith("Doğrudan bonus - pair-") ||
                                     EF.Functions.Like(b.Aciklama, "% seviye zincir bonus - %")))
                        .SumAsync(b => (decimal?)b.Tutar) ?? 0m;

                    if (engineSoFar < _config.EarningCap) continue;

                    // Bu ay zaten yüklenmiş mi?
                    bool alreadyRefilled = await _context.BonusLoglari.AnyAsync(b =>
                        b.KullaniciId == u.Id &&
                        b.Aciklama.StartsWith("Aylık pasif gelir yüklemesi") &&
                        b.Tarih >= monthStart && b.Tarih < monthEnd);

                    if (alreadyRefilled) continue;

                    // YÜKLEME: aylık bakiye CAP kadar doldurulur (cüzdana eklenmez)
                    u.AylikKazanilanPara = _config.EarningCap;
                    _context.Users.Update(u);

                    _context.BonusLoglari.Add(new BonusLog
                    {
                        KullaniciId = u.Id,
                        Tutar = 0m,
                        Tarih = now,
                        Aciklama = $"Aylık pasif gelir yüklemesi {now:yyyy-MM}"
                    });

                    refilled++;
                }

                await _context.SaveChangesAsync();
                return refilled;
            }
            finally
            {
                // Bayrağı eski haline döndür (güvenli)
                _suppressMonthlyGrant = prev;
            }
        }


        private async Task<bool> HasUnapprovedOrdersAsync(string userId)
        {
            // SQL Server çoğunlukla case-insensitive olduğu için direkt != kullanmak yeterli.
            return await _context.Siparisler.AnyAsync(s =>
                s.IdentityUserId == userId &&
                s.Durum != "Onaylandı");
        }


        public async Task<int> DistributeMonthlyPassiveAsync()
        {
            var now = DateTime.Now;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var users = await _context.Users
                .Where(u => u.AylikKazanilanPara > 0)
                .ToListAsync();

            int paid = 0;
            foreach (var u in users)
            {
                if (await _userManager.IsInRoleAsync(u, "Admin"))
                {
                    if (u.AylikKazanilanPara != 0m) { u.AylikKazanilanPara = 0m; _context.Users.Update(u); }
                    continue;
                }

                // ✅ Satıcısı onaylamamış siparişi olan kullanıcıya PASİF DAĞITMA
                if (await HasUnapprovedOrdersAsync(u.Id)) continue;

                bool alreadyThisMonth = await _context.BonusLoglari.AnyAsync(b =>
                    b.KullaniciId == u.Id &&
                    b.Aciklama.StartsWith("Aylık pasif gelir ödemesi") &&
                    b.Tarih >= monthStart && b.Tarih < monthEnd);

                if (alreadyThisMonth) continue;
                if (u.AylikKazanilanPara <= 0) continue;

                _context.BonusLoglari.Add(new BonusLog
                {
                    KullaniciId = u.Id,
                    Tutar = u.AylikKazanilanPara,
                    Tarih = now,
                    Aciklama = $"Aylık pasif gelir ödemesi {now:yyyy-MM}"
                });

                u.AylikKazanilanPara = 0m;
                _context.Users.Update(u);

                await _context.SaveChangesAsync();
                await RecalcUserWalletAsync(u);
                await _context.SaveChangesAsync();

                paid++;
            }

            return paid;
        }


        // ==== RE-CALC ALL (Bonusları Temizle & Güncelle) ====
        // Sadece motor loglarını temizler ve yeniden üretir; AylikKazanilanPara'ya DOKUNMAZ ve pasif grant'i BASKILAR.

        public async Task RecalculateAllEarningsAsync()
        {
            var prev = _suppressMonthlyGrant;
            _suppressMonthlyGrant = true; // recalc boyunca pasif aylık dolumu kapalı
            try
            {
                var users = await _context.Users.ToListAsync();

                // 1) SADECE motor loglarını sil (doğrudan + zincir) — pasif ödeme logları KALIR!
                var engineLogs = await _context.BonusLoglari
                    .Where(b => b.Aciklama.StartsWith("Doğrudan bonus - pair-") ||
                                EF.Functions.Like(b.Aciklama, "% seviye zincir bonus - %"))
                    .ToListAsync();

                if (engineLogs.Any())
                {
                    _context.BonusLoglari.RemoveRange(engineLogs);
                    await _context.SaveChangesAsync();
                }

                // 2) Kullanıcı metriklerini resetle (AylikKazanilanPara'ya DOKUNMA!)
                foreach (var k in users)
                {
                    k.ToplamKazanilanPara = 0m;
                    k.ToplamKazanc = 0m;
                    k.ActiveDirectReferralCount = 0;
                    k.HasMetReferralThreshold = false;
                    k.IsReferralCodeActive = false;
                    _context.Users.Update(k);
                }
                await _context.SaveChangesAsync();

                // 3) Eşiği geçenleri tekrar işle
                foreach (var k in users.OrderBy(u => u.KayitTarihi))
                {
                    if (k.ToplamHarcama >= _config.AktiflikHarcamaLimiti && !string.IsNullOrEmpty(k.SponsorId))
                        await HandleReferralInternalAsync(k, force: true, orderRefKey: null);
                }

                // 4) Cüzdanları loglardan türet
                foreach (var k in users)
                    await RecalcUserWalletAsync(k);

                await _context.SaveChangesAsync();
            }
            finally
            {
                _suppressMonthlyGrant = prev; // önceki duruma dön (daha güvenli)
            }
        }


        // ==== SİPARİŞ ====
        public async Task ApplyOrderEffectsAsync(Siparis siparis)
        {
            if (siparis == null) return;

            // ✅ Sadece SATICI ONAYLI siparişler referral motorunu tetikler
            if (!string.Equals(siparis.Durum, "Onaylandı", StringComparison.OrdinalIgnoreCase)) return;

            if (siparis.ReferralIslenmisMi) return;

            var buyer = await _context.Users.FirstOrDefaultAsync(u => u.Id == siparis.IdentityUserId);
            if (buyer == null) return;

            // ToplamHarcama sadece onaylı siparişlerle artar ⇒
            // referans eşiği ve bonuslar ancak satıcı onayından sonra çalışır
            buyer.ToplamHarcama += siparis.ToplamTutar;
            _context.Users.Update(buyer);
            await _context.SaveChangesAsync();

            if (!buyer.HasMetReferralThreshold && buyer.ToplamHarcama >= _config.AktiflikHarcamaLimiti)
            {
                // İçeride hem HasMetReferralThreshold hem IsReferralCodeActive true yapılır
                await HandleReferralInternalAsync(buyer, force: true, orderRefKey: siparis.OrderRefKey);
            }

            siparis.ReferralIslenmisMi = true;
            _context.Entry(siparis).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        public async Task RevertOrderEffectsAsync(Siparis siparis)
        {
            if (siparis == null) return;
            if (!siparis.ReferralIslenmisMi && string.IsNullOrWhiteSpace(siparis.OrderRefKey)) return;

            var buyer = await _context.Users.FirstOrDefaultAsync(u => u.Id == siparis.IdentityUserId);
            var affectedUserIds = new HashSet<string>();

            if (buyer != null)
            {
                buyer.ToplamHarcama = Math.Max(0, buyer.ToplamHarcama - siparis.ToplamTutar);

                if (buyer.HasMetReferralThreshold && buyer.ToplamHarcama < _config.AktiflikHarcamaLimiti)
                {
                    buyer.HasMetReferralThreshold = false;
                    buyer.IsReferralCodeActive = false; // ✅ EKLENDİ — eşik altına düştüyse kod pasifleşir
                }

                _context.Users.Update(buyer);
                await _context.SaveChangesAsync();

                if (!string.IsNullOrEmpty(buyer.SponsorId))
                {
                    var sponsor = await _context.Users.FirstOrDefaultAsync(u => u.Id == buyer.SponsorId);
                    if (sponsor != null)
                    {
                        sponsor.ActiveDirectReferralCount = await _context.Users
                            .CountAsync(u => u.SponsorId == sponsor.Id && u.HasMetReferralThreshold);

                        _context.Users.Update(sponsor);
                        await _context.SaveChangesAsync();

                        await RemoveInvalidPairBonusesAsync(sponsor, affectedUserIds);

                        affectedUserIds.Add(sponsor.Id);
                        foreach (var uid in await GetUpperChainAsync(sponsor.Id, 10))
                            affectedUserIds.Add(uid);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(siparis.OrderRefKey))
            {
                var relatedLogs = await _context.BonusLoglari
                    .Where(b => b.Aciklama.Contains(siparis.OrderRefKey))
                    .ToListAsync();

                if (relatedLogs.Any())
                {
                    _context.BonusLoglari.RemoveRange(relatedLogs);
                    await _context.SaveChangesAsync();

                    foreach (var log in relatedLogs)
                        affectedUserIds.Add(log.KullaniciId);
                }
            }

            if (affectedUserIds.Any())
            {
                foreach (var uid in affectedUserIds)
                {
                    var u = await _context.Users.FirstOrDefaultAsync(x => x.Id == uid);
                    if (u != null) await RecalcUserWalletAsync(u);
                    if (u != null) await EnsurePassiveEligibilityAsync(u);
                }
                await _context.SaveChangesAsync();
            }

            siparis.ReferralIslenmisMi = false;
            _context.Entry(siparis).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        // ==== GEÇERSİZ PAIR TEMİZLİĞİ ====
        private async Task RemoveInvalidPairBonusesAsync(Kullanici pairOwner, HashSet<string> affectedUserIds)
        {
            int currentActive = await _context.Users
                .CountAsync(u => u.SponsorId == pairOwner.Id && u.HasMetReferralThreshold);

            int highestEvenNow = (currentActive / 2) * 2;

            var directLogs = await _context.BonusLoglari
                .Where(b => b.KullaniciId == pairOwner.Id && b.Aciklama.StartsWith("Doğrudan bonus - pair-"))
                .ToListAsync();

            var invalidDirect = directLogs
                .Select(l => new { Log = l, Pair = ParsePairNumberFromDirect(l.Aciklama) })
                .Where(x => x.Pair.HasValue && x.Pair.Value > highestEvenNow)
                .ToList();

            if (!invalidDirect.Any()) return;

            var invalidPairNumbers = invalidDirect.Select(x => x.Pair!.Value).Distinct().ToList();

            var chainLogs = await _context.BonusLoglari
                .Where(b => b.Aciklama.Contains($" - {pairOwner.Id} - pair-"))
                .ToListAsync();

            var invalidChain = chainLogs
                .Where(cl =>
                {
                    var pair = ParsePairNumberFromChain(cl.Aciklama);
                    var owner = ParsePairOwnerFromChain(cl.Aciklama);
                    return owner == pairOwner.Id && pair.HasValue && invalidPairNumbers.Contains(pair.Value);
                })
                .ToList();

            // Logları kaldır, alanlara dokunma → recalc dışarıda çağrılacak
            if (invalidDirect.Any()) _context.BonusLoglari.RemoveRange(invalidDirect.Select(x => x.Log));
            if (invalidChain.Any()) _context.BonusLoglari.RemoveRange(invalidChain);
            await _context.SaveChangesAsync();

            var affected = invalidDirect.Select(x => x.Log.KullaniciId)
                .Concat(invalidChain.Select(x => x.KullaniciId))
                .Distinct();

            foreach (var uid in affected) affectedUserIds.Add(uid);
        }

        // ==== CÜZDAN (tek kaynak: loglar) ====
        private async Task RecalcUserWalletAsync(Kullanici u)
        {
            // ToplamKazanılanPara = POZİTİF motor + pasif ÖDEME logları (yükleme/bilgi logları 0 tutarlı)
            var totalEngine = await _context.BonusLoglari
                .Where(b => b.KullaniciId == u.Id &&
                    (b.Aciklama.StartsWith("Doğrudan bonus - pair-") ||
                     EF.Functions.Like(b.Aciklama, "% seviye zincir bonus - %") ||
                     b.Aciklama.StartsWith("Aylık pasif gelir ödemesi")))
                .SumAsync(b => (decimal?)b.Tutar) ?? 0m;

            var approvedWithdrawals = await _context.ParaCekmeTalepleri
                .Where(x => x.KullaniciId == u.Id && x.OnaylandiMi)
                .SumAsync(x => (decimal?)x.Tutar) ?? 0m;

            u.ToplamKazanilanPara = totalEngine;                        // değişmez: tüm pozitif kazançlar
            u.ToplamKazanc = Math.Max(0, totalEngine - approvedWithdrawals); // cüzdan

            if (await _userManager.IsInRoleAsync(u, "Admin"))
                u.AylikKazanilanPara = 0m; // admin asla pasif alamaz

            _context.Users.Update(u);
        }

        // ==== HELPERS ====
        private async Task<decimal> GetEngineTotalAsync(string userId)
        {
            return await _context.BonusLoglari
                .Where(b => b.KullaniciId == userId &&
                            (b.Aciklama.StartsWith("Doğrudan bonus - pair-") ||
                             EF.Functions.Like(b.Aciklama, "% seviye zincir bonus - %")))
                .SumAsync(b => (decimal?)b.Tutar) ?? 0m;
        }

        private static int? ParsePairNumberFromDirect(string aciklama)
        {
            try
            {
                var idx = aciklama.IndexOf("pair-");
                if (idx < 0) return null;
                var tail = aciklama[(idx + 5)..];
                var numStr = new string(tail.TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(numStr, out var n)) return n;
            }
            catch { }
            return null;
        }

        private static int? ParsePairNumberFromChain(string aciklama)
        {
            try
            {
                var idx = aciklama.LastIndexOf("pair-");
                if (idx < 0) return null;
                var tail = aciklama[(idx + 5)..];
                var numStr = new string(tail.TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(numStr, out var n)) return n;
            }
            catch { }
            return null;
        }

        private static string? ParsePairOwnerFromChain(string aciklama)
        {
            try
            {
                var marker = "zincir bonus - ";
                var idx = aciklama.IndexOf(marker);
                if (idx < 0) return null;
                var tail = aciklama[(idx + marker.Length)..]; // "{ownerId} - pair-<N>..."
                var owner = tail.Split(" - pair-").FirstOrDefault();
                return string.IsNullOrWhiteSpace(owner) ? null : owner.Trim();
            }
            catch { }
            return null;
        }

        private async Task<List<string>> GetUpperChainAsync(string startUserId, int maxLevel)
        {
            var list = new List<string>();
            var currentId = startUserId;
            int level = 0;
            while (!string.IsNullOrEmpty(currentId) && level < maxLevel)
            {
                var u = await _context.Users.FindAsync(currentId);
                if (u == null || string.IsNullOrEmpty(u.SponsorId)) break;
                var s = await _context.Users.FindAsync(u.SponsorId);
                if (s == null) break;
                list.Add(s.Id);
                currentId = s.Id;
                level++;
            }
            return list;
        }

        private async Task EnsurePassiveEligibilityAsync(Kullanici u)
        {
            var engineSoFar = await _context.BonusLoglari
                .Where(b => b.KullaniciId == u.Id &&
                            (b.Aciklama.StartsWith("Doğrudan bonus - pair-") ||
                             EF.Functions.Like(b.Aciklama, "% seviye zincir bonus - %")))
                .SumAsync(b => (decimal?)b.Tutar) ?? 0m;

            if (engineSoFar < _config.EarningCap && u.AylikKazanilanPara > 0m)
            {
                u.AylikKazanilanPara = 0m; // henüz dağıtılmadıysa, hakkı düşmüştür
                _context.Users.Update(u);
                await _context.SaveChangesAsync();
            }
        }
        // 📌 Bu siparişe ait (pozitif) doğrudan + zincir bonus toplamını döndürür
        public async Task<decimal> GetOrderDistributedBonusTotalAsync(string? orderRefKey)
        {
            if (string.IsNullOrWhiteSpace(orderRefKey)) return 0m;

            var total = await _context.BonusLoglari
                .Where(b => b.Tutar > 0 &&
                            b.Aciklama.Contains(orderRefKey) &&
                            (b.Aciklama.StartsWith("Doğrudan bonus - pair-") ||
                             EF.Functions.Like(b.Aciklama, "% seviye zincir bonus - %")))
                .SumAsync(b => (decimal?)b.Tutar) ?? 0m;

            return total;
        }

    }
}
