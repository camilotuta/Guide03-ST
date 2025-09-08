using Microsoft.AspNetCore.Mvc;
using BankingSystem.Core.Interfaces;
using BankingSystem.Core.DTOs;

namespace BankingSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;
        private readonly ILogger<TransactionController> _logger;

        public TransactionController(ITransactionService transactionService, ILogger<TransactionController> logger)
        {
            _transactionService = transactionService;
            _logger = logger;
        }

        [HttpPost("transfer")]
        public async Task<ActionResult<TransactionResponse>> ProcessTransfer([FromBody] TransactionRequest request)
        {
            try
            {
                request.TransactionType = "Transfer";
                var result = await _transactionService.ProcessTransferAsync(request);

                if (result.Success)
                    return Ok(result);
                else
                    return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing transfer");
                return StatusCode(500, new TransactionResponse
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPost("deposit")]
        public async Task<ActionResult<TransactionResponse>> ProcessDeposit([FromBody] TransactionRequest request)
        {
            try
            {
                request.TransactionType = "Deposit";
                var result = await _transactionService.ProcessDepositAsync(request);

                if (result.Success)
                    return Ok(result);
                else
                    return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing deposit");
                return StatusCode(500, new TransactionResponse
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPost("withdrawal")]
        public async Task<ActionResult<TransactionResponse>> ProcessWithdrawal([FromBody] TransactionRequest request)
        {
            try
            {
                request.TransactionType = "Withdrawal";
                var result = await _transactionService.ProcessWithdrawalAsync(request);

                if (result.Success)
                    return Ok(result);
                else
                    return BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing withdrawal");
                return StatusCode(500, new TransactionResponse
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        [HttpPost("batch")]
        public async Task<ActionResult<bool>> ProcessMultipleTransactions([FromBody] List<TransactionRequest> requests)
        {
            try
            {
                var result = await _transactionService.ProcessMultipleTransactionsAsync(requests);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch transactions");
                return StatusCode(500, false);
            }
        }

        [HttpGet("balance/{accountId}")]
        public async Task<ActionResult<AccountBalance>> GetBalance(int accountId)
        {
            try
            {
                var result = await _transactionService.GetAccountBalanceAsync(accountId);
                if (result == null)
                    return NotFound();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting balance");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("history/{accountId}")]
        public async Task<ActionResult<List<Transaction>>> GetTransactionHistory(int accountId)
        {
            try
            {
                var result = await _transactionService.GetAccountTransactionsAsync(accountId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction history");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}