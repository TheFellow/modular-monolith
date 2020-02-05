CREATE TABLE [UserAccess].[UserEmail]
(
	[Id]               BIGINT           NOT NULL,
    [UserId]           BIGINT           NOT NULL, 
	[Email]            NVARCHAR (128)   NOT NULL,
	[Status]           VARCHAR (32)     NOT NULL,
    [StatusDate]       DATE             CONSTRAINT [UserAccess_UserEmail_StatusDate] DEFAULT (CAST(GETDATE() AS DATE)) NOT NULL,
    [CreatedBy]        VARCHAR (32)     CONSTRAINT [UserAccess_UserEmail_CreatedBy] DEFAULT (SUSER_SNAME()) NOT NULL,
    [CreatedOn]        DATETIME2        CONSTRAINT [UserAccess_UserEmail_CreatedOn] DEFAULT (GETDATE()) NOT NULL,
    [UpdatedBy]        VARCHAR (32)     NOT NULL,
    [UpdatedOn]        DATETIME2        NOT NULL,
    CONSTRAINT [UserAccess_UserEmail_Id] PRIMARY KEY CLUSTERED ([Id] ASC), 
    CONSTRAINT [FK_UserEmail_User] FOREIGN KEY ([UserId]) REFERENCES [UserAccess].[User]([Id])
        ON DELETE CASCADE
		ON UPDATE CASCADE
)
