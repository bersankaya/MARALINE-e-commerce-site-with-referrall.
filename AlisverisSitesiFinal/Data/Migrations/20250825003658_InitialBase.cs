using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlisverisSitesiFinal.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Soyad = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TcKimlikNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReferansKodu = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SponsorId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ToplamHarcama = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ToplamKazanc = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ToplamKazanilanPara = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AylikKazanilanPara = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KayitTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HasMetReferralThreshold = table.Column<bool>(type: "bit", nullable: false),
                    IsReferralCodeActive = table.Column<bool>(type: "bit", nullable: false),
                    HasTriggeredAdminInitialDirectPairBonus = table.Column<bool>(type: "bit", nullable: false),
                    ActiveDirectReferralCount = table.Column<int>(type: "int", nullable: false),
                    UsedBackupReferral = table.Column<bool>(type: "bit", nullable: false),
                    OzelReferansLimiti = table.Column<int>(type: "int", nullable: true),
                    IBAN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUsers_AspNetUsers_SponsorId",
                        column: x => x.SponsorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BonusLoglari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KullaniciId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tarih = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BonusLoglari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Kategoriler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kategoriler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MagazaBasvurulari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KullaniciId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MagazaAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VergiNo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IBAN = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BasvuruTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MagazaBasvurulari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParaCekmeTalepleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KullaniciId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IBANSahibiAdSoyad = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IBAN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TalepTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OnayTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OnaylandiMi = table.Column<bool>(type: "bit", nullable: false),
                    ReddedildiMi = table.Column<bool>(type: "bit", nullable: false),
                    AdminNotu = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParaCekmeTalepleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaticiOdemeler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaticiId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OdemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Durum = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    BrutToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KomisyonToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaticiOdemeler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Adresler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KullaniciId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AdresBasligi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AdresDetayi = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Il = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Ilce = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PostaKodu = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Telefon = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsVarsayilan = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Adresler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Adresler_AspNetUsers_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Magazalar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    VergiNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IBAN = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    Telefon = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Adres = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Magazalar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Magazalar_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SaticiOdemeKalemleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SaticiOdemeId = table.Column<int>(type: "int", nullable: false),
                    SiparisKalemiId = table.Column<int>(type: "int", nullable: false),
                    UrunId = table.Column<int>(type: "int", nullable: false),
                    UrunAdi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Miktar = table.Column<int>(type: "int", nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SaticiTeklifBirimFiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SatirBrut = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SatirSaticiyaNet = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SatirPlatformGeliri = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaticiOdemeKalemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaticiOdemeKalemleri_SaticiOdemeler_SaticiOdemeId",
                        column: x => x.SaticiOdemeId,
                        principalTable: "SaticiOdemeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Uruns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Fiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StokAdedi = table.Column<int>(type: "int", nullable: false),
                    ResimUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EklenmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsSlider = table.Column<bool>(type: "bit", nullable: false),
                    IsPopular = table.Column<bool>(type: "bit", nullable: false),
                    IsAvantajli = table.Column<bool>(type: "bit", nullable: false),
                    IsCokSatan = table.Column<bool>(type: "bit", nullable: false),
                    KategoriId = table.Column<int>(type: "int", nullable: false),
                    Etiket = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SaticiTeklifFiyati = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FiyatAdmin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    FiyatReferansli = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    YayindaMi = table.Column<bool>(type: "bit", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<int>(type: "int", nullable: true),
                    BoyutSecimiVar = table.Column<bool>(type: "bit", nullable: false),
                    BedenSecimiVar = table.Column<bool>(type: "bit", nullable: false),
                    RenkSecimiVar = table.Column<bool>(type: "bit", nullable: false),
                    NumaraSecimiVar = table.Column<bool>(type: "bit", nullable: false),
                    KapasiteSecimiVar = table.Column<bool>(type: "bit", nullable: false),
                    MateryalSecimiVar = table.Column<bool>(type: "bit", nullable: false),
                    DesenSecimiVar = table.Column<bool>(type: "bit", nullable: false),
                    BedenSecenekleri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BoyutSecenekleri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RenkSecenekleri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumaraSecenekleri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KapasiteSecenekleri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MateryalSecenekleri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DesenSecenekleri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MagazaId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Uruns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Uruns_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Uruns_Kategoriler_KategoriId",
                        column: x => x.KategoriId,
                        principalTable: "Kategoriler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Uruns_Magazalar_MagazaId",
                        column: x => x.MagazaId,
                        principalTable: "Magazalar",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Uruns_Magazalar_StoreId",
                        column: x => x.StoreId,
                        principalTable: "Magazalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Sepet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KullaniciId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UrunId = table.Column<int>(type: "int", nullable: false),
                    Miktar = table.Column<int>(type: "int", nullable: false),
                    Renk = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Beden = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Boyut = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Numara = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Kapasite = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Materyal = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Desen = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sepet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sepet_AspNetUsers_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sepet_Uruns_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Uruns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Siparisler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdentityUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SiparisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Durum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UrunId = table.Column<int>(type: "int", nullable: false),
                    AdresId = table.Column<int>(type: "int", nullable: true),
                    ReddetmeNedeni = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderRefKey = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferralIslenmisMi = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Siparisler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Siparisler_Adresler_AdresId",
                        column: x => x.AdresId,
                        principalTable: "Adresler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Siparisler_AspNetUsers_IdentityUserId",
                        column: x => x.IdentityUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Siparisler_Uruns_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Uruns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Yorumlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KullaniciId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UrunId = table.Column<int>(type: "int", nullable: false),
                    Icerik = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Tarih = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Puan = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Yorumlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Yorumlar_AspNetUsers_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Yorumlar_Uruns_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Uruns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiparisKalemleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SiparisId = table.Column<int>(type: "int", nullable: false),
                    UrunId = table.Column<int>(type: "int", nullable: false),
                    Miktar = table.Column<int>(type: "int", nullable: false),
                    BirimFiyat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Renk = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Beden = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Boyut = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Numara = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Kapasite = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Materyal = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Desen = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SaticiId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KalemDurum = table.Column<int>(type: "int", nullable: false),
                    MagazaNotu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KargoTakipNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SaticiTeklifAnlik = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AdminFiyatAnlik = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RefFiyatAnlik = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SaticiOdemeyeDahilMi = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiparisKalemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SiparisKalemleri_Siparisler_SiparisId",
                        column: x => x.SiparisId,
                        principalTable: "Siparisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SiparisKalemleri_Uruns_UrunId",
                        column: x => x.UrunId,
                        principalTable: "Uruns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Adresler_KullaniciId",
                table: "Adresler",
                column: "KullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PhoneNumber",
                table: "AspNetUsers",
                column: "PhoneNumber",
                unique: true,
                filter: "[PhoneNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SponsorId",
                table: "AspNetUsers",
                column: "SponsorId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TcKimlikNo",
                table: "AspNetUsers",
                column: "TcKimlikNo",
                unique: true,
                filter: "[TcKimlikNo] IS NOT NULL AND [TcKimlikNo] <> N''");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Magazalar_OwnerUserId",
                table: "Magazalar",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SaticiOdemeKalemleri_SaticiOdemeId",
                table: "SaticiOdemeKalemleri",
                column: "SaticiOdemeId");

            migrationBuilder.CreateIndex(
                name: "IX_Sepet_KullaniciId",
                table: "Sepet",
                column: "KullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_Sepet_UrunId",
                table: "Sepet",
                column: "UrunId");

            migrationBuilder.CreateIndex(
                name: "IX_SiparisKalemleri_SiparisId",
                table: "SiparisKalemleri",
                column: "SiparisId");

            migrationBuilder.CreateIndex(
                name: "IX_SiparisKalemleri_UrunId",
                table: "SiparisKalemleri",
                column: "UrunId");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_AdresId",
                table: "Siparisler",
                column: "AdresId");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_IdentityUserId",
                table: "Siparisler",
                column: "IdentityUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Siparisler_UrunId",
                table: "Siparisler",
                column: "UrunId");

            migrationBuilder.CreateIndex(
                name: "IX_Uruns_KategoriId",
                table: "Uruns",
                column: "KategoriId");

            migrationBuilder.CreateIndex(
                name: "IX_Uruns_MagazaId",
                table: "Uruns",
                column: "MagazaId");

            migrationBuilder.CreateIndex(
                name: "IX_Uruns_StoreId",
                table: "Uruns",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_Uruns_UserId",
                table: "Uruns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Yorumlar_KullaniciId",
                table: "Yorumlar",
                column: "KullaniciId");

            migrationBuilder.CreateIndex(
                name: "IX_Yorumlar_UrunId",
                table: "Yorumlar",
                column: "UrunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BonusLoglari");

            migrationBuilder.DropTable(
                name: "MagazaBasvurulari");

            migrationBuilder.DropTable(
                name: "ParaCekmeTalepleri");

            migrationBuilder.DropTable(
                name: "SaticiOdemeKalemleri");

            migrationBuilder.DropTable(
                name: "Sepet");

            migrationBuilder.DropTable(
                name: "SiparisKalemleri");

            migrationBuilder.DropTable(
                name: "Yorumlar");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "SaticiOdemeler");

            migrationBuilder.DropTable(
                name: "Siparisler");

            migrationBuilder.DropTable(
                name: "Adresler");

            migrationBuilder.DropTable(
                name: "Uruns");

            migrationBuilder.DropTable(
                name: "Kategoriler");

            migrationBuilder.DropTable(
                name: "Magazalar");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
