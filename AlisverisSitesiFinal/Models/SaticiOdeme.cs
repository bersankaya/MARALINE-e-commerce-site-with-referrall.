using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AlisverisSitesiFinal.Models
{
    public class SaticiOdeme
    {
        public int Id { get; set; }

        [Required]
        public string SaticiId { get; set; } = default!; // AspNetUsers.Id

        [Required]
        public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;

        public DateTime? OdemeTarihi { get; set; }

        [Required, MaxLength(20)]
        public string Durum { get; set; } = "Beklemede"; // Beklemede | Odendi

        [MaxLength(300)]
        public string? Aciklama { get; set; }

        // Toplamlar
        [Precision(18, 2)]
        public decimal BrutToplam { get; set; }        // Müşteriye satış toplamı (rapor)

        [Precision(18, 2)]
        public decimal KomisyonToplam { get; set; }    // Platform geliri = BrutToplam - NetToplam

        [Precision(18, 2)]
        public decimal NetToplam { get; set; }         // Satıcıya ödenecek toplam

        public List<SaticiOdemeKalemi> Kalemler { get; set; } = new();
    }
}
