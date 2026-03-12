CREATE TABLE [dbo].[SecurityAudits] (
    [Id]               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    [ClientId]         NVARCHAR(100)    NOT NULL,
    [ResourceKey]      NVARCHAR(200)    NOT NULL,
    [Success]          BIT              NOT NULL,
    [Reason]           NVARCHAR(MAX)    NULL,
    [ClientIp]         NVARCHAR(50)     NOT NULL,
    [RequestTimestamp] DATETIME2(7)     NOT NULL DEFAULT (SYSUTCDATETIME())
);

CREATE INDEX IX_SecurityAudits_ClientId ON [dbo].[SecurityAudits] ([ClientId]);
CREATE INDEX IX_SecurityAudits_Timestamp ON [dbo].[SecurityAudits] ([RequestTimestamp] DESC);
GO