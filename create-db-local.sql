USE CollegeDB;
GO

CREATE TABLE dbo.Groups
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL UNIQUE
);
GO

CREATE TABLE dbo.Students
(
    Id INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100) NOT NULL,
    GroupId INT NULL,
    CONSTRAINT FK_Students_Groups
        FOREIGN KEY (GroupId) REFERENCES dbo.Groups(Id)
);
GO

INSERT INTO dbo.Groups (Name)
VALUES (N'С-21'), (N'С-22'), (N'-31');
GO
