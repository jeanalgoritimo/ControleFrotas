CREATE TABLE [Accounts] (
    [Id] int NOT NULL IDENTITY,
    [CompanyId] int NOT NULL,
    [Username] nvarchar(100) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Audits] (
    [Id] bigint NOT NULL IDENTITY,
    [CompanyId] int NOT NULL,
    [Actor] nvarchar(max) NOT NULL,
    [Action] nvarchar(max) NOT NULL,
    [RecordId] int NOT NULL,
    [AtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Audits] PRIMARY KEY ([Id])
);
GO


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
GO


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
GO


CREATE UNIQUE INDEX [IX_Accounts_Username] ON [Accounts] ([Username]);
GO


CREATE UNIQUE INDEX [IX_Drivers_CompanyId_Cpf] ON [Drivers] ([CompanyId], [Cpf]);
GO


CREATE UNIQUE INDEX [IX_Vehicles_CompanyId_Plate] ON [Vehicles] ([CompanyId], [Plate]);
GO


