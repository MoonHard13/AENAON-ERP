BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110355_IdentityAndTenantMemberships'
)
BEGIN
    CREATE TABLE [dbo].[AspNetUsers] (
        [Id] uniqueidentifier NOT NULL,
        [UserName] nvarchar(256) NOT NULL,
        [NormalizedUserName] nvarchar(256) NOT NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(256) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_AspNetUsers_Id] CHECK ([Id] <> '00000000-0000-0000-0000-000000000000'),
        CONSTRAINT [CK_AspNetUsers_NormalizedUserName] CHECK (LEN(LTRIM(RTRIM([NormalizedUserName]))) > 0),
        CONSTRAINT [CK_AspNetUsers_UserName] CHECK (LEN(LTRIM(RTRIM([UserName]))) > 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110355_IdentityAndTenantMemberships'
)
BEGIN
    CREATE TABLE [dbo].[AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] uniqueidentifier NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110355_IdentityAndTenantMemberships'
)
BEGIN
    CREATE TABLE [dbo].[AspNetUserLogins] (
        [LoginProvider] nvarchar(128) NOT NULL,
        [ProviderKey] nvarchar(128) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110355_IdentityAndTenantMemberships'
)
BEGIN
    CREATE TABLE [dbo].[AspNetUserTokens] (
        [UserId] uniqueidentifier NOT NULL,
        [LoginProvider] nvarchar(128) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110355_IdentityAndTenantMemberships'
)
BEGIN
    CREATE TABLE [dbo].[TenantMemberships] (
        [TenantId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Role] int NOT NULL,
        CONSTRAINT [PK_TenantMemberships] PRIMARY KEY ([TenantId], [UserId]),
        CONSTRAINT [CK_TenantMemberships_Role] CHECK ([Role] IN (1, 2)),
        CONSTRAINT [FK_TenantMemberships_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TenantMemberships_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110355_IdentityAndTenantMemberships'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110355_IdentityAndTenantMemberships'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110355_IdentityAndTenantMemberships'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [EmailIndex] ON [dbo].[AspNetUsers] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110355_IdentityAndTenantMemberships'
)
BEGIN
    CREATE UNIQUE INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([NormalizedUserName]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110355_IdentityAndTenantMemberships'
)
BEGIN
    CREATE INDEX [IX_TenantMemberships_UserId] ON [dbo].[TenantMemberships] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261109110355_IdentityAndTenantMemberships'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261109110355_IdentityAndTenantMemberships', N'10.0.14');
END;

COMMIT;
GO

