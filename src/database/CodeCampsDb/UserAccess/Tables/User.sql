CREATE TABLE [UserAccess].[User]
(
	[Id]				BIGINT				NOT NULL,
	[RowVersion]		ROWVERSION			NOT NULL,
	[UserId]			UNIQUEIDENTIFIER		NOT NULL,
	[RegistrationId]			UNIQUEIDENTIFIER		NULL,
	[Login]				NVARCHAR(64)		NOT NULL,
	[FirstName]			NVARCHAR(32)		NOT NULL,
    [LastName]			NVARCHAR(64)		NOT NULL,
	[CreatedBy]			VARCHAR(32)			CONSTRAINT [UserAccess_User_CreatedBy] DEFAULT (suser_sname()) NOT NULL,
    [CreatedOn]         DATETIME2			CONSTRAINT [UserAccess_User_CreatedOn] DEFAULT (getdate()) NOT NULL,
    [UpdatedBy]         VARCHAR(32)			CONSTRAINT [UserAccess_User_UpdatedBy] DEFAULT (suser_sname()) NOT NULL,
    [UpdatedOn]         DATETIME2			CONSTRAINT [UserAccess_User_UpdatedOn] DEFAULT (getdate()) NOT NULL,
	CONSTRAINT [UserAccess_User_Id] PRIMARY KEY CLUSTERED ([Id] ASC)
)
