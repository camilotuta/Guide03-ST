namespace BankingSystem.Core.Interfaces
{
    public interface ITransactionService
    {
        Task<TransactionResponse> ProcessTransferAsync(TransactionRequest request);
        Task<TransactionResponse> ProcessDepositAsync(TransactionRequest request);
        Task<TransactionResponse> ProcessWithdrawalAsync(TransactionRequest request);
        Task<AccountBalance> GetAccountBalanceAsync(int accountId);
        Task<List<Transaction>> GetAccountTransactionsAsync(int accountId);
        Task<bool> ProcessMultipleTransactionsAsync(List<TransactionRequest> requests);
    }

    public interface IAccountService
    {
        Task<Account> CreateAccountAsync(string accountType);
        Task<Account> GetAccountAsync(int accountId);
        Task<List<Account>> GetAllAccountsAsync();
        Task<bool> DeactivateAccountAsync(int accountId);
    }
}