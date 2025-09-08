using Microsoft.EntityFrameworkCore;
using BankingSystem.Core.Models;

namespace BankingSystem.Data.Repositories
{
    public interface IAccountRepository
    {
        Task<Account> GetByIdAsync(int id);
        Task<Account> GetByAccountNumberAsync(string accountNumber);
        Task<List<Account>> GetAllAsync();
        Task<Account> CreateAsync(Account account);
        Task<Account> UpdateAsync(Account account);
        Task<bool> DeleteAsync(int id);
    }

    public class AccountRepository : IAccountRepository
    {
        private readonly BankingDbContext _context;

        public AccountRepository(BankingDbContext context)
        {
            _context = context;
        }

        public async Task<Account> GetByIdAsync(int id)
        {
            return await _context.Accounts
                .Include(a => a.FromTransactions)
                .Include(a => a.ToTransactions)
                .FirstOrDefaultAsync(a => a.AccountId == id && a.IsActive);
        }

        public async Task<Account> GetByAccountNumberAsync(string accountNumber)
        {
            return await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber && a.IsActive);
        }

        public async Task<List<Account>> GetAllAsync()
        {
            return await _context.Accounts
                .Where(a => a.IsActive)
                .ToListAsync();
        }

        public async Task<Account> CreateAsync(Account account)
        {
            account.AccountNumber = GenerateAccountNumber();
            account.CreatedDate = DateTime.UtcNow;
            account.IsActive = true;

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();
            return account;
        }

        public async Task<Account> UpdateAsync(Account account)
        {
            _context.Accounts.Update(account);
            await _context.SaveChangesAsync();
            return account;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var account = await GetByIdAsync(id);
            if (account != null)
            {
                account.IsActive = false;
                await UpdateAsync(account);
                return true;
            }
            return false;
        }

        private string GenerateAccountNumber()
        {
            return $"ACC{DateTime.UtcNow.Ticks.ToString().Substring(10)}";
        }
    }
}