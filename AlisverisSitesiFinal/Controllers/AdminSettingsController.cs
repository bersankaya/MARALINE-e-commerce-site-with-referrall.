using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Text;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminSettingsController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;
        private readonly UygulamaDbContext _context;
        private readonly ILogger<AdminSettingsController> _logger;

        public AdminSettingsController(
            IConfiguration configuration,
            IWebHostEnvironment env,
            UygulamaDbContext context,
            ILogger<AdminSettingsController> logger)
        {
            _configuration = configuration;
            _env = env;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index([FromServices] IOptionsMonitor<ReferralConfig> opts, bool debug = false)
        {
            var model = opts.CurrentValue ?? new ReferralConfig(); // <= Snapshot değil Monitor

            if (debug)
            {
                var contentRoot = _env.ContentRootPath ?? string.Empty;
                var envFile = Path.Combine(contentRoot, $"appsettings.{_env.EnvironmentName}.json");
                var baseFile = Path.Combine(contentRoot, "appsettings.json");

                var cfg = new ReferralConfig();
                _configuration.GetSection("ReferralConfig").Bind(cfg);

                TempData["DebugInfo"] =
                    $"ENV='{_env.EnvironmentName}', Base='{baseFile}', EnvFile='{envFile}'. " +
                    $"Runtime(Bonus={cfg.BonusMiktari}, Cap={cfg.EarningCap}, Aktiflik={cfg.AktiflikHarcamaLimiti}). " +
                    $"Monitor(Bonus={model.BonusMiktari}, Cap={model.EarningCap}, Aktiflik={model.AktiflikHarcamaLimiti}).";
            }

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(ReferralConfig model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var contentRoot = _env.ContentRootPath ?? string.Empty;

                var envFilePath = Path.Combine(contentRoot, $"appsettings.{_env.EnvironmentName}.json");
                var baseFilePath = Path.Combine(contentRoot, "appsettings.json");

                // Hedef dosya: ENV varsa önce oraya yaz, ama senkron kalması için base’e de yaz.
                WriteReferralConfig(baseFilePath, model);
                if (System.IO.File.Exists(envFilePath))
                    WriteReferralConfig(envFilePath, model);

                // ⚡ Bellekteki konfigürasyonu anında tazele
                if (_configuration is IConfigurationRoot rootConfig)
                    rootConfig.Reload();

                // Yazdıktan sonra geri oku (gerçekten yansıdı mı?)
                var verify = new ReferralConfig();
                _configuration.GetSection("ReferralConfig").Bind(verify);

                TempData["SuccessMessage"] =
                    $"Ayarlar güncellendi. (ENV: {_env.EnvironmentName}) " +
                    $"→ Bonus={verify.BonusMiktari}, Cap={verify.EarningCap}, Aktiflik={verify.AktiflikHarcamaLimiti}";

                return RedirectToAction(nameof(Index), new { debug = true }); // 1 kez debug göster
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Ayar kaydetme hatası. Environment: {Env}", _env.EnvironmentName);
                TempData["ErrorMessage"] = "Ayarlar kaydedilemedi. Lütfen tekrar deneyin.";
                return RedirectToAction(nameof(Index));
            }
        }

        private static void WriteReferralConfig(string path, ReferralConfig model)
        {
            // Dosya yoksa oluştur
            if (!System.IO.File.Exists(path))
            {
                var empty = new JObject();
                System.IO.File.WriteAllText(path, empty.ToString(Newtonsoft.Json.Formatting.Indented), Encoding.UTF8);
            }

            var json = System.IO.File.ReadAllText(path, Encoding.UTF8);
            var jObj = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);

            if (jObj["ReferralConfig"] == null)
                jObj["ReferralConfig"] = new JObject();

            var cfg = (JObject)jObj["ReferralConfig"]!;
            cfg["BonusMiktari"] = JToken.FromObject(model.BonusMiktari);
            cfg["ReferralLimit"] = JToken.FromObject(model.ReferralLimit);
            cfg["AdminReferralLimit"] = JToken.FromObject(model.AdminReferralLimit);
            cfg["EarningCap"] = JToken.FromObject(model.EarningCap);
            cfg["AdminHasEarningCap"] = JToken.FromObject(model.AdminHasEarningCap);
            cfg["AktiflikHarcamaLimiti"] = JToken.FromObject(model.AktiflikHarcamaLimiti);
            cfg["HizmetBedeliModu"] = JToken.FromObject(model.HizmetBedeliModu);
            cfg["SabitHizmetBedeli"] = JToken.FromObject(model.SabitHizmetBedeli);
            cfg["MinHizmetBedeli"] = JToken.FromObject(model.MinHizmetBedeli);

            System.IO.File.WriteAllText(path, jObj.ToString(Newtonsoft.Json.Formatting.Indented), Encoding.UTF8);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> KullaniciyaLimitVer(KullaniciLimitViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                TempData["ErrorMessage"] = "❌ E-posta boş olamaz.";
                return RedirectToAction("Index");
            }

            var norm = model.Email.Trim().ToUpperInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == norm);
            if (user == null)
            {
                TempData["ErrorMessage"] = "❌ Kullanıcı bulunamadı.";
                return RedirectToAction("Index");
            }

            user.OzelReferansLimiti = model.YeniLimit;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ {user.Email} için referans limiti {model.YeniLimit} olarak ayarlandı.";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> KimKacKisi()
        {
            var cfg = new ReferralConfig();
            _configuration.GetSection("ReferralConfig").Bind(cfg);

            var userRoles =
                from ur in _context.UserRoles
                join r in _context.Roles on ur.RoleId equals r.Id
                select new { ur.UserId, RoleName = r.Name };

            var users = await _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Ad,
                    u.Soyad,
                    u.OzelReferansLimiti,
                    u.IsReferralCodeActive,
                    u.HasMetReferralThreshold
                })
                .ToListAsync();

            var childStatsList = await _context.Users
                .Where(x => !string.IsNullOrEmpty(x.SponsorId))
                .GroupBy(x => x.SponsorId!)
                .Select(g => new
                {
                    SponsorId = g.Key,
                    Total = g.Count(),
                    Active = g.Count(c => c.HasMetReferralThreshold),
                    Passive = g.Count(c => !c.HasMetReferralThreshold)
                })
                .ToListAsync();

            var childrenLookup = childStatsList
                .ToDictionary(x => x.SponsorId, x => new { x.Total, x.Active, x.Passive });

            var roleDict = await userRoles
                .GroupBy(x => x.UserId)
                .Select(g => new { g.Key, Role = g.Select(x => x.RoleName).FirstOrDefault() ?? "Musteri" })
                .ToDictionaryAsync(k => k.Key, v => v.Role);

            var list = new List<AlisverisSitesiFinal.Models.ViewModels.DavetRaporSatir>();
            foreach (var u in users)
            {
                var rol = roleDict.TryGetValue(u.Id, out var r) ? r : "Musteri";

                int etkinLimit;
                string kaynak;
                if (u.OzelReferansLimiti.HasValue && u.OzelReferansLimiti.Value > 0)
                {
                    etkinLimit = u.OzelReferansLimiti.Value;
                    kaynak = "Özel";
                }
                else if (rol == "Admin")
                {
                    etkinLimit = (cfg.AdminReferralLimit > 0 ? cfg.AdminReferralLimit : cfg.ReferralLimit);
                    kaynak = "Admin";
                }
                else
                {
                    etkinLimit = cfg.ReferralLimit;
                    kaynak = "Genel";
                }

                childrenLookup.TryGetValue(u.Id, out var stats);

                list.Add(new AlisverisSitesiFinal.Models.ViewModels.DavetRaporSatir
                {
                    UserId = u.Id,
                    Email = u.Email ?? "",
                    AdSoyad = string.Join(" ", new[] { u.Ad, u.Soyad }.Where(s => !string.IsNullOrWhiteSpace(s))),
                    Rol = rol,
                    EtkinLimit = etkinLimit,
                    LimitKaynak = kaynak,
                    ToplamCocuk = stats?.Total ?? 0,
                    AktifCocuk = stats?.Active ?? 0,
                    PasifCocuk = stats?.Passive ?? 0
                });
            }

            return View(list.OrderByDescending(x => x.ToplamCocuk).ThenBy(x => x.Email).ToList());
        }
    }
}
