
IF DB_ID('RaceDayDb') IS NULL
BEGIN
    CREATE DATABASE RaceDayDb;
END
GO

USE RaceDayDb;
GO

/* ---------- Drop tables if they already exist (reverse FK order) -------- */
IF OBJECT_ID('dbo.Result', 'U') IS NOT NULL DROP TABLE dbo.Result;
IF OBJECT_ID('dbo.Enrolment', 'U') IS NOT NULL DROP TABLE dbo.Enrolment;
IF OBJECT_ID('dbo.Category', 'U') IS NOT NULL DROP TABLE dbo.Category;
IF OBJECT_ID('dbo.Event', 'U') IS NOT NULL DROP TABLE dbo.Event;
IF OBJECT_ID('dbo.[User]', 'U') IS NOT NULL DROP TABLE dbo.[User];
IF OBJECT_ID('dbo.Role', 'U') IS NOT NULL DROP TABLE dbo.Role;
GO

/* =========================================================================
   TABLE: Role
   ========================================================================= */
CREATE TABLE dbo.Role (
    RoleId      INT IDENTITY(1,1) NOT NULL,
    RoleName    VARCHAR(20)       NOT NULL,
    CONSTRAINT PK_Role PRIMARY KEY (RoleId),
    CONSTRAINT UQ_Role_RoleName UNIQUE (RoleName)
);
GO

/* =========================================================================
   TABLE: User
   ========================================================================= */
CREATE TABLE dbo.[User] (
    UserId          INT IDENTITY(1,1) NOT NULL,
    FullName        VARCHAR(100)      NOT NULL,
    Email           VARCHAR(150)      NOT NULL,
    PasswordHash    VARBINARY(MAX)    NOT NULL,
    PasswordSalt    VARBINARY(MAX)    NOT NULL,
    RoleId          INT               NOT NULL,
    CreatedAt       DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_User PRIMARY KEY (UserId),
    CONSTRAINT UQ_User_Email UNIQUE (Email),
    CONSTRAINT FK_User_Role FOREIGN KEY (RoleId) REFERENCES dbo.Role (RoleId)
);
GO

/* =========================================================================
   TABLE: Event
   ========================================================================= */
CREATE TABLE dbo.Event (
    EventId         INT IDENTITY(1,1) NOT NULL,
    Name            VARCHAR(150)      NOT NULL,
    Description     VARCHAR(MAX)      NULL,
    EventDate       DATETIME2         NOT NULL,
    Location        VARCHAR(150)      NOT NULL,
    Distance        DECIMAL(6,2)      NOT NULL,
    EventType       VARCHAR(20)       NOT NULL,
    OrganiserId     INT               NOT NULL,
    BannerBlobName  VARCHAR(260)      NULL,   -- Azure Blob Storage blob name of the banner image
    CONSTRAINT PK_Event PRIMARY KEY (EventId),
    CONSTRAINT FK_Event_Organiser FOREIGN KEY (OrganiserId) REFERENCES dbo.[User] (UserId),
    CONSTRAINT CK_Event_EventType CHECK (EventType IN ('Run', 'Walk', 'Cycle')),
    CONSTRAINT CK_Event_Distance CHECK (Distance > 0)
);
GO

/* =========================================================================
   TABLE: Category
   ========================================================================= */
CREATE TABLE dbo.Category (
    CategoryId          INT IDENTITY(1,1) NOT NULL,
    EventId             INT               NOT NULL,
    Name                VARCHAR(50)       NOT NULL,
    MinAge              INT               NULL,
    MaxAge              INT               NULL,
    CategoryDistance    DECIMAL(6,2)      NOT NULL,
    CONSTRAINT PK_Category PRIMARY KEY (CategoryId),
    CONSTRAINT FK_Category_Event FOREIGN KEY (EventId) REFERENCES dbo.Event (EventId) ON DELETE CASCADE,
    CONSTRAINT CK_Category_Age CHECK (MinAge IS NULL OR MaxAge IS NULL OR MinAge <= MaxAge)
);
GO

