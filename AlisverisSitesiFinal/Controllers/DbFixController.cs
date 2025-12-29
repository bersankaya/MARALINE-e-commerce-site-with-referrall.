//using System.Text;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using AlisverisSitesiFinal.Data;

//namespace AlisverisSitesiFinal.Controllers
//{
//    [Authorize(Roles = "Admin")] // Admin girişi şart
//    public class DbFixController : Controller
//    {
//        private readonly UygulamaDbContext _db;
//        public DbFixController(UygulamaDbContext db) => _db = db;

//        // (A) Hangi DB’ye bağlıyım? + son migration’lar
//        [HttpGet("/__db/info")]
//        public async Task<IActionResult> Info()
//        {
//            await _db.Database.OpenConnectionAsync();
//            var conn = _db.Database.GetDbConnection();

//            string dbName, serverName;
//            await using (var cmd = conn.CreateCommand())
//            {
//                cmd.CommandText = "SELECT DB_NAME();";
//                var obj = await cmd.ExecuteScalarAsync();
//                dbName = obj?.ToString() ?? "(unknown)";
//            }

//            await using (var cmd = conn.CreateCommand())
//            {
//                cmd.CommandText = "SELECT @@SERVERNAME;";
//                var obj = await cmd.ExecuteScalarAsync();
//                serverName = obj?.ToString() ?? "(unknown)";
//            }

//            // Son 10 migration
//            var sb = new StringBuilder();
//            sb.AppendLine($"Server : {serverName}");
//            sb.AppendLine($"DataSrc: {conn.DataSource}");
//            sb.AppendLine($"Database: {dbName}");
//            sb.AppendLine("---- Last migrations ----");
//            var list = await _db.Database
//                .SqlQueryRaw<string>("SELECT TOP 10 [MigrationId] FROM [__EFMigrationsHistory] ORDER BY [MigrationId] DESC")
//                .ToListAsync();
//            foreach (var m in list) sb.AppendLine(m);

//            return Content(sb.ToString(), "text/plain", Encoding.UTF8);
//        }

//        // (B) Hot-fix: eksik kolonları varsa ekle (idempotent)
//        [HttpGet("/__db/hotfix")]
//        public async Task<IActionResult> Hotfix()
//        {
//            await _db.Database.OpenConnectionAsync();
//            var conn = _db.Database.GetDbConnection();

//            // --- Yardımcılar -------------------------------------------------
//            async Task<string?> FirstExisting(params string[] names)
//            {
//                foreach (var n in names)
//                {
//                    await using var c = conn.CreateCommand();
//                    c.CommandText = "SELECT 1 FROM sys.tables WHERE name=@n AND schema_id = SCHEMA_ID('dbo')";
//                    var p = c.CreateParameter(); p.ParameterName = "@n"; p.Value = n; c.Parameters.Add(p);
//                    var r = await c.ExecuteScalarAsync();
//                    if (r != null && r != DBNull.Value) return $"dbo.{n}";
//                }
//                return null;
//            }

//            async Task<string?> FindTableByCols(params string[] cols)
//            {
//                // Verdiğin sütunların en az 3'üne sahip tabloyu bulur (şema + ad)
//                var prm = string.Join(",", cols.Select((c, i) => $"@p{i}"));
//                var sql = $@"
//SELECT TOP 1 s.name + '.' + t.name
//FROM sys.tables t
//JOIN sys.schemas s ON s.schema_id = t.schema_id
//JOIN sys.columns c ON c.object_id = t.object_id
//WHERE c.name IN ({prm})
//GROUP BY s.name, t.name
//HAVING COUNT(DISTINCT c.name) >= @min
//ORDER BY MAX(t.create_date) DESC";

//                await using var cmd = conn.CreateCommand();
//                cmd.CommandText = sql;
//                for (int i = 0; i < cols.Length; i++)
//                {
//                    var p = cmd.CreateParameter(); p.ParameterName = $"@p{i}"; p.Value = cols[i];
//                    cmd.Parameters.Add(p);
//                }
//                var pmin = cmd.CreateParameter(); pmin.ParameterName = "@min"; pmin.Value = Math.Min(3, cols.Length);
//                cmd.Parameters.Add(pmin);

