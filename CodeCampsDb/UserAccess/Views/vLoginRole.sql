CREATE VIEW [UserAccess].[vLoginRole]
AS
SELECT	u.[Login],
		ur.[Role]
	FROM [UserAccess].[User] u
	INNER JOIN [UserAccess].[UserRole] ur
		ON u.[Id] = ur.[UserId]
	WHERE ur.EndOnUtc IS NULL
