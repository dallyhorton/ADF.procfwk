IF OBJECT_ID(N'[dbo].[ExecutionLogBackup]') IS NOT NULL DROP TABLE [dbo].[ExecutionLogBackup];

IF OBJECT_ID(N'[procfwk].[ExecutionLog]') IS NOT NULL
	SELECT 
		*
	INTO
		[dbo].[ExecutionLogBackup]
	FROM
		[procfwk].[ExecutionLog];