//                var r = await cmd.ExecuteScalarAsync();
//                return r == null || r == DBNull.Value ? null : (string)r;
//            }

//            var sb = new StringBuilder();

//            async Task Exec(string sql)
//            {
//                try { await _db.Database.ExecuteSqlRawAsync(sql); sb.AppendLine("OK   - " + sql); }
//                catch (Exception ex) { sb.AppendLine("HATA - " + ex.Message + "\nSQL  - " + sql); }
//                sb.AppendLine(new string('-', 60));
//            }
//            // ----------------------------------------------------------------

//            // Tablo adlarını bul (önce isim, olmazsa kolon imzası ile)
//            var sipTbl = await FirstExisting("Siparisler", "Siparis")
//                        ?? await FindTableByCols("ToplamTutar", "SiparisTarihi", "Durum", "IdentityUserId");

//            var kalemTbl = await FirstExisting("SiparisKalemleri", "SiparisKalemi")
//                        ?? await FindTableByCols("BirimFiyat", "Miktar", "UrunId", "SiparisId");

//            var iadeTbl = await FirstExisting("IadeTalepleri", "IadeTalebi"); // bu zaten bulunuyordu

//            sb.AppendLine($"Bulunan tablolar -> Siparis: {sipTbl ?? "-"} | Kalem: {kalemTbl ?? "-"} | Iade: {iadeTbl ?? "-"}");
//            sb.AppendLine(new string('=', 60));

//            // === SIPARIS ===
//            if (sipTbl != null)
//            {
//                await Exec($"IF COL_LENGTH('{sipTbl}','ToplamHizmetBedeli') IS NULL ALTER TABLE {sipTbl} ADD ToplamHizmetBedeli decimal(18,2) NOT NULL DEFAULT(0)");
//                await Exec($"IF COL_LENGTH('{sipTbl}','ToplamSirketKari')   IS NULL ALTER TABLE {sipTbl} ADD ToplamSirketKari   decimal(18,2) NOT NULL DEFAULT(0)");
//                await Exec($"IF COL_LENGTH('{sipTbl}','FaturaDurumu')      IS NULL ALTER TABLE {sipTbl} ADD FaturaDurumu nvarchar(32) NULL");
//                await Exec($"IF COL_LENGTH('{sipTbl}','EFaturaNo')         IS NULL ALTER TABLE {sipTbl} ADD EFaturaNo nvarchar(64) NULL");
//                await Exec($"IF COL_LENGTH('{sipTbl}','EFaturaTarihi')     IS NULL ALTER TABLE {sipTbl} ADD EFaturaTarihi datetime2 NULL");
//                await Exec($"IF COL_LENGTH('{sipTbl}','IyzicoFee')         IS NULL ALTER TABLE {sipTbl} ADD IyzicoFee decimal(18,2) NOT NULL DEFAULT(0)");
//                await Exec($"IF COL_LENGTH('{sipTbl}','IyzicoPaymentId')   IS NULL ALTER TABLE {sipTbl} ADD IyzicoPaymentId nvarchar(128) NULL");
//                await Exec($"IF COL_LENGTH('{sipTbl}','NetTahsilat')       IS NULL ALTER TABLE {sipTbl} ADD NetTahsilat decimal(18,2) NOT NULL DEFAULT(0)");
//            }
//            else sb.AppendLine("UYARI: Sipariş tablosu bulunamadı (isim/şema farklı olabilir).");

//            // === SIPARIS KALEMLERI ===
//            if (kalemTbl != null)
//            {
//                await Exec($"IF COL_LENGTH('{kalemTbl}','HizmetBedeli') IS NULL ALTER TABLE {kalemTbl} ADD HizmetBedeli decimal(18,2) NOT NULL DEFAULT(0)");
//                await Exec($"IF COL_LENGTH('{kalemTbl}','SirketKari')   IS NULL ALTER TABLE {kalemTbl} ADD SirketKari   decimal(18,2) NOT NULL DEFAULT(0)");
//                await Exec($"IF COL_LENGTH('{kalemTbl}','KdvOrani')     IS NULL ALTER TABLE {kalemTbl} ADD KdvOrani decimal(5,2) NULL");
//                await Exec($"IF COL_LENGTH('{kalemTbl}','KdvTutari')    IS NULL ALTER TABLE {kalemTbl} ADD KdvTutari decimal(18,2) NULL");
//            }
//            else sb.AppendLine("UYARI: Sipariş kalem tablosu bulunamadı (isim/şema farklı olabilir).");

