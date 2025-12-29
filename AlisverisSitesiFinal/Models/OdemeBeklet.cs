// Models/OdemeBeklet.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class OdemeBeklet
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string MerchantOid { get; set; } = default!;

    [Required, MaxLength(64)]
    public string KullaniciId { get; set; } = default!;

    [Required, MaxLength(256)]
    public string Email { get; set; } = default!;

    [MaxLength(128)]
    public string UserName { get; set; } = "";

    [MaxLength(32)]
    public string UserPhone { get; set; } = "";

    [MaxLength(1024)]
    public string UserAddress { get; set; } = "";

    // Kuruş değil TL cinsinden saklıyoruz
    [Range(0, double.MaxValue)]
    [Column(TypeName = "decimal(18,2)")]
    public decimal ToplamTutar { get; set; }

    // Sepet kalemlerinin snapshottan oluşturulması için JSON
    // [{urunId:1, miktar:2, birim:5200.00, renk:"", beden:"", ...}]
    [Required]
    public string SepetJson { get; set; } = default!;

    public DateTime Olusturma { get; set; } = DateTime.Now;
}
