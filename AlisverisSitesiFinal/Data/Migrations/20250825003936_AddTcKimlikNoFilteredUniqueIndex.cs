using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlisverisSitesiFinal.Data.Migrations
{
    public partial class AddTcKimlikNoFilteredUniqueIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NOT NULL
BEGIN
    DECLARE @objId INT = OBJECT_ID(N'dbo.AspNetUsers', N'U');

    ----------------------------------------------------------------
    -- 1) TcKimlikNo üzerinde duran TÜM index'leri düşür
    ----------------------------------------------------------------
    DECLARE @sql NVARCHAR(MAX) = N'';

    SELECT @sql = @sql + N'DROP INDEX [' + i.name + N'] ON [dbo].[AspNetUsers];' + CHAR(13)
    FROM sys.indexes i
    JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE i.object_id = @objId
      AND c.name = N'TcKimlikNo'
      AND i.name IS NOT NULL
      AND i.is_hypothetical = 0;

    IF LEN(@sql) > 0 EXEC sp_executesql @sql;

    ----------------------------------------------------------------
    -- 2) TcKimlikNo üzerinde duran UNIQUE CONSTRAINT (UQ) varsa düşür
    ----------------------------------------------------------------
    SET @sql = N'';
    SELECT @sql = @sql + N'ALTER TABLE [dbo].[AspNetUsers] DROP CONSTRAINT [' + kc.name + N'];' + CHAR(13)
    FROM sys.key_constraints kc
    JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE kc.parent_object_id = @objId
      AND c.name = N'TcKimlikNo'
      AND kc.[type] = 'UQ';

    IF LEN(@sql) > 0 EXEC sp_executesql @sql;

    ----------------------------------------------------------------
    -- 3) Varsayılan kısıt (default constraint) varsa düşür
    ----------------------------------------------------------------
    DECLARE @dc sysname;
    SELECT @dc = d.name
    FROM sys.default_constraints d
    INNER JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
    WHERE d.parent_object_id = @objId AND c.name = N'TcKimlikNo';

    IF @dc IS NOT NULL
        EXEC(N'ALTER TABLE [dbo].[AspNetUsers] DROP CONSTRAINT [' + @dc + ']');

    ----------------------------------------------------------------
    -- 4) Sütunu ekle/ALTER et
    ----------------------------------------------------------------
    IF COL_LENGTH('dbo.AspNetUsers', 'TcKimlikNo') IS NULL
    BEGIN
        ALTER TABLE [dbo].[AspNetUsers] ADD [TcKimlikNo] NVARCHAR(450) NULL;
    END
    ELSE
    BEGIN
        ALTER TABLE [dbo].[AspNetUsers] ALTER COLUMN [TcKimlikNo] NVARCHAR(450) NULL;
    END

    ----------------------------------------------------------------
    -- 5) Filtreli UNIQUE INDEX (yalnızca NOT NULL değerler tekil)
    ----------------------------------------------------------------
    CREATE UNIQUE INDEX [IX_AspNetUsers_TcKimlikNo]
    ON [dbo].[AspNetUsers]([TcKimlikNo])
    WHERE [TcKimlikNo] IS NOT NULL;
END
", suppressTransaction: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NOT NULL
BEGIN
    -- Oluşturduğumuz filtreli index'i düşür
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetUsers_TcKimlikNo' AND object_id = OBJECT_ID(N'dbo.AspNetUsers'))
        DROP INDEX [IX_AspNetUsers_TcKimlikNo] ON [dbo].[AspNetUsers];

    -- (İsteğe bağlı) Sütunu kaldırmak istersen:
    -- IF COL_LENGTH('dbo.AspNetUsers', 'TcKimlikNo') IS NOT NULL
    --     ALTER TABLE [dbo].[AspNetUsers] DROP COLUMN [TcKimlikNo];
END
", suppressTransaction: false);
        }
    }
}
