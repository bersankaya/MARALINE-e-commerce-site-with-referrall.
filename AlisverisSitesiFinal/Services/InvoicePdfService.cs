using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using AlisverisSitesiFinal.Models;

namespace AlisverisSitesiFinal.Services
{
    /// <summary>
    /// Proforma / Bilgilendirme PDF üretimi.
    /// NOT: Bu belge resmî e-Arşiv/e-Fatura yerine geçmez.
    /// </summary>
    public class InvoicePdfService
    {
        /// <summary>
        /// Sipariş için proforma/bilgilendirme PDF üretir.
        /// </summary>
        /// <param name="s">Sipariş</param>
        /// <param name="musteriAdSoyad">Müşteri ad-soyad (opsiyonel)</param>
        /// <param name="musteriEmail">Müşteri e-posta (opsiyonel)</param>
        /// <param name="musteriTelefon">Müşteri telefon (opsiyonel)</param>
        /// <param name="musteriAdres">Müşteri adres (opsiyonel serbest metin)</param>
        public byte[] Generate(
            Siparis s,
            string? musteriAdSoyad = null,
            string? musteriEmail = null,
            string? musteriTelefon = null,
            string? musteriAdres = null)
        {
            var tr = new CultureInfo("tr-TR");

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(t => t.FontSize(10));

                    // ---------- ÜST BÖLÜM ----------
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("MARALINE").SemiBold().FontSize(16);
                            col.Item().Text("Bilgilendirme / Proforma");
                            col.Item().Text($"Sipariş No: #{s.Id}");
                            col.Item().Text($"Tarih: {s.SiparisTarihi:dd.MM.yyyy HH:mm}");
                        });

                        row.ConstantItem(260).Column(col =>
                        {
                            col.Item().Text("Müşteri Bilgileri").SemiBold();
                            col.Item().Text($"Ad Soyad: {(string.IsNullOrWhiteSpace(musteriAdSoyad) ? "-" : musteriAdSoyad)}");
                            col.Item().Text($"E-posta : {(string.IsNullOrWhiteSpace(musteriEmail) ? "-" : musteriEmail)}");
                            col.Item().Text($"Telefon : {(string.IsNullOrWhiteSpace(musteriTelefon) ? "-" : musteriTelefon)}");
                            col.Item().Text($"Adres   : {(string.IsNullOrWhiteSpace(musteriAdres) ? "-" : musteriAdres)}");
                        });
                    });

                    // ---------- İÇERİK ----------
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // Kalem tablosu
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(4); // Kalem
                                c.RelativeColumn(2); // Miktar
                                c.RelativeColumn(3); // Birim Fiyat
                                c.RelativeColumn(3); // Tutar
                            });

                            table.Header(h =>
                            {
                                h.Cell().Element(HeaderCell).Text("Kalem");
                                h.Cell().Element(HeaderCell).AlignCenter().Text("Miktar");
                                h.Cell().Element(HeaderCell).AlignRight().Text("Birim Fiyat");
                                h.Cell().Element(HeaderCell).AlignRight().Text("Tutar");

                                static IContainer HeaderCell(IContainer c) =>
                                    c.Background(Colors.Grey.Lighten3)
                                     .Padding(5)
                                     .DefaultTextStyle(t => t.SemiBold());
                            });

                            foreach (var k in (s.SiparisKalemleri ?? new List<SiparisKalemi>()))
                            {
                                var urunAdi = k.Urun?.Ad ?? "Ürün";
                                var birimFiyat = k.BirimFiyat;
                                var miktar = k.Miktar;
                                var tutar = birimFiyat * miktar;

                                // Ürün satırı
                                table.Cell().Padding(4).Text($"{urunAdi} (Ürün Bedeli)");
                                table.Cell().Padding(4).AlignCenter().Text(miktar.ToString(tr));
                                table.Cell().Padding(4).AlignRight().Text(birimFiyat.ToString("C", tr));
                                table.Cell().Padding(4).AlignRight().Text(tutar.ToString("C", tr));

                                // Platform Hizmet Bedeli (kaleme dağıtılmışsa)
                                if (k.HizmetBedeli > 0)
                                {
                                    table.Cell().Padding(4).Text("Platform Hizmet Bedeli");
                                    table.Cell().Padding(4).AlignCenter().Text("1");
                                    table.Cell().Padding(4).AlignRight().Text(k.HizmetBedeli.ToString("C", tr));
                                    table.Cell().Padding(4).AlignRight().Text(k.HizmetBedeli.ToString("C", tr));
                                }

                                // Maraline Kârı (kaleme dağıtılmışsa)
                                if (k.SirketKari > 0)
                                {
                                    table.Cell().Padding(4).Text("Maraline Kârı");
                                    table.Cell().Padding(4).AlignCenter().Text("1");
                                    table.Cell().Padding(4).AlignRight().Text(k.SirketKari.ToString("C", tr));
                                    table.Cell().Padding(4).AlignRight().Text(k.SirketKari.ToString("C", tr));
                                }
                            }
                        });

                        // Toplamlar
                        col.Item().PaddingTop(10).Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(300).Column(sum =>
                            {
                                decimal urunToplam = (s.SiparisKalemleri ?? new())
                                    .Sum(x => x.SaticiTeklifAnlik ?? (x.BirimFiyat * x.Miktar));

                                sum.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Toplam Ürün:").AlignRight();
                                    r.ConstantItem(140).Text(urunToplam.ToString("C", tr)).AlignRight();
                                });

                                sum.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Toplam Hizmet Bedeli:").AlignRight();
                                    r.ConstantItem(140).Text((s.ToplamHizmetBedeli).ToString("C", tr)).AlignRight();
                                });

                                sum.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Toplam Maraline Kârı:").AlignRight();
                                    r.ConstantItem(140).Text((s.ToplamSirketKari).ToString("C", tr)).AlignRight();
                                });

                                sum.Item().LineHorizontal(0.8f);

                                sum.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Genel Toplam:").SemiBold().AlignRight();
                                    r.ConstantItem(140).Text(s.ToplamTutar.ToString("C", tr)).SemiBold().AlignRight();
                                });
                            });
                        });

                        // Not
                        col.Item().PaddingTop(12).Text(t =>
                        {
                            t.Span("Not: ").SemiBold();
                            t.Span("Bu belge bilgilendirme / proforma amaçlıdır; resmî e-Arşiv / e-Fatura yerine geçmez.");
                        });
                    });

                    // ---------- ALT BÖLÜM ----------
                    page.Footer().AlignCenter().Text(txt =>
                    {
                        txt.Span("Maraline • ").SemiBold();
                        txt.Span("www.maraline.com • destek@maraline.com");
                    });
                });
            }).GeneratePdf();
        }
    }
}
