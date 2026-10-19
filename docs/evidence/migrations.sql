IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    CREATE TABLE [Agents] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(200) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [IsActive] bit NOT NULL,
        [MaxOpenTickets] int NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_Agents] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Agents_MaxOpenTickets] CHECK ([MaxOpenTickets] > 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [RequiresSpecialist] bit NOT NULL,
        [ForcesCriticalPriority] bit NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    CREATE TABLE [Customers] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [Phone] nvarchar(50) NULL,
        [Tier] int NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Customers_Tier] CHECK ([Tier] IN (1, 2))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    CREATE TABLE [AgentSpecializations] (
        [AgentId] int NOT NULL,
        [CategoryId] int NOT NULL,
        CONSTRAINT [PK_AgentSpecializations] PRIMARY KEY ([AgentId], [CategoryId]),
        CONSTRAINT [FK_AgentSpecializations_Agents_AgentId] FOREIGN KEY ([AgentId]) REFERENCES [Agents] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AgentSpecializations_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    CREATE TABLE [Tickets] (
        [Id] int NOT NULL IDENTITY,
        [Reference] nvarchar(20) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [CustomerId] int NOT NULL,
        [CategoryId] int NOT NULL,
        [AssignedAgentId] int NULL,
        [Priority] int NOT NULL,
        [Status] int NOT NULL,
        [CreatedAtUtc] datetime2(3) NOT NULL,
        [UpdatedAtUtc] datetime2(3) NOT NULL,
        [DueAtUtc] datetime2(3) NULL,
        [ResolvedAtUtc] datetime2(3) NULL,
        CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Tickets_Priority] CHECK ([Priority] BETWEEN 1 AND 4),
        CONSTRAINT [CK_Tickets_Status] CHECK ([Status] BETWEEN 1 AND 5),
        CONSTRAINT [FK_Tickets_Agents_AssignedAgentId] FOREIGN KEY ([AssignedAgentId]) REFERENCES [Agents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Tickets_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Tickets_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Agents_Email] ON [Agents] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AgentSpecializations_CategoryId] ON [AgentSpecializations] ([CategoryId]) INCLUDE ([AgentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Categories_Name] ON [Categories] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Customers_Email] ON [Customers] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Tickets_AssignedAgentId] ON [Tickets] ([AssignedAgentId]) WHERE [AssignedAgentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_CategoryId] ON [Tickets] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_CustomerId] ON [Tickets] ([CustomerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_DueAtUtc] ON [Tickets] ([DueAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tickets_Status_Priority] ON [Tickets] ([Status], [Priority]) INCLUDE ([DueAtUtc], [AssignedAgentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Tickets_Reference] ON [Tickets] ([Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260926183612_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260926183612_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019132018_TicketEscalationAndSla'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tickets]') AND [c].[name] = N'UpdatedAtUtc');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Tickets] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Tickets] ALTER COLUMN [UpdatedAtUtc] datetime2(7) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019132018_TicketEscalationAndSla'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tickets]') AND [c].[name] = N'ResolvedAtUtc');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Tickets] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [Tickets] ALTER COLUMN [ResolvedAtUtc] datetime2(7) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019132018_TicketEscalationAndSla'
)
BEGIN
    DROP INDEX [IX_Tickets_DueAtUtc] ON [Tickets];
    DROP INDEX [IX_Tickets_Status_Priority] ON [Tickets];
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tickets]') AND [c].[name] = N'DueAtUtc');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Tickets] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [Tickets] ALTER COLUMN [DueAtUtc] datetime2(7) NULL;
    CREATE INDEX [IX_Tickets_DueAtUtc] ON [Tickets] ([DueAtUtc]);
    CREATE INDEX [IX_Tickets_Status_Priority] ON [Tickets] ([Status], [Priority]) INCLUDE ([DueAtUtc], [AssignedAgentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019132018_TicketEscalationAndSla'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tickets]') AND [c].[name] = N'CreatedAtUtc');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Tickets] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [Tickets] ALTER COLUMN [CreatedAtUtc] datetime2(7) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019132018_TicketEscalationAndSla'
)
BEGIN
    ALTER TABLE [Tickets] ADD [SlaAtRiskAtUtc] datetime2(7) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019132018_TicketEscalationAndSla'
)
BEGIN
    CREATE TABLE [TicketEscalation] (
        [Id] int NOT NULL IDENTITY,
        [TicketId] int NOT NULL,
        [FromPriority] int NOT NULL,
        [ToPriority] int NOT NULL,
        [FromAgentId] int NULL,
        [ToAgentId] int NULL,
        [FromDueAtUtc] datetime2(7) NULL,
        [ToDueAtUtc] datetime2(7) NULL,
        [Reason] nvarchar(500) NOT NULL,
        [EscalatedBy] nvarchar(100) NOT NULL,
        [EscalatedAtUtc] datetime2(7) NOT NULL,
        CONSTRAINT [PK_TicketEscalation] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TicketEscalation_Agents_FromAgentId] FOREIGN KEY ([FromAgentId]) REFERENCES [Agents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TicketEscalation_Agents_ToAgentId] FOREIGN KEY ([ToAgentId]) REFERENCES [Agents] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TicketEscalation_Tickets_TicketId] FOREIGN KEY ([TicketId]) REFERENCES [Tickets] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019132018_TicketEscalationAndSla'
)
BEGIN
    CREATE INDEX [IX_TicketEscalation_FromAgentId] ON [TicketEscalation] ([FromAgentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019132018_TicketEscalationAndSla'
)
BEGIN
    CREATE INDEX [IX_TicketEscalation_TicketId_EscalatedAtUtc_Id] ON [TicketEscalation] ([TicketId], [EscalatedAtUtc] DESC, [Id] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019132018_TicketEscalationAndSla'
)
BEGIN
    CREATE INDEX [IX_TicketEscalation_ToAgentId] ON [TicketEscalation] ([ToAgentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019132018_TicketEscalationAndSla'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019132018_TicketEscalationAndSla', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019132532_AgentCredentials'
)
BEGIN
    ALTER TABLE [Agents] ADD [LastSuccessfulLoginAtUtc] datetime2(7) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019132532_AgentCredentials'
)
BEGIN
    ALTER TABLE [Agents] ADD [PasswordHash] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261019132532_AgentCredentials'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261019132532_AgentCredentials', N'10.0.12');
END;

COMMIT;
GO

