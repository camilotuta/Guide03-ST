using Microsoft.AspNetCore.Mvc;
using Guia03.Core.Interfaces;
using Guia03odels;

namespace Guia03ntrollers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(IAccountService accountService, ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<Account>> CreateAccount([FromBody] CreateAccountRequest request)
        {
            try
            {
                var result = await _accountService.CreateAccountAsync(request.AccountType);
                return CreatedAtAction(nameof(GetAccount), new { id = result.AccountId }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating account");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Account>> GetAccount(int id)
        {
            try
            {
                var result = await _accountService.GetAccountAsync(id);
                if (result == null)
                    return NotFound();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet]
        public async Task<ActionResult<List<Account>>> GetAllAccounts()
        {
            try
            {
                var result = await _accountService.GetAllAccountsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all accounts");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<bool>> DeactivateAccount(int id)
        {
            try
            {
                var result = await _accountService.DeactivateAccountAsync(id);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating account");
                return StatusCode(500, false);
            }
        }
    }

    public class CreateAccountRequest
    {
        public string AccountType { get; set; }
    }
}