/* =========================================================================
   TABLE: Enrolment
   ========================================================================= */
CREATE TABLE dbo.Enrolment (
    EnrolmentId     INT IDENTITY(1,1) NOT NULL,
    ParticipantId   INT               NOT NULL,
    EventId         INT               NOT NULL,
    CategoryId      INT               NOT NULL,
    EnrolmentDate   DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME(),
    Status          VARCHAR(20)       NOT NULL CONSTRAINT DF_Enrolment_Status DEFAULT 'Pending',
    CONSTRAINT PK_Enrolment PRIMARY KEY (EnrolmentId),
    CONSTRAINT FK_Enrolment_Participant FOREIGN KEY (ParticipantId) REFERENCES dbo.[User] (UserId),
    CONSTRAINT FK_Enrolment_Event FOREIGN KEY (EventId) REFERENCES dbo.Event (EventId),
    CONSTRAINT FK_Enrolment_Category FOREIGN KEY (CategoryId) REFERENCES dbo.Category (CategoryId),
    CONSTRAINT UQ_Enrolment_Participant_Event UNIQUE (ParticipantId, EventId),
    CONSTRAINT CK_Enrolment_Status CHECK (Status IN ('Pending', 'Confirmed', 'Cancelled'))
);
GO

/* =========================================================================
   TABLE: Result
   ========================================================================= */
CREATE TABLE dbo.Result (
    ResultId        INT IDENTITY(1,1) NOT NULL,
    EnrolmentId     INT               NOT NULL,
    FinishTime      TIME              NOT NULL,
    FinishPosition  INT               NULL,
    RecordedAt      DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Result PRIMARY KEY (ResultId),
    CONSTRAINT FK_Result_Enrolment FOREIGN KEY (EnrolmentId) REFERENCES dbo.Enrolment (EnrolmentId) ON DELETE CASCADE,
    CONSTRAINT UQ_Result_Enrolment UNIQUE (EnrolmentId)
);
GO

/* =========================================================================
   SEED DATA
   ========================================================================= */

-- Roles
INSERT INTO dbo.Role (RoleName) VALUES ('Organiser'), ('Participant');
GO

-- Users: 2 Organisers, 2 Participants
-- Passwords below are placeholder hash/salt bytes for sample data only -
-- the API hashes real passwords using PBKDF2 (see PasswordHasher.cs).
INSERT INTO dbo.[User] (FullName, Email, PasswordHash, PasswordSalt, RoleId)
VALUES
    ('Thandi Mokoena', 'thandi.organiser@raceday.co.za', 0x01, 0x01, (SELECT RoleId FROM dbo.Role WHERE RoleName = 'Organiser')),
    ('Johan van der Merwe', 'johan.organiser@raceday.co.za', 0x01, 0x01, (SELECT RoleId FROM dbo.Role WHERE RoleName = 'Organiser')),
    ('Lindiwe Dlamini', 'lindiwe.participant@raceday.co.za', 0x01, 0x01, (SELECT RoleId FROM dbo.Role WHERE RoleName = 'Participant')),
    ('Sipho Nkosi', 'sipho.participant@raceday.co.za', 0x01, 0x01, (SELECT RoleId FROM dbo.Role WHERE RoleName = 'Participant'));
GO

