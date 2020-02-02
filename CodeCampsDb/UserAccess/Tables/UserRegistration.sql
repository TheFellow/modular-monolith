CREATE TABLE [UserAccess].[Registration] (
    [Id]               BIGINT           NOT NULL,
    [RowVersion] ROWVERSION NOT NULL, 
    [RegistrationId]   UNIQUEIDENTIFIER NOT NULL,
    [Login]            NVARCHAR (64)    NOT NULL,
    [ConfirmationCode] VARCHAR (16)     NOT NULL,
    [ExpiryDate]       DATE         NOT NULL,
    [PasswordHash]     VARCHAR(512)   NOT NULL,
    [PasswordSalt]     VARCHAR(128)   NOT NULL,
    [FirstName]        NVARCHAR (32)    NOT NULL,
    [LastName]         NVARCHAR (64)    NOT NULL,
    [Email]            NVARCHAR (128)   NOT NULL,
    [Status]           VARCHAR (32)     NOT NULL,
    [StatusDate]       DATE             CONSTRAINT [UserAccess_Registration_StatusDate] DEFAULT (CAST(GETDATE() AS DATE)) NOT NULL,
    [CreatedBy]        VARCHAR (32)     CONSTRAINT [UserAccess_Registration_CreatedBy] DEFAULT (SUSER_SNAME()) NOT NULL,
    [CreatedOn]        DATETIME2        CONSTRAINT [UserAccess_Registration_CreatedOn] DEFAULT (GETDATE()) NOT NULL,
    [UpdatedBy]        VARCHAR (32)     CONSTRAINT [UserAccess_Registration_UpdatedBy] DEFAULT (SUSER_SNAME()) NOT NULL,
    [UpdatedOn]        DATETIME2        CONSTRAINT [UserAccess_Registration_UpdatedOn] DEFAULT (GETDATE()) NOT NULL,
    CONSTRAINT [UserAccess_Registration_Id] PRIMARY KEY CLUSTERED ([Id] ASC)
);

