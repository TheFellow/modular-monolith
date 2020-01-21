CREATE TABLE [UserAccess].[UserRegistration] (
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
    [StatusDate]       DATE        CONSTRAINT [UserAccess_Userregistration_StatusDate] DEFAULT CAST(getdate() AS DATE) NOT NULL,
    [CreatedBy]        VARCHAR (32)     CONSTRAINT [UserAccess_UserRegistration_CreatedBy] DEFAULT (suser_sname()) NOT NULL,
    [CreatedOn]        DATETIME2         CONSTRAINT [UserAccess_UserRegistration_CreatedOn] DEFAULT (getdate()) NOT NULL,
    [UpdatedBy]        VARCHAR (32)     CONSTRAINT [UserAccess_UserRegistration_UpdatedBy] DEFAULT (suser_sname()) NOT NULL,
    [UpdatedOn]        DATETIME2         CONSTRAINT [UserAccess_UserRegistration_UpdatedOn] DEFAULT (getdate()) NOT NULL,
    CONSTRAINT [UserAccess_UserRegistration_Id] PRIMARY KEY CLUSTERED ([Id] ASC)
);

