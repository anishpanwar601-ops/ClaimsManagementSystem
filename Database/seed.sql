-- Seed Roles
IF NOT EXISTS (SELECT * FROM Roles WHERE RoleName = 'Admin')
    INSERT INTO Roles (RoleName) VALUES ('Admin');
IF NOT EXISTS (SELECT * FROM Roles WHERE RoleName = 'LoanOfficer')
    INSERT INTO Roles (RoleName) VALUES ('LoanOfficer');
IF NOT EXISTS (SELECT * FROM Roles WHERE RoleName = 'Customer')
    INSERT INTO Roles (RoleName) VALUES ('Customer');

-- Seed Claim Statuses
IF NOT EXISTS (SELECT * FROM ClaimStatuses WHERE StatusID = 1)
    INSERT INTO ClaimStatuses (StatusID, StatusName) VALUES (1, 'Pending');
IF NOT EXISTS (SELECT * FROM ClaimStatuses WHERE StatusID = 2)
    INSERT INTO ClaimStatuses (StatusID, StatusName) VALUES (2, 'Approved');
IF NOT EXISTS (SELECT * FROM ClaimStatuses WHERE StatusID = 3)
    INSERT INTO ClaimStatuses (StatusID, StatusName) VALUES (3, 'Rejected');
