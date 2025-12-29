using System;

namespace AlisverisSitesiFinal.Models
{
    public class IadeTalebi
    {
        public int Id { get; set; }

        // Hangi sipariş için?
        public int SiparisId { get; set; }
        public Siparis? Siparis { get; set; }

        // Talebi açan (müşteri)
        public string KullaniciId { get; set; } = string.Empty;

        // Basit durumlar: Beklemede / Onaylandı / Reddedildi
        public string Durum { get; set; } = "Beklemede";

        public string? Aciklama { get; set; }
        public DateTime TalepTarihi { get; set; } = DateTime.Now;
        public DateTime? SonucTarihi { get; set; }
        public string? ReddetmeNedeni { get; set; }
        // --- ÖDEME (ADMIN) ---
        public decimal? IadeTutar { get; set; }            // Ödenen tutar
        public string? IadeYontemi { get; set; }           // Kart iadesi / Havale / cüzdan vs.
        public string? IadeYapanUserId { get; set; }       // Admin Id
        public DateTime? IadeOdemeTarihi { get; set; }     // Ödeme tarihi

        // --- RMA / İade lojistiği ---
        public string? RmaKodu { get; set; }           // örn: RMA-20250826-1234
        public string? IadeAdres { get; set; }         // “Mağaza Adı - Adres satırı ...”
        public string? IadeTalimat { get; set; }       // “Kutuyu saklayın, 7 gün içinde gönderin” gibi
        public string? MusteriKargoFirmasi { get; set; }
        public string? MusteriKargoTakipNo { get; set; }
        public DateTime? RmaOlusturmaTarihi { get; set; }
    }
}
