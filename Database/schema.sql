-- Database schema creation script
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Roles')
BEGIN
    CREATE TABLE Roles (
        RoleID INT PRIMARY KEY IDENTITY(1,1),
        RoleName NVARCHAR(50) NOT NULL UNIQUE
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        UserID INT PRIMARY KEY IDENTITY(1,1),
        Username NVARCHAR(50) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(255) NOT NULL,
        RoleID INT NOT NULL FOREIGN KEY REFERENCES Roles(RoleID),
        FullName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(100) NOT NULL
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ClaimStatuses')
BEGIN
    CREATE TABLE ClaimStatuses (
        StatusID INT PRIMARY KEY,
        StatusName NVARCHAR(50) NOT NULL UNIQUE
    );
END;

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Claims')
BEGIN
    CREATE TABLE Claims (
        ClaimID INT PRIMARY KEY IDENTITY(1000,1), -- Starts at 1000 for realistic Claim IDs
        LoanAccountNumber NVARCHAR(50) NOT NULL,
        ClaimType NVARCHAR(100) NOT NULL,
        Description NVARCHAR(MAX) NOT NULL,
        DateSubmitted DATETIME NOT NULL DEFAULT GETDATE(),
        StatusID INT NOT NULL FOREIGN KEY REFERENCES ClaimStatuses(StatusID) DEFAULT 1,
        SubmittedByUserID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        ReviewedByUserID INT FOREIGN KEY REFERENCES Users(UserID) NULL,
        ReviewDate DATETIME NULL,
        OfficerComments NVARCHAR(MAX) NULL
    );
END;
