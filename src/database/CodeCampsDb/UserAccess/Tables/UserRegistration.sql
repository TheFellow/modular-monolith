CREATE TABLE [UserAccess].[UserRegistration] (
    [Id]               BIGINT           NOT NULL,
    [RegistrationId]   UNIQUEIDENTIFIER NOT NULL,
    [Login]            NVARCHAR (64)    NOT NULL,
    [ConfirmationCode] VARCHAR (16)     NOT NULL,
    [ExpiryDate]       DATETIME         NOT NULL,
    [PasswordHash]     NVARCHAR (512)   NOT NULL,
    [PasswordSalt]     NVARCHAR (128)   NOT NULL,
    [FirstName]        NVARCHAR (32)    NOT NULL,
    [LastName]         NVARCHAR (64)    NOT NULL,
    [Email]            NVARCHAR (128)   NOT NULL,
    [Status]           VARCHAR (32)     NOT NULL,
    [CreatedBy]        VARCHAR (32)     CONSTRAINT [UserAccess_UserRegistration_CreatedBy] DEFAULT (suser_sname()) NOT NULL,
    [CreatedOn]        DATETIME2         CONSTRAINT [UserAccess_UserRegistration_CreatedOn] DEFAULT (getdate()) NOT NULL,
    [UpdatedBy]        VARCHAR (32)     CONSTRAINT [UserAccess_UserRegistration_UpdatedBy] DEFAULT (suser_sname()) NOT NULL,
    [UpdatedOn]        DATETIME2         CONSTRAINT [UserAccess_UserRegistration_UpdatedOn] DEFAULT (getdate()) NOT NULL,
    [RowVersion] ROWVERSION NOT NULL, 
    CONSTRAINT [UserAccess_UserRegistration_Id] PRIMARY KEY CLUSTERED ([Id] ASC)
);

