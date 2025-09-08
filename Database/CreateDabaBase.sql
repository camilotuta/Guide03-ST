-- Crear base de datos
CREATE DATABASE Guia03DB;
GO

USE Guia03DB;
GO

-- Tabla de Cuentas
CREATE TABLE Accounts (
    AccountId INT PRIMARY KEY IDENTITY(1,1),
    AccountNumber NVARCHAR(20) UNIQUE NOT NULL,
    Balance DECIMAL(18,2) NOT NULL DEFAULT 0,
    AccountType NVARCHAR(20) NOT NULL,
    CreatedDate DATETIME2 DEFAULT GETUTCDATE(),
    IsActive BIT DEFAULT 1
);

-- Tabla de Transacciones
CREATE TABLE Transactions (
    TransactionId INT PRIMARY KEY IDENTITY(1,1),
    FromAccountId INT NULL,
    ToAccountId INT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    TransactionType NVARCHAR(20) NOT NULL,
    TransactionDate DATETIME2 DEFAULT GETUTCDATE(),
    Status NVARCHAR(20) DEFAULT 'Completed',
    Description NVARCHAR(500),
    FOREIGN KEY (FromAccountId) REFERENCES Accounts(AccountId),
    FOREIGN KEY (ToAccountId) REFERENCES Accounts(AccountId)
);

-- Tabla de Logs de Auditoría
CREATE TABLE AuditLogs (
    LogId INT PRIMARY KEY IDENTITY(1,1),
    TableName NVARCHAR(50) NOT NULL,
    Operation NVARCHAR(10) NOT NULL,
    RecordId INT NOT NULL,
    OldValues NVARCHAR(MAX),
    NewValues NVARCHAR(MAX),
    Timestamp DATETIME2 DEFAULT GETUTCDATE()
);

-- Índices para rendimiento
CREATE INDEX IX_Transactions_FromAccountId ON Transactions(FromAccountId);
CREATE INDEX IX_Transactions_ToAccountId ON Transactions(ToAccountId);
CREATE INDEX IX_Transactions_TransactionDate ON Transactions(TransactionDate DESC);
CREATE INDEX IX_Accounts_AccountNumber ON Accounts(AccountNumber);
CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs(Timestamp DESC);

-- Trigger para auditoría automática
CREATE TRIGGER tr_Accounts_Audit
ON Accounts
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Para INSERT
    IF EXISTS(SELECT * FROM inserted) AND NOT EXISTS(SELECT * FROM deleted)
    BEGIN
        INSERT INTO AuditLogs (TableName, Operation, RecordId, NewValues)
        SELECT 'Accounts', 'INSERT', AccountId, 
               CONCAT('AccountNumber:', AccountNumber, ',Balance:', Balance, ',AccountType:', AccountType)
        FROM inserted;
    END
    
    -- Para UPDATE
    IF EXISTS(SELECT * FROM inserted) AND EXISTS(SELECT * FROM deleted)
    BEGIN
        INSERT INTO AuditLogs (TableName, Operation, RecordId, OldValues, NewValues)
        SELECT 'Accounts', 'UPDATE', i.AccountId,
               CONCAT('AccountNumber:', d.AccountNumber, ',Balance:', d.Balance, ',AccountType:', d.AccountType),
               CONCAT('AccountNumber:', i.AccountNumber, ',Balance:', i.Balance, ',AccountType:', i.AccountType)
        FROM inserted i
        INNER JOIN deleted d ON i.AccountId = d.AccountId;
    END
    
    -- Para DELETE
    IF EXISTS(SELECT * FROM deleted) AND NOT EXISTS(SELECT * FROM inserted)
    BEGIN
        INSERT INTO AuditLogs (TableName, Operation, RecordId, OldValues)
        SELECT 'Accounts', 'DELETE', AccountId,
               CONCAT('AccountNumber:', AccountNumber, ',Balance:', Balance, ',AccountType:', AccountType)
        FROM deleted;
    END
END;
GO

-- Procedimientos almacenados para operaciones complejas
CREATE PROCEDURE sp_ProcessTransfer
    @FromAccountId INT,
    @ToAccountId INT,
    @Amount DECIMAL(18,2),
    @Description NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    
    BEGIN TRY
        DECLARE @FromBalance DECIMAL(18,2);
        DECLARE @ToBalance DECIMAL(18,2);
        
        -- Obtener saldos actuales con bloqueo
        SELECT @FromBalance = Balance 
        FROM Accounts WITH (UPDLOCK, HOLDLOCK)
        WHERE AccountId = @FromAccountId AND IsActive = 1;
        
        SELECT @ToBalance = Balance 
        FROM Accounts WITH (UPDLOCK, HOLDLOCK)
        WHERE AccountId = @ToAccountId AND IsActive = 1;
        
        -- Validaciones
        IF @FromBalance IS NULL
        BEGIN
            RAISERROR('Source account not found or inactive', 16, 1);
            RETURN;
        END
        
        IF @ToBalance IS NULL
        BEGIN
            RAISERROR('Destination account not found or inactive', 16, 1);
            RETURN;
        END
        
        IF @FromBalance < @Amount
        BEGIN
            RAISERROR('Insufficient funds', 16, 1);
            RETURN;
        END
        
        IF @Amount <= 0
        BEGIN
            RAISERROR('Amount must be greater than zero', 16, 1);
            RETURN;
        END
        
        -- Actualizar saldos
        UPDATE Accounts 
        SET Balance = Balance - @Amount 
        WHERE AccountId = @FromAccountId;
        
        UPDATE Accounts 
        SET Balance = Balance + @Amount 
        WHERE AccountId = @ToAccountId;
        
        -- Crear registro de transacción
        INSERT INTO Transactions (FromAccountId, ToAccountId, Amount, TransactionType, Description)
        VALUES (@FromAccountId, @ToAccountId, @Amount, 'Transfer', @Description);
        
        COMMIT TRANSACTION;
        
        SELECT 'SUCCESS' AS Result, SCOPE_IDENTITY() AS TransactionId;
        
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO

-- Función para obtener balance de cuenta
CREATE FUNCTION fn_GetAccountBalance(@AccountId INT)
RETURNS DECIMAL(18,2)
AS
BEGIN
    DECLARE @Balance DECIMAL(18,2);
    
    SELECT @Balance = Balance 
    FROM Accounts 
    WHERE AccountId = @AccountId AND IsActive = 1;
    
    RETURN ISNULL(@Balance, 0);
END;
GO

-- Vista para resumen de transacciones
CREATE VIEW vw_TransactionSummary
AS
SELECT 
    t.TransactionId,
    t.TransactionType,
    t.Amount,
    t.TransactionDate,
    t.Description,
    t.Status,
    fa.AccountNumber AS FromAccountNumber,
    fa.AccountType AS FromAccountType,
    ta.AccountNumber AS ToAccountNumber,
    ta.AccountType AS ToAccountType
FROM Transactions t
LEFT JOIN Accounts fa ON t.FromAccountId = fa.AccountId
LEFT JOIN Accounts ta ON t.ToAccountId = ta.AccountId;
GO

-- Datos de ejemplo
INSERT INTO Accounts (AccountNumber, Balance, AccountType) VALUES 
('ACC001', 1000.00, 'Checking'),
('ACC002', 2500.00, 'Savings'),
('ACC003', 500.00, 'Checking'),
('ACC004', 10000.00, 'Business'),
('ACC005', 750.00, 'Savings');
GO