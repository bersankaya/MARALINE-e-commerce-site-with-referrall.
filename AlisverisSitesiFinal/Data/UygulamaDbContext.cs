using System.Linq;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AlisverisSitesiFinal.Models;

namespace AlisverisSitesiFinal.Data
{
    public class UygulamaDbContext : IdentityDbContext<Kullanici>
    {
        public UygulamaDbContext(DbContextOptions<UygulamaDbContext> options) : base(options) { }

        public DbSet<Kullanici> Kullanicis { get; set; }
        public DbSet<Urun> Uruns { get; set; }
        public DbSet<Siparis> Siparisler { get; set; }
        public DbSet<SiparisKalemi> SiparisKalemleri { get; set; }
        public DbSet<BonusLog> BonusLoglari { get; set; }
        public DbSet<SepetKalemi> Sepet { get; set; }
        public DbSet<Kategori> Kategoriler { get; set; }
        public DbSet<Yorum> Yorumlar { get; set; }
        public DbSet<ParaCekmeTalebi> ParaCekmeTalepleri { get; set; }
        public DbSet<MagazaBasvurusu> MagazaBasvurulari { get; set; }
        public DbSet<Magaza> Magazalar { get; set; }
        public DbSet<Adres> Adresler => Set<Adres>();
        public DbSet<SaticiOdeme> SaticiOdemeler { get; set; } = default!;
        public DbSet<SaticiOdemeKalemi> SaticiOdemeKalemleri { get; set; } = default!;
        public DbSet<IadeTalebi> IadeTalepleri { get; set; } = default!;
        public DbSet<OdemeBeklet> OdemeBekletler { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Güvenlik: görünmeden kalan tüm cascade'leri Restrict'e çevir
            foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
                if (!fk.IsOwnership && fk.DeleteBehavior == DeleteBehavior.Cascade)
                    fk.DeleteBehavior = DeleteBehavior.Restrict;

            modelBuilder.Entity<IadeTalebi>()
               .HasOne(it => it.Siparis)
               .WithMany()             // Siparis tarafında koleksiyon tutmuyoruz; basit kalsın
               .HasForeignKey(it => it.SiparisId)
               .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<IadeTalebi>()
             .Property(t => t.IadeTutar)
             .HasColumnType("decimal(18,2)");
            // *** DİKKAT: Adres↔Kullanici ilişkisinde DataAnnotation ([ForeignKey(nameof(KullaniciId))])
            // zaten var. Burada FLUENT tanımlamıyoruz ki gölge FK (KullaniciId1) oluşmasın. ***
            modelBuilder.Entity<SaticiOdeme>(e =>
            {
                e.Property(x => x.Durum).HasMaxLength(20);
                e.HasMany(x => x.Kalemler)
                 .WithOne(k => k.SaticiOdeme)
                 .HasForeignKey(k => k.SaticiOdemeId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Kullanici>()
                .HasIndex(u => u.TcKimlikNo)
                .HasDatabaseName("IX_AspNetUsers_TcKimlikNo")
                .IsUnique()
                .HasFilter("[TcKimlikNo] IS NOT NULL AND [TcKimlikNo] <> N''");

            modelBuilder.Entity<Magaza>()
            .HasIndex(x => x.VergiNo).IsUnique().HasFilter("[VergiNo] IS NOT NULL");

            modelBuilder.Entity<Magaza>()
                .HasIndex(x => x.IBAN).IsUnique().HasFilter("[IBAN] IS NOT NULL");


            // Siparis ↔ Adres (nullable)
            modelBuilder.Entity<Siparis>()
                .HasOne(s => s.Adres)
                .WithMany()
                .HasForeignKey(s => s.AdresId)
                .OnDelete(DeleteBehavior.Restrict);

            // Siparis ↔ Kullanici (nav yok; FK: IdentityUserId → AspNetUsers.Id)
            modelBuilder.Entity<Siparis>()
                .HasOne<Kullanici>()
                .WithMany()
                .HasForeignKey(s => s.IdentityUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Siparis ↔ Urun (tek ürün alanı)
            modelBuilder.Entity<Siparis>()
                .HasOne(s => s.Urun)
                .WithMany()
                .HasForeignKey(s => s.UrunId)
                .OnDelete(DeleteBehavior.Restrict);

            // SiparisKalemi ↔ Siparis (Siparis.SiparisKalemleri var)
            modelBuilder.Entity<SiparisKalemi>()
                .HasOne(sk => sk.Siparis)
                .WithMany(s => s.SiparisKalemleri)
                .HasForeignKey(sk => sk.SiparisId)
                .OnDelete(DeleteBehavior.Restrict);

            // OdemeBeklet decimal precision
            modelBuilder.Entity<OdemeBeklet>()
                .Property(x => x.ToplamTutar)
                .HasColumnType("decimal(18,2)");

            // SiparisKalemi ↔ Urun
            modelBuilder.Entity<SiparisKalemi>()
                .HasOne(sk => sk.Urun)
                .WithMany()
                .HasForeignKey(sk => sk.UrunId)
                .OnDelete(DeleteBehavior.Restrict);

            // Urun ↔ Kategori (nullable)
            modelBuilder.Entity<Urun>()
                .HasOne(u => u.Kategori)
                .WithMany()
                .HasForeignKey(u => u.KategoriId)
                .OnDelete(DeleteBehavior.Restrict);

            // Urun ↔ Kullanici (sahibi) | FK: UserId (string)
            modelBuilder.Entity<Urun>()
                .HasOne(u => u.Kullanici)
                .WithMany()
                .HasForeignKey(u => u.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Urun ↔ Magaza (nullable)
            modelBuilder.Entity<Urun>()
                .HasOne(u => u.Store)
                .WithMany()
                .HasForeignKey(u => u.StoreId)
                .OnDelete(DeleteBehavior.SetNull);

            // DECIMAL precision
            modelBuilder.Entity<Urun>(b =>
            {
                b.Property(x => x.Fiyat).HasPrecision(18, 2);
                b.Property(x => x.SaticiTeklifFiyati).HasPrecision(18, 2);
                b.Property(x => x.FiyatAdmin).HasPrecision(18, 2);
                b.Property(x => x.FiyatReferansli).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Siparis>(b =>
            {
                b.Property(x => x.ToplamTutar).HasPrecision(18, 2);
            });

            modelBuilder.Entity<SiparisKalemi>(b =>
            {
                b.Property(x => x.BirimFiyat).HasPrecision(18, 2);
                b.Property(x => x.SaticiTeklifAnlik).HasPrecision(18, 2);
                b.Property(x => x.AdminFiyatAnlik).HasPrecision(18, 2);
                b.Property(x => x.RefFiyatAnlik).HasPrecision(18, 2);
            });

            modelBuilder.Entity<BonusLog>(b =>
            {
                b.Property(x => x.Tutar).HasPrecision(18, 2);
            });

            modelBuilder.Entity<ParaCekmeTalebi>(b =>
            {
                b.Property(x => x.Tutar).HasPrecision(18, 2);
            });

            modelBuilder.Entity<Kullanici>(b =>
            {
                b.Property(e => e.AylikKazanilanPara).HasColumnType("decimal(18,2)");
                b.Property(e => e.ToplamHarcama).HasColumnType("decimal(18,2)");
                b.Property(e => e.ToplamKazanilanPara).HasColumnType("decimal(18,2)");
                b.Property(e => e.ToplamKazanc).HasColumnType("decimal(18,2)");

                b.HasIndex(u => u.PhoneNumber)
                 .IsUnique()
                 .HasFilter("[PhoneNumber] IS NOT NULL");
            });

            modelBuilder.Entity<MagazaBasvurusu>().ToTable("MagazaBasvurulari");
        }
    }
}
