using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AlisverisSitesiFinal.Data.Migrations
{
    public partial class AddReturnInvoiceCols : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- SİPARİŞLER
IF COL_LENGTH('dbo.Siparisler','ToplamHizmetBedeli') IS NULL ALTER TABLE dbo.Siparisler ADD ToplamHizmetBedeli decimal(18,2) NOT NULL CONSTRAINT DF_Siparisler_ToplamHizmetBedeli DEFAULT(0);
IF COL_LENGTH('dbo.Siparisler','ToplamSirketKari')   IS NULL ALTER TABLE dbo.Siparisler ADD ToplamSirketKari   decimal(18,2) NOT NULL CONSTRAINT DF_Siparisler_ToplamSirketKari DEFAULT(0);
IF COL_LENGTH('dbo.Siparisler','FaturaDurumu')       IS NULL ALTER TABLE dbo.Siparisler ADD FaturaDurumu nvarchar(32) NULL;
IF COL_LENGTH('dbo.Siparisler','EFaturaNo')          IS NULL ALTER TABLE dbo.Siparisler ADD EFaturaNo nvarchar(64) NULL;
IF COL_LENGTH('dbo.Siparisler','EFaturaTarihi')      IS NULL ALTER TABLE dbo.Siparisler ADD EFaturaTarihi datetime2 NULL;
IF COL_LENGTH('dbo.Siparisler','IyzicoFee')          IS NULL ALTER TABLE dbo.Siparisler ADD IyzicoFee decimal(18,2) NOT NULL CONSTRAINT DF_Siparisler_IyzicoFee DEFAULT(0);
IF COL_LENGTH('dbo.Siparisler','IyzicoPaymentId')    IS NULL ALTER TABLE dbo.Siparisler ADD IyzicoPaymentId nvarchar(128) NULL;
IF COL_LENGTH('dbo.Siparisler','NetTahsilat')        IS NULL ALTER TABLE dbo.Siparisler ADD NetTahsilat decimal(18,2) NOT NULL CONSTRAINT DF_Siparisler_NetTahsilat DEFAULT(0);

-- SİPARİŞ KALEMLERİ
IF COL_LENGTH('dbo.SiparisKalemleri','HizmetBedeli') IS NULL ALTER TABLE dbo.SiparisKalemleri ADD HizmetBedeli decimal(18,2) NOT NULL CONSTRAINT DF_SiparisKalemleri_HizmetBedeli DEFAULT(0);
IF COL_LENGTH('dbo.SiparisKalemleri','SirketKari')   IS NULL ALTER TABLE dbo.SiparisKalemleri ADD SirketKari   decimal(18,2) NOT NULL CONSTRAINT DF_SiparisKalemleri_SirketKari DEFAULT(0);
IF COL_LENGTH('dbo.SiparisKalemleri','KdvOrani')     IS NULL ALTER TABLE dbo.SiparisKalemleri ADD KdvOrani decimal(5,2) NOT NULL CONSTRAINT DF_SiparisKalemleri_KdvOrani DEFAULT(0);
IF COL_LENGTH('dbo.SiparisKalemleri','KdvTutari')    IS NULL ALTER TABLE dbo.SiparisKalemleri ADD KdvTutari decimal(18,2) NOT NULL CONSTRAINT DF_SiparisKalemleri_KdvTutari DEFAULT(0);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Siparisler','ToplamHizmetBedeli') IS NOT NULL ALTER TABLE dbo.Siparisler DROP CONSTRAINT DF_Siparisler_ToplamHizmetBedeli;
IF COL_LENGTH('dbo.Siparisler','ToplamSirketKari')   IS NOT NULL ALTER TABLE dbo.Siparisler DROP CONSTRAINT DF_Siparisler_ToplamSirketKari;
IF COL_LENGTH('dbo.Siparisler','IyzicoFee')          IS NOT NULL ALTER TABLE dbo.Siparisler DROP CONSTRAINT DF_Siparisler_IyzicoFee;
IF COL_LENGTH('dbo.Siparisler','NetTahsilat')        IS NOT NULL ALTER TABLE dbo.Siparisler DROP CONSTRAINT DF_Siparisler_NetTahsilat;

IF COL_LENGTH('dbo.Siparisler','ToplamHizmetBedeli') IS NOT NULL ALTER TABLE dbo.Siparisler DROP COLUMN ToplamHizmetBedeli;
IF COL_LENGTH('dbo.Siparisler','ToplamSirketKari')   IS NOT NULL ALTER TABLE dbo.Siparisler DROP COLUMN ToplamSirketKari;
IF COL_LENGTH('dbo.Siparisler','FaturaDurumu')       IS NOT NULL ALTER TABLE dbo.Siparisler DROP COLUMN FaturaDurumu;
IF COL_LENGTH('dbo.Siparisler','EFaturaNo')          IS NOT NULL ALTER TABLE dbo.Siparisler DROP COLUMN EFaturaNo;
IF COL_LENGTH('dbo.Siparisler','EFaturaTarihi')      IS NOT NULL ALTER TABLE dbo.Siparisler DROP COLUMN EFaturaTarihi;
IF COL_LENGTH('dbo.Siparisler','IyzicoFee')          IS NOT NULL ALTER TABLE dbo.Siparisler DROP COLUMN IyzicoFee;
IF COL_LENGTH('dbo.Siparisler','IyzicoPaymentId')    IS NOT NULL ALTER TABLE dbo.Siparisler DROP COLUMN IyzicoPaymentId;
IF COL_LENGTH('dbo.Siparisler','NetTahsilat')        IS NOT NULL ALTER TABLE dbo.Siparisler DROP COLUMN NetTahsilat;

IF COL_LENGTH('dbo.SiparisKalemleri','HizmetBedeli') IS NOT NULL ALTER TABLE dbo.SiparisKalemleri DROP COLUMN HizmetBedeli;
IF COL_LENGTH('dbo.SiparisKalemleri','SirketKari')   IS NOT NULL ALTER TABLE dbo.SiparisKalemleri DROP COLUMN SirketKari;
IF COL_LENGTH('dbo.SiparisKalemleri','KdvOrani')     IS NOT NULL ALTER TABLE dbo.SiparisKalemleri DROP COLUMN KdvOrani;
IF COL_LENGTH('dbo.SiparisKalemleri','KdvTutari')    IS NOT NULL ALTER TABLE dbo.SiparisKalemleri DROP COLUMN KdvTutari;
");
        }
    }
}
