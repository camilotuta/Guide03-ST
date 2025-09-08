namespace BankingSystem.Tests.ACID
{
    public class ACIDPropertiesTests : IDisposable
    {
        private readonly BankingDbContext _context;
        private readonly TransactionService _transactionService;
        private readonly AccountRepository _accountRepository;
        private readonly TransactionRepository _transactionRepository;

        public ACIDPropertiesTests()
        {
            var options = new DbContextOptionsBuilder<BankingDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new BankingDbContext(options);
            _accountRepository = new AccountRepository(_context);
            _transactionRepository = new TransactionRepository(_context);
            var logger = new Mock<ILogger<TransactionService>>().Object;

            _transactionService = new TransactionService(_context, _accountRepository, _transactionRepository, logger);

            SeedTestData();
        }

        private void SeedTestData()
        {
            var accounts = new List<Account>
            {
                new() { AccountId = 1, AccountNumber = "ACID001", Balance = 1000m, AccountType = "Test", IsActive = true },
                new() { AccountId = 2, AccountNumber = "ACID002", Balance = 1000m, AccountType = "Test", IsActive = true }
            };

            _context.Accounts.AddRange(accounts);
            _context.SaveChanges();
        }

        [Fact]
        public async Task Atomicity_FailedTransfer_ShouldRollbackAllChanges()
        {
            // Arrange - Simular fallo durante transacción
            var initialBalance1 = (await _accountRepository.GetByIdAsync(1)).Balance;
            var initialBalance2 = (await _accountRepository.GetByIdAsync(2)).Balance;

            var request = new TransactionRequest
            {
                FromAccountId = 1,
                ToAccountId = 999, // Account que no existe
                Amount = 100m,
                TransactionType = "Transfer"
            };

            // Act
            var result = await _transactionService.ProcessTransferAsync(request);

            // Assert - Verificar que no hay cambios
            Assert.False(result.Success);

            var finalBalance1 = (await _accountRepository.GetByIdAsync(1)).Balance;
            var finalBalance2 = (await _accountRepository.GetByIdAsync(2)).Balance;

            Assert.Equal(initialBalance1, finalBalance1);
            Assert.Equal(initialBalance2, finalBalance2);
        }

        [Fact]
        public async Task Consistency_Transfer_ShouldMaintainTotalBalance()
        {
            // Arrange
            var initialTotal = (await _accountRepository.GetByIdAsync(1)).Balance +
                              (await _accountRepository.GetByIdAsync(2)).Balance;

            var request = new TransactionRequest
            {
                FromAccountId = 1,
                ToAccountId = 2,
                Amount = 300m,
                TransactionType = "Transfer"
            };

            // Act
            var result = await _transactionService.ProcessTransferAsync(request);

            // Assert
            Assert.True(result.Success);

            var finalTotal = (await _accountRepository.GetByIdAsync(1)).Balance +
                            (await _accountRepository.GetByIdAsync(2)).Balance;

            Assert.Equal(initialTotal, finalTotal);
        }

        [Fact]
        public async Task Isolation_ConcurrentTransfers_ShouldNotInterfere()
        {
            // Arrange
            var tasks = new List<Task<TransactionResponse>>();

            // Crear múltiples transferencias concurrentes
            for (int i = 0; i < 10; i++)
            {
                var request = new TransactionRequest
                {
                    FromAccountId = 1,
                    ToAccountId = 2,
                    Amount = 50m,
                    TransactionType = "Transfer"
                };

                tasks.Add(_transactionService.ProcessTransferAsync(request));
            }

            // Act
            var results = await Task.WhenAll(tasks);

            // Assert
            var successfulTransfers = results.Count(r => r.Success);
            var failedTransfers = results.Count(r => !r.Success);

            // Verificar que algunas transferencias fallan por fondos insuficientes
            // pero las exitosas mantienen la consistencia
            Assert.True(successfulTransfers > 0);

            var finalBalance1 = (await _accountRepository.GetByIdAsync(1)).Balance;
            var finalBalance2 = (await _accountRepository.GetByIdAsync(2)).Balance;

            // Balance total debe mantenerse
            Assert.Equal(2000m, finalBalance1 + finalBalance2);

            // La cuenta 1 debe haber disminuido exactamente por las transferencias exitosas
            Assert.Equal(1000m - (successfulTransfers * 50m), finalBalance1);
        }

        [Fact]
        public async Task Durability_CompletedTransfer_ShouldPersistAfterRestart()
        {
            // Arrange
            var request = new TransactionRequest
            {
                FromAccountId = 1,
                ToAccountId = 2,
                Amount = 200m,
                TransactionType = "Transfer"
            };

            // Act
            var result = await _transactionService.ProcessTransferAsync(request);
            Assert.True(result.Success);

            // Simular "reinicio" creando nuevo contexto
            using var newContext = new BankingDbContext(
                new DbContextOptionsBuilder<BankingDbContext>()
                    .UseInMemoryDatabase(databaseName: _context.Database.GetDbConnection().Database)
                    .Options);

            // Assert - Los cambios deben persistir
            var account1 = await newContext.Accounts.FindAsync(1);
            var account2 = await newContext.Accounts.FindAsync(2);

            Assert.Equal(800m, account1.Balance);
            Assert.Equal(1200m, account2.Balance);

            // Verificar que existe el registro de transacción
            var transaction = await newContext.Transactions
                .FirstOrDefaultAsync(t => t.TransactionId.ToString() == result.TransactionId);
            Assert.NotNull(transaction);
            Assert.Equal(200m, transaction.Amount);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}