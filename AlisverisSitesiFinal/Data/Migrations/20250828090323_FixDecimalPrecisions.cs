using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AlisverisSitesiFinal.Data.Migrations
{
    public partial class FixDecimalPrecisions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ========== SİPARİŞLER ==========
IF COL_LENGTH('dbo.Siparisler','EFaturaNo') IS NULL
    ALTER TABLE dbo.Siparisler ADD EFaturaNo nvarchar(64) NULL;

IF COL_LENGTH('dbo.Siparisler','EFaturaTarihi') IS NULL
    ALTER TABLE dbo.Siparisler ADD EFaturaTarihi datetime2 NULL;

IF COL_LENGTH('dbo.Siparisler','FaturaDurumu') IS NULL
    ALTER TABLE dbo.Siparisler ADD FaturaDurumu nvarchar(32) NULL;

IF COL_LENGTH('dbo.Siparisler','IyzicoFee') IS NULL
    ALTER TABLE dbo.Siparisler ADD IyzicoFee decimal(18,2) NOT NULL CONSTRAINT DF_Siparisler_IyzicoFee DEFAULT(0);

IF COL_LENGTH('dbo.Siparisler','IyzicoPaymentId') IS NULL
    ALTER TABLE dbo.Siparisler ADD IyzicoPaymentId nvarchar(128) NULL;

IF COL_LENGTH('dbo.Siparisler','NetTahsilat') IS NULL
    ALTER TABLE dbo.Siparisler ADD NetTahsilat decimal(18,2) NOT NULL CONSTRAINT DF_Siparisler_NetTahsilat DEFAULT(0);

IF COL_LENGTH('dbo.Siparisler','ToplamHizmetBedeli') IS NULL
    ALTER TABLE dbo.Siparisler ADD ToplamHizmetBedeli decimal(18,2) NOT NULL CONSTRAINT DF_Siparisler_ToplamHizmetBedeli DEFAULT(0);

IF COL_LENGTH('dbo.Siparisler','ToplamSirketKari') IS NULL
    ALTER TABLE dbo.Siparisler ADD ToplamSirketKari decimal(18,2) NOT NULL CONSTRAINT DF_Siparisler_ToplamSirketKari DEFAULT(0);

-- ========== SİPARİŞ KALEMLERİ ==========
IF COL_LENGTH('dbo.SiparisKalemleri','HizmetBedeli') IS NULL
    ALTER TABLE dbo.SiparisKalemleri ADD HizmetBedeli decimal(18,2) NOT NULL CONSTRAINT DF_SiparisKalemleri_HizmetBedeli DEFAULT(0);

IF COL_LENGTH('dbo.SiparisKalemleri','KdvOrani') IS NULL
    ALTER TABLE dbo.SiparisKalemleri ADD KdvOrani decimal(5,2) NULL;

IF COL_LENGTH('dbo.SiparisKalemleri','KdvTutari') IS NULL
    ALTER TABLE dbo.SiparisKalemleri ADD KdvTutari decimal(18,2) NULL;

IF COL_LENGTH('dbo.SiparisKalemleri','SirketKari') IS NULL
    ALTER TABLE dbo.SiparisKalemleri ADD SirketKari decimal(18,2) NOT NULL CONSTRAINT DF_SiparisKalemleri_SirketKari DEFAULT(0);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.Siparisler','EFaturaNo') IS NOT NULL
    ALTER TABLE dbo.Siparisler DROP COLUMN EFaturaNo;

IF COL_LENGTH('dbo.Siparisler','EFaturaTarihi') IS NOT NULL
    ALTER TABLE dbo.Siparisler DROP COLUMN EFaturaTarihi;

IF COL_LENGTH('dbo.Siparisler','FaturaDurumu') IS NOT NULL
    ALTER TABLE dbo.Siparisler DROP COLUMN FaturaDurumu;

IF COL_LENGTH('dbo.Siparisler','IyzicoFee') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.Siparisler DROP CONSTRAINT DF_Siparisler_IyzicoFee;
        ALTER TABLE dbo.Siparisler DROP COLUMN IyzicoFee;
    END

IF COL_LENGTH('dbo.Siparisler','IyzicoPaymentId') IS NOT NULL
    ALTER TABLE dbo.Siparisler DROP COLUMN IyzicoPaymentId;

IF COL_LENGTH('dbo.Siparisler','NetTahsilat') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.Siparisler DROP CONSTRAINT DF_Siparisler_NetTahsilat;
        ALTER TABLE dbo.Siparisler DROP COLUMN NetTahsilat;
    END

IF COL_LENGTH('dbo.Siparisler','ToplamHizmetBedeli') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.Siparisler DROP CONSTRAINT DF_Siparisler_ToplamHizmetBedeli;
        ALTER TABLE dbo.Siparisler DROP COLUMN ToplamHizmetBedeli;
    END

IF COL_LENGTH('dbo.Siparisler','ToplamSirketKari') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.Siparisler DROP CONSTRAINT DF_Siparisler_ToplamSirketKari;
        ALTER TABLE dbo.Siparisler DROP COLUMN ToplamSirketKari;
    END

IF COL_LENGTH('dbo.SiparisKalemleri','HizmetBedeli') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.SiparisKalemleri DROP CONSTRAINT DF_SiparisKalemleri_HizmetBedeli;
        ALTER TABLE dbo.SiparisKalemleri DROP COLUMN HizmetBedeli;
    END

IF COL_LENGTH('dbo.SiparisKalemleri','KdvOrani') IS NOT NULL
    ALTER TABLE dbo.SiparisKalemleri DROP COLUMN KdvOrani;

IF COL_LENGTH('dbo.SiparisKalemleri','KdvTutari') IS NOT NULL
    ALTER TABLE dbo.SiparisKalemleri DROP COLUMN KdvTutari;

IF COL_LENGTH('dbo.SiparisKalemleri','SirketKari') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.SiparisKalemleri DROP CONSTRAINT DF_SiparisKalemleri_SirketKari;
        ALTER TABLE dbo.SiparisKalemleri DROP COLUMN SirketKari;
    END
");
        }
    }
}
