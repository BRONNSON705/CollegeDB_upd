IF DB_ID(N'CollegeDB') IS NULL
    CREATE DATABASE CollegeDB;
GO

USE CollegeDB;
GO

IF OBJECT_ID(N'dbo.Groups', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Groups
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL UNIQUE
    );
END
GO

IF OBJECT_ID(N'dbo.Students', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Students
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FullName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(100) NOT NULL,
        GroupId INT NULL,
        CONSTRAINT FK_Students_Groups
            FOREIGN KEY (GroupId) REFERENCES dbo.Groups(Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Groups)
BEGIN
    INSERT INTO dbo.Groups (Name)
    VALUES (N'ИС-21'), (N'ИС-22'), (N'ПР-31');
END
GO
