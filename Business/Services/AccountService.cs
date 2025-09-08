namespace BankingSystem.Business.Services
{
    public class AccountService : IAccountService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ILogger<AccountService> _logger;

        public AccountService(IAccountRepository accountRepository, ILogger<AccountService> logger)
        {
            _accountRepository = accountRepository;
            _logger = logger;
        }

        public async Task<Account> CreateAccountAsync(string accountType)
        {
            var account = new Account
            {
                AccountType = accountType,
                Balance = 0,
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            };

            return await _accountRepository.CreateAsync(account);
        }

        public async Task<Account> GetAccountAsync(int accountId)
        {
            return await _accountRepository.GetByIdAsync(accountId);
        }

        public async Task<List<Account>> GetAllAccountsAsync()
        {
            return await _accountRepository.GetAllAsync();
        }

        public async Task<bool> DeactivateAccountAsync(int accountId)
        {
            return await _accountRepository.DeleteAsync(accountId);
        }
    }
}