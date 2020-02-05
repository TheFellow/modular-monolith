CREATE TABLE [UserAccess].[UserRole]
(
	[Id] BIGINT NOT NULL PRIMARY KEY, 
    [UserId] BIGINT NOT NULL, 
    [Role] VARCHAR(64) NOT NULL, 
    [BeginOnUtc] DATETIME2 NOT NULL, 
    [EndOnUtc] DATETIME2 NOT NULL, 
    [CreatedBy] VARCHAR(32) NOT NULL CONSTRAINT [UserAccess_UserRole_CreatedBy] DEFAULT (SUSER_SNAME()),
    [CreatedOn] DATETIME2 NOT NULL CONSTRAINT [UserAccess_UserRole_CreatedOn] DEFAULT (GETDATE()),
    [UpdatedBy] VARCHAR(32) NOT NULL, 
    [UpdatedOn] DATETIME2 NOT NULL, 
    CONSTRAINT [FK_UserRole_User] FOREIGN KEY ([UserId]) REFERENCES [UserAccess].[User]([Id])
)
