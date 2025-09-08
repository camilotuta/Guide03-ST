// TransactionServiceTests.cs
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Guia03.Data;
using Guia03epositories;
using Guia03ss.Services;
using Guia03odels;
using Guia03TOs;

namespace Guia03Services
{
    public class TransactionServiceTests : IDisposable
    {
        private readonly BankingDbContext _context;
        private readonly TransactionService _transactionService;
        private readonly AccountRepository _accountRepository;
        private readonly TransactionRepository _transactionRepository;
        private readonly Mock<ILogger<TransactionService>> _loggerMock;

        public TransactionServiceTests()
        {
            var options = new DbContextOptionsBuilder<BankingDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new BankingDbContext(options);
            _accountRepository = new AccountRepository(_context);
            _transactionRepository = new TransactionRepository(_context);
            _loggerMock = new Mock<ILogger<TransactionService>>();

            _transactionService = new TransactionService(
                _context,
                _accountRepository,
                _transactionRepository,
                _loggerMock.Object);

            SeedTestData();
        }

        private void SeedTestData()
        {
            var accounts = new List<Account>
            {
                new Account { AccountId = 1, AccountNumber = "TEST001", Balance = 1000m, AccountType = "Checking", IsActive = true },
                new Account { AccountId = 2, AccountNumber = "TEST002", Balance = 500m, AccountType = "Savings", IsActive = true },
                new Account { AccountId = 3, AccountNumber = "TEST003", Balance = 0m, AccountType = "Checking", IsActive = true }
            };

            _context.Accounts.AddRange(accounts);
            _context.SaveChanges();
        }

        [Fact]
        public async Task ProcessTransferAsync_ValidTransfer_ShouldSucceed()
        {
            // Arrange
            var request = new TransactionRequest
            {
                FromAccountId = 1,
                ToAccountId = 2,
                Amount = 200m,
                TransactionType = "Transfer",
                Description = "Test transfer"
            };

            // Act
            var result = await _transactionService.ProcessTransferAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.TransactionId);
            Assert.Equal(800m, result.NewBalance); // 1000 - 200
        }

        [Fact]
        public async Task ProcessTransferAsync_InsufficientFunds_ShouldFail()
        {
            // Arrange
            var request = new TransactionRequest
            {
                FromAccountId = 2, // Account with 500 balance
                ToAccountId = 1,
                Amount = 600m, // More than available
                TransactionType = "Transfer"
            };

            // Act
            var result = await _transactionService.ProcessTransferAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Insufficient funds", result.Message);
        }

        [Fact]
        public async Task ProcessTransferAsync_SameAccount_ShouldFail()
        {
            // Arrange
            var request = new TransactionRequest
            {
                FromAccountId = 1,
                ToAccountId = 1, // Same account
                Amount = 100m,
                TransactionType = "Transfer"
            };

            // Act
            var result = await _transactionService.ProcessTransferAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("same account", result.Message);
        }

        [Fact]
        public async Task ProcessDepositAsync_ValidDeposit_ShouldSucceed()
        {
            // Arrange
            var request = new TransactionRequest
            {
                ToAccountId = 3, // Account with 0 balance
                Amount = 250m,
                TransactionType = "Deposit"
            };

            // Act
            var result = await _transactionService.ProcessDepositAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(250m, result.NewBalance);
        }

        [Fact]
        public async Task ProcessWithdrawalAsync_ValidWithdrawal_ShouldSucceed()
        {
            // Arrange
            var request = new TransactionRequest
            {
                FromAccountId = 1, // Account with 1000 balance
                Amount = 150m,
                TransactionType = "Withdrawal"
            };

            // Act
            var result = await _transactionService.ProcessWithdrawalAsync(request);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(850m, result.NewBalance); // 1000 - 150
        }

        [Fact]
        public async Task ProcessMultipleTransactionsAsync_ConcurrentTransfers_ShouldMaintainConsistency()
        {
            // Arrange
            var requests = new List<TransactionRequest>
            {
                new() { FromAccountId = 1, ToAccountId = 2, Amount = 100m, TransactionType = "Transfer" },
                new() { FromAccountId = 1, ToAccountId = 3, Amount = 150m, TransactionType = "Transfer" },
                new() { FromAccountId = 2, ToAccountId = 3, Amount = 50m, TransactionType = "Transfer" }
            };

            // Act
            var result = await _transactionService.ProcessMultipleTransactionsAsync(requests);

            // Assert
            Assert.True(result);

            // Verify final balances
            var account1 = await _accountRepository.GetByIdAsync(1);
            var account2 = await _accountRepository.GetByIdAsync(2);
            var account3 = await _accountRepository.GetByIdAsync(3);

            Assert.Equal(750m, account1.Balance); // 1000 - 100 - 150
            Assert.Equal(450m, account2.Balance); // 500 + 100 - 50
            Assert.Equal(200m, account3.Balance); // 0 + 150 + 50
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-100)]
        public async Task ProcessTransferAsync_InvalidAmount_ShouldFail(decimal amount)
        {
            // Arrange
            var request = new TransactionRequest
            {
                FromAccountId = 1,
                ToAccountId = 2,
                Amount = amount,
                TransactionType = "Transfer"
            };

            // Act
            var result = await _transactionService.ProcessTransferAsync(request);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("greater than zero", result.Message);
        }

        [Fact]
        public async Task GetAccountBalanceAsync_ValidAccount_ShouldReturnBalance()
        {
            // Act
            var result = await _transactionService.GetAccountBalanceAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1000m, result.Balance);
            Assert.Equal("TEST001", result.AccountNumber);
        }

        [Fact]
        public async Task GetAccountBalanceAsync_InvalidAccount_ShouldReturnNull()
        {
            // Act
            var result = await _transactionService.GetAccountBalanceAsync(999);

            // Assert
            Assert.Null(result);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}