//            // === IADE TALEPLERI ===
//            if (iadeTbl != null)
//            {
//                await Exec($"IF COL_LENGTH('{iadeTbl}','IadeAdres')           IS NULL ALTER TABLE {iadeTbl} ADD IadeAdres nvarchar(256) NULL");
//                await Exec($"IF COL_LENGTH('{iadeTbl}','IadeTalimat')         IS NULL ALTER TABLE {iadeTbl} ADD IadeTalimat nvarchar(512) NULL");
//                await Exec($"IF COL_LENGTH('{iadeTbl}','RmaKodu')             IS NULL ALTER TABLE {iadeTbl} ADD RmaKodu nvarchar(64) NULL");
//                await Exec($"IF COL_LENGTH('{iadeTbl}','RmaOlusturmaTarihi')  IS NULL ALTER TABLE {iadeTbl} ADD RmaOlusturmaTarihi datetime2 NULL");
//                await Exec($"IF COL_LENGTH('{iadeTbl}','MusteriKargoFirmasi') IS NULL ALTER TABLE {iadeTbl} ADD MusteriKargoFirmasi nvarchar(64) NULL");
//                await Exec($"IF COL_LENGTH('{iadeTbl}','MusteriKargoTakipNo') IS NULL ALTER TABLE {iadeTbl} ADD MusteriKargoTakipNo nvarchar(64) NULL");
//                await Exec($"IF COL_LENGTH('{iadeTbl}','IadeYontemi')         IS NULL ALTER TABLE {iadeTbl} ADD IadeYontemi nvarchar(200) NULL");
//                await Exec($"IF COL_LENGTH('{iadeTbl}','IadeTutar')           IS NULL ALTER TABLE {iadeTbl} ADD IadeTutar decimal(18,2) NULL");
//                await Exec($"IF COL_LENGTH('{iadeTbl}','IadeYapanUserId')     IS NULL ALTER TABLE {iadeTbl} ADD IadeYapanUserId nvarchar(450) NULL");
//                await Exec($"IF COL_LENGTH('{iadeTbl}','IadeOdemeTarihi')     IS NULL ALTER TABLE {iadeTbl} ADD IadeOdemeTarihi datetime2 NULL");
//            }
//            else sb.AppendLine("UYARI: İade tablosu bulunamadı (isim/şema farklı olabilir).");

//            return Content(sb.ToString(), "text/plain; charset=utf-8");
//        }
//        [Authorize(Roles = "Admin")]
//        [HttpGet("/__db/ensure-odeme-beklet")]
//        public async Task<IActionResult> EnsureOdemeBeklet()
//        {
//            var sql = @"
//IF OBJECT_ID(N'dbo.OdemeBekletler', N'U') IS NULL
//BEGIN
//    CREATE TABLE dbo.OdemeBekletler
//    (
//        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OdemeBekletler PRIMARY KEY,
//        MerchantOid   NVARCHAR(64)   NOT NULL,
//        KullaniciId   NVARCHAR(64)   NULL,
//        Email         NVARCHAR(256)  NULL,
//        UserName      NVARCHAR(128)  NULL,
//        UserPhone     NVARCHAR(32)   NULL,
//        UserAddress   NVARCHAR(1024) NULL,
//        ToplamTutar   DECIMAL(18,2)  NOT NULL,
//        SepetJson     NVARCHAR(MAX)  NOT NULL,
//        Olusturma     DATETIME2      NOT NULL
//    );

//    CREATE UNIQUE INDEX IX_OdemeBekletler_MerchantOid
//        ON dbo.OdemeBekletler(MerchantOid);
//END";
//            try
//            {
//                await _db.Database.ExecuteSqlRawAsync(sql);
//                return Content("OK: OdemeBekletler ensured/created.", "text/plain");
//            }
//            catch (Exception ex)
//            {
//                return Content("HATA: " + ex, "text/plain");
//            }
//        }



//    }
//}
