IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100218_InitialTenancy'
)
BEGIN
    CREATE TABLE [dbo].[Tenants] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Tenants_Id] CHECK ([Id] <> '00000000-0000-0000-0000-000000000000'),
        CONSTRAINT [CK_Tenants_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100218_InitialTenancy'
)
BEGIN
    CREATE TABLE [dbo].[Stores] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Code] nvarchar(30) COLLATE Greek_CI_AS NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        CONSTRAINT [PK_Stores] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Stores_Code] CHECK (LEN(LTRIM(RTRIM([Code]))) > 0),
        CONSTRAINT [CK_Stores_Id] CHECK ([Id] <> '00000000-0000-0000-0000-000000000000'),
        CONSTRAINT [CK_Stores_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0),
        CONSTRAINT [FK_Stores_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100218_InitialTenancy'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Stores_TenantId_Code] ON [dbo].[Stores] ([TenantId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109100218_InitialTenancy'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109100218_InitialTenancy', N'10.0.14');
END;

COMMIT;
GO

