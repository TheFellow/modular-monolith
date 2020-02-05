CREATE TABLE [UserAccess].[UserPassword]
(
	[Id]				BIGINT				NOT NULL,
	[UserId]			BIGINT				NOT NULL,
	[PasswordHash]		VARCHAR(512)		NOT NULL,
    [PasswordSalt]		VARCHAR(128)		NOT NULL,
	[BeginOnUtc]		DATETIME2			NOT NULL,
	[EndOnUtc]			DATETIME2			NULL,
    [CreatedBy]        VARCHAR (32)     CONSTRAINT [UserAccess_UserPassword_CreatedBy] DEFAULT (SUSER_SNAME()) NOT NULL,
    [CreatedOn]        DATETIME2        CONSTRAINT [UserAccess_UserPassword_CreatedOn] DEFAULT (GETDATE()) NOT NULL,
    [UpdatedBy]        VARCHAR (32)     NOT NULL,
    [UpdatedOn]        DATETIME2        NOT NULL,
	CONSTRAINT [UserAccess_UserPassword_Id] PRIMARY KEY CLUSTERED ([Id] ASC), 
    CONSTRAINT [FK_UserPassword_User] FOREIGN KEY ([UserId]) REFERENCES [UserAccess].[User]([Id])
		ON DELETE CASCADE
		ON UPDATE CASCADE
)
