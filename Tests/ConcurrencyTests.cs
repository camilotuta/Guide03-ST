// ConcurrencyTests.cs - Pruebas de threading y concurrencia
namespace BankingSystem.Tests.Concurrency
{
    public class ConcurrencyTests : IDisposable
    {
        private readonly BankingDbContext _context;
        private readonly TransactionService _transactionService;

        public ConcurrencyTests()
        {
            var options = new DbContextOptionsBuilder<BankingDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new BankingDbContext(options);
            var accountRepo = new AccountRepository(_context);
            var transactionRepo = new TransactionRepository(_context);
            var logger = new Mock<ILogger<TransactionService>>().Object;

            _transactionService = new TransactionService(_context, accountRepo, transactionRepo, logger);

            SeedTestData();
        }

        private void SeedTestData()
        {
            var accounts = new List<Account>();
            for (int i = 1; i <= 10; i++)
            {
                accounts.Add(new Account
                {
                    AccountId = i,
                    AccountNumber = $"CONC{i:D3}",
                    Balance = 1000m,
                    AccountType = "Test",
                    IsActive = true
                });
            }

            _context.Accounts.AddRange(accounts);
            _context.SaveChanges();
        }

        [Fact]
        public async Task HighConcurrency_ManySimultaneousTransfers_ShouldMaintainConsistency()
        {
            // Arrange
            var random = new Random();
            var tasks = new List<Task<TransactionResponse>>();
            var expectedTotalBalance = 10000m; // 10 accounts * 1000 each

            // Crear 100 transferencias aleatorias concurrentes
            for (int i = 0; i < 100; i++)
            {
                var fromAccount = random.Next(1, 11);
                var toAccount = random.Next(1, 11);

                if (fromAccount != toAccount)
                {
                    var request = new TransactionRequest
                    {
                        FromAccountId = fromAccount,
                        ToAccountId = toAccount,
                        Amount = random.Next(1, 100),
                        TransactionType = "Transfer"
                    };

                    tasks.Add(_transactionService.ProcessTransferAsync(request));
                }
            }

            // Act
            var results = await Task.WhenAll(tasks);

            // Assert
            var successfulTransfers = results.Count(r => r.Success);

            // Verificar que el balance total se mantiene
            var accounts = await _context.Accounts.ToListAsync();
            var actualTotalBalance = accounts.Sum(a => a.Balance);

            Assert.Equal(expectedTotalBalance, actualTotalBalance);
            Assert.True(successfulTransfers > 0); // Al menos algunas transferencias deben ser exitosas
        }

        [Fact]
        public async Task ThreadSafety_ParallelDepositsToSameAccount_ShouldBeAccurate()
        {
            // Arrange
            const int numberOfDeposits = 50;
            const decimal depositAmount = 10m;
            const int targetAccountId = 1;

            var initialBalance = (await _context.Accounts.FindAsync(targetAccountId)).Balance;
            var tasks = new List<Task<TransactionResponse>>();

            // Act - Crear múltiples depósitos simultáneos a la misma cuenta
            for (int i = 0; i < numberOfDeposits; i++)
            {
                var request = new TransactionRequest
                {
                    ToAccountId = targetAccountId,
                    Amount = depositAmount,
                    TransactionType = "Deposit"
                };

                tasks.Add(_transactionService.ProcessDepositAsync(request));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            var successfulDeposits = results.Count(r => r.Success);
            var finalBalance = (await _context.Accounts.FindAsync(targetAccountId)).Balance;
            var expectedFinalBalance = initialBalance + (successfulDeposits * depositAmount);

            Assert.Equal(expectedFinalBalance, finalBalance);
            Assert.Equal(numberOfDeposits, successfulDeposits); // Todos los depósitos deben ser exitosos
        }

        [Fact]
        public async Task DeadlockPrevention_CircularTransfers_ShouldComplete()
        {
            // Arrange - Crear transferencias que podrían causar deadlock
            var task1 = _transactionService.ProcessTransferAsync(new TransactionRequest
            {
                FromAccountId = 1,
                ToAccountId = 2,
                Amount = 100m,
                TransactionType = "Transfer"
            });

            var task2 = _transactionService.ProcessTransferAsync(new TransactionRequest
            {
                FromAccountId = 2,
                ToAccountId = 1,
                Amount = 50m,
                TransactionType = "Transfer"
            });

            // Act
            var results = await Task.WhenAll(task1, task2);

            // Assert - Al menos una transferencia debe completarse sin deadlock
            Assert.True(results.Any(r => r.Success));

            // Verificar que no hay deadlock esperando un tiempo razonable
            var completed = Task.WhenAll(task1, task2).Wait(TimeSpan.FromSeconds(5));
            Assert.True(completed, "Operations should complete without deadlock");
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}