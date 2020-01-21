CREATE TABLE [UserAccess].[UserLogin]
(
	[Id]				BIGINT				NOT NULL,
	[UserId]			BIGINT				NOT NULL,
	[PasswordHash]		VARCHAR(512)		NOT NULL,
    [PasswordSalt]		VARCHAR(128)		NOT NULL,
	[BeginOnUtc]		DATETIME2			NOT NULL,
	[EndOnUtc]			DATETIME2			NULL,
    [CreatedBy]        VARCHAR (32)     CONSTRAINT [UserAccess_UserLogin_CreatedBy] DEFAULT (SUSER_SNAME()) NOT NULL,
    [CreatedOn]        DATETIME2        CONSTRAINT [UserAccess_UserLogin_CreatedOn] DEFAULT (GETDATE()) NOT NULL,
    [UpdatedBy]        VARCHAR (32)     CONSTRAINT [UserAccess_UserLogin_UpdatedBy] DEFAULT (SUSER_SNAME()) NOT NULL,
    [UpdatedOn]        DATETIME2        CONSTRAINT [UserAccess_UserLogin_UpdatedOn] DEFAULT (GETDATE()) NOT NULL,
	CONSTRAINT [UserAccess_UserLogin_Id] PRIMARY KEY CLUSTERED ([Id] ASC), 
    CONSTRAINT [FK_UserLogin_User] FOREIGN KEY ([UserId]) REFERENCES [UserAccess].[User]([Id])
		ON DELETE CASCADE
		ON UPDATE CASCADE
)
