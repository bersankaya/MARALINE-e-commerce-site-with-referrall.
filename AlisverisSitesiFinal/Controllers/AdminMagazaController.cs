using System.Data;
using System.Text;
using AlisverisSitesiFinal.Data;
using AlisverisSitesiFinal.Models;
using AlisverisSitesiFinal.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace AlisverisSitesiFinal.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminMagazaController : Controller
    {
        private readonly UygulamaDbContext _ctx;
        public AdminMagazaController(UygulamaDbContext ctx) { _ctx = ctx; }

        // ortak sorgu (liste/eksport aynı kaynaktan beslensin)
        private IQueryable<AdminUrunListeVM> BuildQuery(string? saticiId, string? q, bool? yayinda)
        {
            var query = _ctx.Uruns
                .Include(u => u.Kategori)
                .Include(u => u.Kullanici) // Urun.Kullanici navigation varsa
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(saticiId))
                query = query.Where(x => x.UserId == saticiId);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                query = query.Where(x =>
                    EF.Functions.Like(x.Ad, $"%{k}%") ||
                    (x.Kategori != null && EF.Functions.Like(x.Kategori.Ad, $"%{k}%")) ||
                    (x.Kullanici != null &&
                        (EF.Functions.Like((x.Kullanici.Ad + " " + x.Kullanici.Soyad), $"%{k}%")
                         || EF.Functions.Like(x.Kullanici.Email, $"%{k}%"))));
            }

            if (yayinda.HasValue)
                query = query.Where(x => x.YayindaMi == yayinda.Value);

            // null güvenli projeksiyon (CS8601/8602 uyarılarını önler)
            return query.OrderByDescending(x => x.EklenmeTarihi)
                        .Select(x => new AdminUrunListeVM
                        {
                            UrunId = x.Id,
                            UrunAdi = x.Ad ?? string.Empty,
                            Kategori = x.Kategori != null ? x.Kategori.Ad : null,
                            Stok = x.StokAdedi,
                            EklenmeTarihi = x.EklenmeTarihi,
                            YayindaMi = x.YayindaMi,

                            SaticiId = x.UserId ?? string.Empty,
                            SaticiAdSoyad = x.Kullanici != null
                                ? ((x.Kullanici.Ad ?? string.Empty) + " " + (x.Kullanici.Soyad ?? string.Empty)).Trim()
                                : string.Empty,
                            SaticiEmail = x.Kullanici != null ? (x.Kullanici.Email ?? string.Empty) : string.Empty,

                            SaticiTeklifFiyati = x.SaticiTeklifFiyati,
                            FiyatAdmin = x.FiyatAdmin,
                            FiyatReferansli = x.FiyatReferansli
                        });
        }

        // LISTE
        public async Task<IActionResult> Urunler(string? saticiId, string? q, bool? yayinda)
        {
            var list = await BuildQuery(saticiId, q, yayinda).ToListAsync();

            // filtre dropdown için satıcı listesi
            var saticilar = await _ctx.Users
                .OrderBy(u => u.Ad).Select(u => new
                {
                    u.Id,
                    AdSoyad = ((u.Ad ?? "") + " " + (u.Soyad ?? "")).Trim(),
                    Email = u.Email ?? ""
                }).ToListAsync();

            ViewBag.Saticilar = saticilar;
            ViewBag.SaticiId = saticiId ?? "";
            ViewBag.Q = q ?? "";
            ViewBag.Yayinda = yayinda;

            return View(list);
        }

        // CSV EXPORT
        [HttpGet]
        public async Task<FileResult> ExportCsv(string? saticiId, string? q, bool? yayinda)
        {
            var rows = await BuildQuery(saticiId, q, yayinda).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("UrunId;UrunAdi;Kategori;Stok;Yayinda;SaticiAdSoyad;SaticiEmail;SaticiTeklif;FiyatAdmin;FiyatReferansli;EklenmeTarihi");

            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(';', new[]
                {
                    r.UrunId.ToString(),
                    r.UrunAdi.Replace(';', ','),
                    (r.Kategori ?? "").Replace(';', ','),
                    r.Stok.ToString(),
                    r.YayindaMi ? "Evet" : "Hayir",
                    r.SaticiAdSoyad.Replace(';', ','),
                    r.SaticiEmail,
                    (r.SaticiTeklifFiyati ?? 0m).ToString("0.##"),
                    (r.FiyatAdmin ?? 0m).ToString("0.##"),
                    (r.FiyatReferansli ?? 0m).ToString("0.##"),
                    r.EklenmeTarihi.ToString("yyyy-MM-dd HH:mm")
                }));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "magaza-urunleri.csv");
        }

        // EXCEL EXPORT (ClosedXML ile)
        // NuGet: Install-Package ClosedXML
        [HttpGet]
        public async Task<IActionResult> ExportExcel(string? saticiId, string? q, bool? yayinda)
        {
            var rows = await BuildQuery(saticiId, q, yayinda).ToListAsync();

            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.AddWorksheet("Urunler");
            // başlıklar
            var headers = new[]
            {
                "UrunId","UrunAdi","Kategori","Stok","Yayinda","Satıcı","Email",
                "Satıcı Teklif","Admin Fiyat","Ref. Fiyat","Eklenme"
            };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            int row = 2;
            foreach (var r in rows)
            {
                ws.Cell(row, 1).Value = r.UrunId;
                ws.Cell(row, 2).Value = r.UrunAdi;
                ws.Cell(row, 3).Value = r.Kategori ?? "";
                ws.Cell(row, 4).Value = r.Stok;
                ws.Cell(row, 5).Value = r.YayindaMi ? "Evet" : "Hayır";
                ws.Cell(row, 6).Value = r.SaticiAdSoyad;
                ws.Cell(row, 7).Value = r.SaticiEmail;
                ws.Cell(row, 8).Value = r.SaticiTeklifFiyati;
                ws.Cell(row, 9).Value = r.FiyatAdmin;
                ws.Cell(row, 10).Value = r.FiyatReferansli;
                ws.Cell(row, 11).Value = r.EklenmeTarihi;
                row++;
            }
            ws.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Position = 0;
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "magaza-urunleri.xlsx");
        }
    }
}
