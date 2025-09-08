using Grpc.Core;
using BankingSystem.Core.Interfaces;
using BankingSystem.Core.DTOs;

namespace BankingSystem.gRPC.Services
{
    public class BankingGrpcService : Banking.BankingBase
    {
        private readonly ITransactionService _transactionService;
        private readonly ILogger<BankingGrpcService> _logger;

        public BankingGrpcService(ITransactionService transactionService, ILogger<BankingGrpcService> logger)
        {
            _transactionService = transactionService;
            _logger = logger;
        }

        public override async Task<TransferResponse> ProcessTransfer(TransferRequest request, ServerCallContext context)
        {
            try
            {
                var transactionRequest = new TransactionRequest
                {
                    FromAccountId = request.FromAccountId,
                    ToAccountId = request.ToAccountId,
                    Amount = (decimal)request.Amount,
                    TransactionType = "Transfer",
                    Description = request.Description
                };

                var result = await _transactionService.ProcessTransferAsync(transactionRequest);

                return new TransferResponse
                {
                    Success = result.Success,
                    TransactionId = result.TransactionId ?? "",
                    Message = result.Message,
                    NewBalance = (double)result.NewBalance
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in gRPC ProcessTransfer");
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }

        public override async Task<DepositResponse> ProcessDeposit(DepositRequest request, ServerCallContext context)
        {
            try
            {
                var transactionRequest = new TransactionRequest
                {
                    ToAccountId = request.AccountId,
                    Amount = (decimal)request.Amount,
                    TransactionType = "Deposit",
                    Description = request.Description
                };

                var result = await _transactionService.ProcessDepositAsync(transactionRequest);

                return new DepositResponse
                {
                    Success = result.Success,
                    TransactionId = result.TransactionId ?? "",
                    Message = result.Message,
                    NewBalance = (double)result.NewBalance
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in gRPC ProcessDeposit");
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }

        public override async Task<WithdrawalResponse> ProcessWithdrawal(WithdrawalRequest request, ServerCallContext context)
        {
            try
            {
                var transactionRequest = new TransactionRequest
                {
                    FromAccountId = request.AccountId,
                    Amount = (decimal)request.Amount,
                    TransactionType = "Withdrawal",
                    Description = request.Description
                };

                var result = await _transactionService.ProcessWithdrawalAsync(transactionRequest);

                return new WithdrawalResponse
                {
                    Success = result.Success,
                    TransactionId = result.TransactionId ?? "",
                    Message = result.Message,
                    NewBalance = (double)result.NewBalance
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in gRPC ProcessWithdrawal");
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }

        public override async Task<BalanceResponse> GetBalance(BalanceRequest request, ServerCallContext context)
        {
            try
            {
                var result = await _transactionService.GetAccountBalanceAsync(request.AccountId);

                if (result == null)
                {
                    return new BalanceResponse
                    {
                        Success = false
                    };
                }

                return new BalanceResponse
                {
                    Success = true,
                    Balance = (double)result.Balance,
                    AccountNumber = result.AccountNumber,
                    AccountType = result.AccountType
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in gRPC GetBalance");
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }

        public override async Task<TransactionHistoryResponse> GetTransactionHistory(TransactionHistoryRequest request, ServerCallContext context)
        {
            try
            {
                var transactions = await _transactionService.GetAccountTransactionsAsync(request.AccountId);
                var limitedTransactions = transactions.Take(request.Limit > 0 ? request.Limit : 50);

                var response = new TransactionHistoryResponse
                {
                    Success = true
                };

                foreach (var transaction in limitedTransactions)
                {
                    response.Transactions.Add(new TransactionItem
                    {
                        TransactionId = transaction.TransactionId.ToString(),
                        TransactionType = transaction.TransactionType,
                        Amount = (double)transaction.Amount,
                        Description = transaction.Description ?? "",
                        TransactionDate = transaction.TransactionDate.ToString("yyyy-MM-dd HH:mm:ss"),
                        Status = transaction.Status
                    });
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in gRPC GetTransactionHistory");
                throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
            }
        }
    }
}