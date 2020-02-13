CREATE VIEW [UserAccess].[vUser]
AS
SELECT	u.[Id],
		u.[Login],
		u.[FirstName],
		u.[LastName],
		ue.[Email],
		up.[PasswordHash],
		up.[PasswordSalt]
FROM [UserAccess].[User] u
INNER JOIN [UserAccess].[UserEmail] ue
	ON u.[Id] = ue.[UserId]
INNER JOIN [UserAccess].[UserPassword] up
	ON u.[Id] = up.[UserId]
WHERE ue.[Status] = 'Active'
	AND up.[EndOnUtc] IS NULL