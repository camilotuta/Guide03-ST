using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BankingSystem.Core.Interfaces;
using BankingSystem.Core.Models;
using BankingSystem.Core.DTOs;
using BankingSystem.Data;
using BankingSystem.Data.Repositories;

namespace BankingSystem.Business.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly BankingDbContext _context;
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ILogger<TransactionService> _logger;
        private readonly SemaphoreSlim _semaphore;

        public TransactionService(
            BankingDbContext context,
            IAccountRepository accountRepository,
            ITransactionRepository transactionRepository,
            ILogger<TransactionService> logger)
        {
            _context = context;
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _logger = logger;
            _semaphore = new SemaphoreSlim(5, 5); // Máximo 5 transacciones concurrentes
        }

        public async Task<TransactionResponse> ProcessTransferAsync(TransactionRequest request)
        {
            await _semaphore.WaitAsync();
            try
            {
                return await ProcessTransferInternalAsync(request);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<TransactionResponse> ProcessTransferInternalAsync(TransactionRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validaciones
                if (request.FromAccountId == request.ToAccountId)
                {
                    return new TransactionResponse
                    {
                        Success = false,
                        Message = "Cannot transfer to the same account"
                    };
                }

                if (request.Amount <= 0)
                {
                    return new TransactionResponse
                    {
                        Success = false,
                        Message = "Amount must be greater than zero"
                    };
                }

                // Obtener cuentas con bloqueo para evitar condiciones de carrera
                var fromAccount = await _context.Accounts
                    .Where(a => a.AccountId == request.FromAccountId && a.IsActive)
                    .FirstOrDefaultAsync();

                var toAccount = await _context.Accounts
                    .Where(a => a.AccountId == request.ToAccountId && a.IsActive)
                    .FirstOrDefaultAsync();

                if (fromAccount == null)
                {
                    return new TransactionResponse
                    {
                        Success = false,
                        Message = "Source account not found"
                    };
                }

                if (toAccount == null)
                {
                    return new TransactionResponse
                    {
                        Success = false,
                        Message = "Destination account not found"
                    };
                }

                // Verificar saldo suficiente
                if (fromAccount.Balance < request.Amount)
                {
                    return new TransactionResponse
                    {
                        Success = false,
                        Message = "Insufficient funds"
                    };
                }

                // Procesar transferencia - ATOMICIDAD
                fromAccount.Balance -= request.Amount;
                toAccount.Balance += request.Amount;

                // Crear registro de transacción
                var transactionRecord = new Transaction
                {
                    FromAccountId = request.FromAccountId,
                    ToAccountId = request.ToAccountId,
                    Amount = request.Amount,
                    TransactionType = "Transfer",
                    Description = request.Description ?? "Transfer between accounts",
                    TransactionDate = DateTime.UtcNow,
                    Status = "Completed"
                };

                _context.Transactions.Add(transactionRecord);

                // Crear log de auditoría
                var auditLog = new AuditLog
                {
                    TableName = "Transactions",
                    Operation = "INSERT",
                    RecordId = transactionRecord.TransactionId,
                    NewValues = $"Transfer: {request.Amount} from {fromAccount.AccountNumber} to {toAccount.AccountNumber}",
                    Timestamp = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);

                // Guardar cambios
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation($"Transfer completed: {request.Amount} from account {fromAccount.AccountNumber} to {toAccount.AccountNumber}");

                return new TransactionResponse
                {
                    Success = true,
                    TransactionId = transactionRecord.TransactionId.ToString(),
                    Message = "Transfer completed successfully",
                    NewBalance = fromAccount.Balance
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing transfer");

                return new TransactionResponse
                {
                    Success = false,
                    Message = "Transfer failed due to system error"
                };
            }
        }

        public async Task<TransactionResponse> ProcessDepositAsync(TransactionRequest request)
        {
            await _semaphore.WaitAsync();
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    if (request.Amount <= 0)
                    {
                        return new TransactionResponse
                        {
                            Success = false,
                            Message = "Amount must be greater than zero"
                        };
                    }

                    var account = await _accountRepository.GetByIdAsync(request.ToAccountId.Value);
                    if (account == null)
                    {
                        return new TransactionResponse
                        {
                            Success = false,
                            Message = "Account not found"
                        };
                    }

                    // Procesar depósito
                    account.Balance += request.Amount;
                    await _accountRepository.UpdateAsync(account);

                    // Crear registro de transacción
                    var transactionRecord = new Transaction
                    {
                        ToAccountId = request.ToAccountId,
                        Amount = request.Amount,
                        TransactionType = "Deposit",
                        Description = request.Description ?? "Account deposit",
                        TransactionDate = DateTime.UtcNow,
                        Status = "Completed"
                    };

                    await _transactionRepository.CreateAsync(transactionRecord);
                    await transaction.CommitAsync();

                    return new TransactionResponse
                    {
                        Success = true,
                        TransactionId = transactionRecord.TransactionId.ToString(),
                        Message = "Deposit completed successfully",
                        NewBalance = account.Balance
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error processing deposit");
                    throw;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<TransactionResponse> ProcessWithdrawalAsync(TransactionRequest request)
        {
            await _semaphore.WaitAsync();
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    if (request.Amount <= 0)
                    {
                        return new TransactionResponse
                        {
                            Success = false,
                            Message = "Amount must be greater than zero"
                        };
                    }

                    var account = await _accountRepository.GetByIdAsync(request.FromAccountId.Value);
                    if (account == null)
                    {
                        return new TransactionResponse
                        {
                            Success = false,
                            Message = "Account not found"
                        };
                    }

                    if (account.Balance < request.Amount)
                    {
                        return new TransactionResponse
                        {
                            Success = false,
                            Message = "Insufficient funds"
                        };
                    }

                    // Procesar retiro
                    account.Balance -= request.Amount;
                    await _accountRepository.UpdateAsync(account);

                    // Crear registro de transacción
                    var transactionRecord = new Transaction
                    {
                        FromAccountId = request.FromAccountId,
                        Amount = request.Amount,
                        TransactionType = "Withdrawal",
                        Description = request.Description ?? "Account withdrawal",
                        TransactionDate = DateTime.UtcNow,
                        Status = "Completed"
                    };

                    await _transactionRepository.CreateAsync(transactionRecord);
                    await transaction.CommitAsync();

                    return new TransactionResponse
                    {
                        Success = true,
                        TransactionId = transactionRecord.TransactionId.ToString(),
                        Message = "Withdrawal completed successfully",
                        NewBalance = account.Balance
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error processing withdrawal");
                    throw;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<AccountBalance> GetAccountBalanceAsync(int accountId)
        {
            var account = await _accountRepository.GetByIdAsync(accountId);
            if (account == null) return null;

            return new AccountBalance
            {
                AccountId = account.AccountId,
                AccountNumber = account.AccountNumber,
                Balance = account.Balance,
                AccountType = account.AccountType
            };
        }

        public async Task<List<Transaction>> GetAccountTransactionsAsync(int accountId)
        {
            return await _transactionRepository.GetByAccountIdAsync(accountId);
        }

        // Procesamiento concurrente de múltiples transacciones
        public async Task<bool> ProcessMultipleTransactionsAsync(List<TransactionRequest> requests)
        {
            var tasks = requests.Select(request => Task.Run(async () =>
            {
                switch (request.TransactionType?.ToLower())
                {
                    case "transfer":
                        return await ProcessTransferAsync(request);
                    case "deposit":
                        return await ProcessDepositAsync(request);
                    case "withdrawal":
                        return await ProcessWithdrawalAsync(request);
                    default:
                        return new TransactionResponse { Success = false, Message = "Invalid transaction type" };
                }
            }));

            var results = await Task.WhenAll(tasks);
            return results.All(r => r.Success);
        }
    }
}