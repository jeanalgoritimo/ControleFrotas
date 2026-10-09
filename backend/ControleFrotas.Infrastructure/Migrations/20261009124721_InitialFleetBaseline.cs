using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace ControleFrotas.Infrastructure.Migrations;

public partial class InitialFleetBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Empty databases are created. Existing v0.1 databases are validated and adopted without recreating tables.
        migrationBuilder.Sql("""
IF NOT EXISTS(SELECT 1 FROM sys.tables WHERE is_ms_shipped=0 AND name<>N'__EFMigrationsHistory')
BEGIN
CREATE TABLE [Accounts] (
    [Id] int NOT NULL IDENTITY,
    [CompanyId] int NOT NULL,
    [Username] nvarchar(100) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id])
);


CREATE TABLE [Audits] (
    [Id] bigint NOT NULL IDENTITY,
    [CompanyId] int NOT NULL,
    [Actor] nvarchar(max) NOT NULL,
    [Action] nvarchar(max) NOT NULL,
    [RecordId] int NOT NULL,
    [AtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Audits] PRIMARY KEY ([Id])
);


CREATE TABLE [Drivers] (
    [Id] int NOT NULL IDENTITY,
    [CompanyId] int NOT NULL,
    [Name] nvarchar(150) NOT NULL,
    [Cpf] nvarchar(11) NOT NULL,
    [License] nvarchar(11) NOT NULL,
    [LicenseCategory] nvarchar(2) NOT NULL,
    [LicenseExpiry] date NOT NULL,
    [Active] bit NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Drivers] PRIMARY KEY ([Id])
);


CREATE TABLE [Vehicles] (
    [Id] int NOT NULL IDENTITY,
    [CompanyId] int NOT NULL,
    [Plate] nvarchar(7) NOT NULL,
    [Category] nvarchar(20) NOT NULL,
    [Brand] nvarchar(80) NOT NULL,
    [Model] nvarchar(100) NOT NULL,
    [Year] int NOT NULL,
    [Odometer] decimal(12,3) NOT NULL,
    [Active] bit NOT NULL,
    [Version] rowversion NOT NULL,
    CONSTRAINT [PK_Vehicles] PRIMARY KEY ([Id])
);


CREATE UNIQUE INDEX [IX_Accounts_Username] ON [Accounts] ([Username]);


CREATE UNIQUE INDEX [IX_Drivers_CompanyId_Cpf] ON [Drivers] ([CompanyId], [Cpf]);


CREATE UNIQUE INDEX [IX_Vehicles_CompanyId_Plate] ON [Vehicles] ([CompanyId], [Plate]);

END
ELSE
BEGIN

DECLARE @expected TABLE (TableName sysname, ColumnName sysname, TypeName sysname, MaxLength smallint, NumericPrecision tinyint, NumericScale tinyint, IsNullable bit, IsIdentity bit);
INSERT INTO @expected VALUES
(N'Accounts',N'Id',N'int',4,0,0,0,1),
(N'Accounts',N'CompanyId',N'int',4,0,0,0,0),
(N'Accounts',N'Username',N'nvarchar',200,0,0,0,0),
(N'Accounts',N'PasswordHash',N'nvarchar',-1,0,0,0,0),
(N'Audits',N'Id',N'bigint',8,0,0,0,1),
(N'Audits',N'CompanyId',N'int',4,0,0,0,0),
(N'Audits',N'Actor',N'nvarchar',-1,0,0,0,0),
(N'Audits',N'Action',N'nvarchar',-1,0,0,0,0),
(N'Audits',N'RecordId',N'int',4,0,0,0,0),
(N'Audits',N'AtUtc',N'datetime2',8,0,0,0,0),
(N'Drivers',N'Id',N'int',4,0,0,0,1),
(N'Drivers',N'CompanyId',N'int',4,0,0,0,0),
(N'Drivers',N'Name',N'nvarchar',300,0,0,0,0),
(N'Drivers',N'Cpf',N'nvarchar',22,0,0,0,0),
(N'Drivers',N'License',N'nvarchar',22,0,0,0,0),
(N'Drivers',N'LicenseCategory',N'nvarchar',4,0,0,0,0),
(N'Drivers',N'LicenseExpiry',N'date',3,0,0,0,0),
(N'Drivers',N'Active',N'bit',1,0,0,0,0),
(N'Drivers',N'Version',N'timestamp',8,0,0,0,0),
(N'Vehicles',N'Id',N'int',4,0,0,0,1),
(N'Vehicles',N'CompanyId',N'int',4,0,0,0,0),
(N'Vehicles',N'Plate',N'nvarchar',14,0,0,0,0),
(N'Vehicles',N'Category',N'nvarchar',40,0,0,0,0),
(N'Vehicles',N'Brand',N'nvarchar',160,0,0,0,0),
(N'Vehicles',N'Model',N'nvarchar',200,0,0,0,0),
(N'Vehicles',N'Year',N'int',4,0,0,0,0),
(N'Vehicles',N'Odometer',N'decimal',9,12,3,0,0),
(N'Vehicles',N'Active',N'bit',1,0,0,0,0),
(N'Vehicles',N'Version',N'timestamp',8,0,0,0,0);
IF EXISTS (
    SELECT 1 FROM @expected e
    LEFT JOIN sys.tables t ON t.name = e.TableName AND t.schema_id = SCHEMA_ID(N'dbo')
    LEFT JOIN sys.columns c ON c.object_id = t.object_id AND c.name = e.ColumnName
    LEFT JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    WHERE c.column_id IS NULL OR ty.name <> e.TypeName OR c.max_length <> e.MaxLength
      OR c.is_nullable <> e.IsNullable OR c.is_identity <> e.IsIdentity OR c.is_computed <> 0
      OR (e.TypeName = N'decimal' AND (c.precision <> e.NumericPrecision OR c.scale <> e.NumericScale))
) OR EXISTS (
    SELECT 1 FROM sys.columns c JOIN sys.tables t ON t.object_id=c.object_id
    WHERE t.schema_id=SCHEMA_ID(N'dbo') AND t.name IN (N'Accounts',N'Audits',N'Drivers',N'Vehicles')
      AND NOT EXISTS (SELECT 1 FROM @expected e WHERE e.TableName=t.name AND e.ColumnName=c.name)
)
    THROW 51000, N'Banco incompatível com a v0.1. Atualização interrompida; nenhuma tabela de cadastro será recriada.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID(N'dbo.Accounts') AND i.is_primary_key=1 AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0)=1 AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'Id')) THROW 51000,N'Chave primária incompatível com v0.1.',1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID(N'dbo.Audits') AND i.is_primary_key=1 AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0)=1 AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'Id')) THROW 51000,N'Chave primária incompatível com v0.1.',1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID(N'dbo.Drivers') AND i.is_primary_key=1 AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0)=1 AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'Id')) THROW 51000,N'Chave primária incompatível com v0.1.',1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID(N'dbo.Vehicles') AND i.is_primary_key=1 AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0)=1 AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'Id')) THROW 51000,N'Chave primária incompatível com v0.1.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID(N'dbo.Accounts') AND i.is_unique=1 AND i.has_filter=0 AND i.is_disabled=0 AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'Username') AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0)=1) THROW 51000,N'Índice único incompatível com v0.1.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID(N'dbo.Drivers') AND i.is_unique=1 AND i.has_filter=0 AND i.is_disabled=0 AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'CompanyId') AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'Cpf') AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0)=2) THROW 51000,N'Índice único incompatível com v0.1.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes i WHERE i.object_id=OBJECT_ID(N'dbo.Vehicles') AND i.is_unique=1 AND i.has_filter=0 AND i.is_disabled=0 AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1 AND c.name=N'CompanyId') AND EXISTS(SELECT 1 FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=2 AND c.name=N'Plate') AND (SELECT COUNT(*) FROM sys.index_columns ic WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0)=2) THROW 51000,N'Índice único incompatível com v0.1.',1;

END
""");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("O baseline não pode ser revertido automaticamente para proteger os cadastros existentes.");
}
