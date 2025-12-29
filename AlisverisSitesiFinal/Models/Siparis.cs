using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlisverisSitesiFinal.Models
{
    public class Siparis
    {
        public int Id { get; set; }
        public string IdentityUserId { get; set; } = string.Empty;
        public DateTime SiparisTarihi { get; set; }
        public decimal ToplamTutar { get; set; }
        public string Durum { get; set; } = string.Empty;

        public int UrunId { get; set; }
        public Urun? Urun { get; set; }  // Navigation property

        public List<SiparisKalemi> SiparisKalemleri { get; set; } = new();
        public int? AdresId { get; set; }
        public Adres? Adres { get; set; }
        public string? ReddetmeNedeni { get; set; }

        // ⬇️ EKLENDİ: Siparişe özgü referans anahtarı (log eşlemesi için)
        public string? OrderRefKey { get; set; }   // "order-<Id>" formatında doldurulacak
        // ⬇️ EKLENDİ: Bu siparişin referral/bonus etkileri işlendi mi?
        public bool ReferralIslenmisMi { get; set; } = false;
        // ✅ Yeni alanlar (toplamlar)
        [Column(TypeName = "decimal(18,2)")]
        public decimal ToplamHizmetBedeli { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ToplamSirketKari { get; set; }

        // Ödeme/iyzico bilgi alanları (opsiyonel ama önerilir)
        public string? IyzicoPaymentId { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal IyzicoFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetTahsilat { get; set; }

        // Fatura takibi
        public string FaturaDurumu { get; set; } = "Bekliyor"; // Bekliyor/Kesildi/İptal
        public string? EFaturaNo { get; set; }
        public DateTime? EFaturaTarihi { get; set; }

    }
}