-- Events: 3 events across the two Organisers
INSERT INTO dbo.Event (Name, Description, EventDate, Location, Distance, EventType, OrganiserId)
VALUES
    ('Johannesburg City 10K', 'A fast, flat 10km road race through the Joburg CBD.', '2026-11-14 07:00:00', 'Johannesburg, Gauteng', 10.00, 'Run',
        (SELECT UserId FROM dbo.[User] WHERE Email = 'thandi.organiser@raceday.co.za')),
    ('Cradle Trail Cycle Challenge', 'Off-road cycling route through the Cradle of Humankind.', '2026-10-03 06:30:00', 'Cradle of Humankind, Gauteng', 42.00, 'Cycle',
        (SELECT UserId FROM dbo.[User] WHERE Email = 'thandi.organiser@raceday.co.za')),
    ('Pretoria Fun Walk', 'Family-friendly community walk for all ages.', '2026-09-20 08:00:00', 'Pretoria, Gauteng', 5.00, 'Walk',
        (SELECT UserId FROM dbo.[User] WHERE Email = 'johan.organiser@raceday.co.za'));
GO

-- Categories: at least one per event
INSERT INTO dbo.Category (EventId, Name, MinAge, MaxAge, CategoryDistance)
VALUES
    ((SELECT EventId FROM dbo.Event WHERE Name = 'Johannesburg City 10K'), 'Under 20', 0, 19, 10.00),
    ((SELECT EventId FROM dbo.Event WHERE Name = 'Johannesburg City 10K'), 'Senior', 20, 59, 10.00),
    ((SELECT EventId FROM dbo.Event WHERE Name = 'Johannesburg City 10K'), 'Veteran', 60, 120, 10.00),
    ((SELECT EventId FROM dbo.Event WHERE Name = 'Cradle Trail Cycle Challenge'), 'Open 42km', 16, 120, 42.00),
    ((SELECT EventId FROM dbo.Event WHERE Name = 'Cradle Trail Cycle Challenge'), 'Open 21km', 16, 120, 21.00),
    ((SELECT EventId FROM dbo.Event WHERE Name = 'Pretoria Fun Walk'), 'Family (all ages)', 0, 120, 5.00);
GO

-- Sample enrolments: both participants enter events under valid categories
INSERT INTO dbo.Enrolment (ParticipantId, EventId, CategoryId)
VALUES
    ((SELECT UserId FROM dbo.[User] WHERE Email = 'lindiwe.participant@raceday.co.za'),
     (SELECT EventId FROM dbo.Event WHERE Name = 'Johannesburg City 10K'),
     (SELECT CategoryId FROM dbo.Category WHERE Name = 'Senior' AND EventId = (SELECT EventId FROM dbo.Event WHERE Name = 'Johannesburg City 10K'))),
    ((SELECT UserId FROM dbo.[User] WHERE Email = 'sipho.participant@raceday.co.za'),
     (SELECT EventId FROM dbo.Event WHERE Name = 'Johannesburg City 10K'),
     (SELECT CategoryId FROM dbo.Category WHERE Name = 'Senior' AND EventId = (SELECT EventId FROM dbo.Event WHERE Name = 'Johannesburg City 10K'))),
    ((SELECT UserId FROM dbo.[User] WHERE Email = 'lindiwe.participant@raceday.co.za'),
     (SELECT EventId FROM dbo.Event WHERE Name = 'Pretoria Fun Walk'),
     (SELECT CategoryId FROM dbo.Category WHERE Name = 'Family (all ages)'));
GO

-- Sample result for a completed enrolment
INSERT INTO dbo.Result (EnrolmentId, FinishTime, FinishPosition)
VALUES
    ((SELECT EnrolmentId FROM dbo.Enrolment
        WHERE ParticipantId = (SELECT UserId FROM dbo.[User] WHERE Email = 'lindiwe.participant@raceday.co.za')
        AND EventId = (SELECT EventId FROM dbo.Event WHERE Name = 'Johannesburg City 10K')),
     '00:52:31', 14);
GO

/* ---------- Quick verification queries (optional, comment out if not needed) */
-- SELECT * FROM dbo.Role;
-- SELECT * FROM dbo.[User];
-- SELECT * FROM dbo.Event;
-- SELECT * FROM dbo.Category;
-- SELECT * FROM dbo.Enrolment;
-- SELECT * FROM dbo.Result;
