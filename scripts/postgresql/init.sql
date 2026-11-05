-- ============================================================================
-- ORGCHAT — Schéma PostgreSQL (équivalent de la migration EF Core InitialCreate)
-- Base de données exclusivement côté serveur. Les clients n'y accèdent jamais directement.
-- ============================================================================

-- --------------------------------------------------------
-- Table : Users
-- --------------------------------------------------------
CREATE TABLE Users (
    Id SERIAL PRIMARY KEY,
    Username VARCHAR(100) NOT NULL UNIQUE,
    Email VARCHAR(256) NOT NULL UNIQUE,
    DisplayName VARCHAR(100) NOT NULL,
    PasswordHash TEXT NOT NULL,
    Role SMALLINT NOT NULL DEFAULT 1,          -- 1 = User, 2 = Admin
    IsActive BOOLEAN NOT NULL DEFAULT TRUE,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

-- --------------------------------------------------------
-- Table : Conversations
-- --------------------------------------------------------
CREATE TABLE Conversations (
    Id SERIAL PRIMARY KEY,
    Title VARCHAR(200),
    Type SMALLINT NOT NULL,                   -- 1 = Private, 2 = Group
    CreatedById INTEGER NOT NULL,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT FK_Conversations_Users_CreatedById
        FOREIGN KEY (CreatedById) REFERENCES Users(Id) ON DELETE RESTRICT
);

-- --------------------------------------------------------
-- Table : ConversationParticipants
-- --------------------------------------------------------
CREATE TABLE ConversationParticipants (
    Id SERIAL PRIMARY KEY,
    ConversationId INTEGER NOT NULL,
    UserId INTEGER NOT NULL,
    JoinedAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    LastReadAt TIMESTAMP WITH TIME ZONE,
    IsGroupAdmin BOOLEAN NOT NULL DEFAULT FALSE,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT FK_ConversationParticipants_Conversations_ConversationId
        FOREIGN KEY (ConversationId) REFERENCES Conversations(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ConversationParticipants_Users_UserId
        FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE RESTRICT,
    CONSTRAINT UQ_ConversationParticipants_ConversationId_UserId
        UNIQUE (ConversationId, UserId)
);

-- --------------------------------------------------------
-- Table : Messages
-- --------------------------------------------------------
CREATE TABLE Messages (
    Id SERIAL PRIMARY KEY,
    ConversationId INTEGER NOT NULL,
    SenderId INTEGER NOT NULL,
    Content TEXT NOT NULL,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),  -- horodatage serveur
    EditedAt TIMESTAMP WITH TIME ZONE,
    IsDeleted BOOLEAN NOT NULL DEFAULT FALSE,
    DeletedAt TIMESTAMP WITH TIME ZONE,
    CONSTRAINT FK_Messages_Conversations_ConversationId
        FOREIGN KEY (ConversationId) REFERENCES Conversations(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Messages_Users_SenderId
        FOREIGN KEY (SenderId) REFERENCES Users(Id) ON DELETE RESTRICT
);

-- --------------------------------------------------------
-- Table : Attachments
-- --------------------------------------------------------
CREATE TABLE Attachments (
    Id SERIAL PRIMARY KEY,
    MessageId INTEGER NOT NULL,
    OriginalFileName VARCHAR(500) NOT NULL,
    StorageFileName VARCHAR(200) NOT NULL,    -- nom généré par le serveur
    ContentType VARCHAR(100) NOT NULL,
    SizeInBytes BIGINT NOT NULL,
    StoragePath VARCHAR(1000) NOT NULL,       -- chemin hors web root
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT FK_Attachments_Messages_MessageId
        FOREIGN KEY (MessageId) REFERENCES Messages(Id) ON DELETE CASCADE
);

-- --------------------------------------------------------
-- Table : Notifications
-- --------------------------------------------------------
CREATE TABLE Notifications (
    Id SERIAL PRIMARY KEY,
    UserId INTEGER NOT NULL,
    Type VARCHAR(100) NOT NULL,
    Payload TEXT NOT NULL,                    -- contenu JSON
    ReadAt TIMESTAMP WITH TIME ZONE,
    CreatedAt TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT FK_Notifications_Users_UserId
        FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- --------------------------------------------------------
-- Index (équivalent des index EF Core)
-- --------------------------------------------------------
CREATE INDEX IX_Users_Username ON Users(Username);
CREATE INDEX IX_Users_Email ON Users(Email);

CREATE INDEX IX_Conversations_CreatedById ON Conversations(CreatedById);

CREATE INDEX IX_ConversationParticipants_ConversationId ON ConversationParticipants(ConversationId);
CREATE INDEX IX_ConversationParticipants_UserId ON ConversationParticipants(UserId);

CREATE INDEX IX_Messages_ConversationId_CreatedAt ON Messages(ConversationId, CreatedAt);
CREATE INDEX IX_Messages_SenderId ON Messages(SenderId);

CREATE INDEX IX_Attachments_MessageId ON Attachments(MessageId);

CREATE INDEX IX_Notifications_UserId ON Notifications(UserId);

-- --------------------------------------------------------
-- Note : pour utiliser ce schéma avec Entity Framework Core (Code First),
-- il faut configurer le provider Npgsql dans OrgChat.Infrastructure :
--
--   services.AddDbContext<AppDbContext>(options =>
--       options.UseNpgsql(connectionString));
--
-- et remplacer Microsoft.EntityFrameworkCore.Sqlite par Npgsql.EntityFrameworkCore.PostgreSQL.
-- ============================================================================
