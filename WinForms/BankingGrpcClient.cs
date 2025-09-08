

// BankingGrpcClient.cs
using Grpc.Net.Client;
using BankingSystem.gRPC;

namespace BankingSystem.WinForms
{
    public class BankingGrpcClient
    {
        private readonly Banking.BankingClient _client;

        public BankingGrpcClient(string serverAddress)
        {
            var channel = GrpcChannel.ForAddress(serverAddress);
            _client = new Banking.BankingClient(channel);
        }

        public async Task<TransferResponse> ProcessTransferAsync(int fromAccountId, int toAccountId, decimal amount, string description)
        {
            var request = new TransferRequest
            {
                FromAccountId = fromAccountId,
                ToAccountId = toAccountId,
                Amount = (double)amount,
                Description = description ?? ""
            };

            return await _client.ProcessTransferAsync(request);
        }

        public async Task<DepositResponse> ProcessDepositAsync(int accountId, decimal amount, string description)
        {
            var request = new DepositRequest
            {
                AccountId = accountId,
                Amount = (double)amount,
                Description = description ?? ""
            };

            return await _client.ProcessDepositAsync(request);
        }

        public async Task<WithdrawalResponse> ProcessWithdrawalAsync(int accountId, decimal amount, string description)
        {
            var request = new WithdrawalRequest
            {
                AccountId = accountId,
                Amount = (double)amount,
                Description = description ?? ""
            };

            return await _client.ProcessWithdrawalAsync(request);
        }

        public async Task<BalanceResponse> GetBalanceAsync(int accountId)
        {
            var request = new BalanceRequest
            {
                AccountId = accountId
            };

            return await _client.GetBalanceAsync(request);
        }

        public async Task<TransactionHistoryResponse> GetTransactionHistoryAsync(int accountId, int limit = 50)
        {
            var request = new TransactionHistoryRequest
            {
                AccountId = accountId,
                Limit = limit
            };

            return await _client.GetTransactionHistoryAsync(request);
        }
    }
}