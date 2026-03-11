CREATE TABLE AccessLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ClientId NVARCHAR(100) NOT NULL,
    ResourceKey NVARCHAR(255) NOT NULL,
    RequestTimestamp DATETIME2 DEFAULT GETUTCDATE(),
    ClientIp NVARCHAR(45),
    IsSuccess BIT NOT NULL,
    ErrorCode NVARCHAR(50),
    AuditMessage NVARCHAR(MAX)
);

CREATE INDEX IX_AccessLogs_ClientId_Timestamp ON AccessLogs(ClientId, RequestTimestamp);