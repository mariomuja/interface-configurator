-- Creates the Features table in InterfaceConfigDb so FeatureService can store and query feature metadata.
-- Run this script against the InterfaceConfigDb database (same server used by InterfaceConfigDbContext).

IF OBJECT_ID(N'[dbo].[Features]', N'U') IS NOT NULL
BEGIN
    PRINT 'Features table already exists.';
    RETURN;
END

CREATE TABLE [dbo].[Features] (
    [Id]                  INT              IDENTITY(1,1) PRIMARY KEY,
    [FeatureNumber]       INT              NOT NULL UNIQUE,
    [Title]               NVARCHAR(200)    NOT NULL,
    [Description]         NVARCHAR(500)    NOT NULL,
    [DetailedDescription] NVARCHAR(10000)  NOT NULL,
    [TechnicalDetails]    NVARCHAR(10000)  NULL,
    [TestInstructions]    NVARCHAR(10000)  NULL,
    [KnownIssues]         NVARCHAR(5000)   NULL,
    [Dependencies]        NVARCHAR(2000)   NULL,
    [BreakingChanges]     NVARCHAR(5000)   NULL,
    [Screenshots]         NVARCHAR(2000)   NULL,
    [Category]            NVARCHAR(100)    NOT NULL CONSTRAINT DF_Features_Category DEFAULT('General'),
    [Priority]            NVARCHAR(50)     NOT NULL CONSTRAINT DF_Features_Priority DEFAULT('Medium'),
    [IsEnabled]           BIT              NOT NULL CONSTRAINT DF_Features_IsEnabled DEFAULT(0),
    [ImplementedDate]     DATETIME2(2)     NOT NULL CONSTRAINT DF_Features_ImplementedDate DEFAULT SYSUTCDATETIME(),
    [EnabledDate]         DATETIME2(2)     NULL,
    [EnabledBy]           NVARCHAR(100)    NULL,
    [ImplementationDetails] NVARCHAR(MAX)  NULL,
    [TestComment]         NVARCHAR(5000)   NULL,
    [TestCommentBy]       NVARCHAR(100)    NULL,
    [TestCommentDate]     DATETIME2(2)     NULL
);

PRINT 'Features table created successfully.';

