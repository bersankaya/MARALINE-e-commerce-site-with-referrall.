using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlisverisSitesiFinal.Data.Migrations
{
    public partial class AddRefundFieldsToIadeTalebi : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kolon zaten varsa ekleme; yoksa ekle (idempotent)
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.IadeTalepleri','IadeTutar') IS NULL
    ALTER TABLE [dbo].[IadeTalepleri] ADD [IadeTutar] decimal(18,2) NULL;

IF COL_LENGTH('dbo.IadeTalepleri','IadeYontemi') IS NULL
    ALTER TABLE [dbo].[IadeTalepleri] ADD [IadeYontemi] nvarchar(200) NULL;

IF COL_LENGTH('dbo.IadeTalepleri','IadeYapanUserId') IS NULL
    ALTER TABLE [dbo].[IadeTalepleri] ADD [IadeYapanUserId] nvarchar(450) NULL;

IF COL_LENGTH('dbo.IadeTalepleri','IadeOdemeTarihi') IS NULL
    ALTER TABLE [dbo].[IadeTalepleri] ADD [IadeOdemeTarihi] datetime2 NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Varsa sil (idempotent drop)
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.IadeTalepleri','IadeTutar') IS NOT NULL
    ALTER TABLE [dbo].[IadeTalepleri] DROP COLUMN [IadeTutar];

IF COL_LENGTH('dbo.IadeTalepleri','IadeYontemi') IS NOT NULL
    ALTER TABLE [dbo].[IadeTalepleri] DROP COLUMN [IadeYontemi];

IF COL_LENGTH('dbo.IadeTalepleri','IadeYapanUserId') IS NOT NULL
    ALTER TABLE [dbo].[IadeTalepleri] DROP COLUMN [IadeYapanUserId];

IF COL_LENGTH('dbo.IadeTalepleri','IadeOdemeTarihi') IS NOT NULL
    ALTER TABLE [dbo].[IadeTalepleri] DROP COLUMN [IadeOdemeTarihi];
");
        }
    }
}
