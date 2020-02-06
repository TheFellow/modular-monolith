




CREATE VIEW [UserAccess].[vLogin]
AS
SELECT	u.[Login],
		ue.[Email]
	FROM [UserAccess].[User] u
	INNER JOIN [UserAccess].[UserEmail] ue
		ON u.[Id] = ue.[UserId]
UNION
SELECT	r.[Login],
		r.[Email]
	FROM [UserAccess].[Registration] r
	WHERE r.[Status] <> 'Expired'