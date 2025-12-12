using Microsoft.AspNetCore.Mvc;
using PaymentsService.Models;
using PaymentsService.Services;

namespace PaymentsService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost("accounts")]
    public async Task<ActionResult<Account>> CreateAccount([FromHeader(Name = "X-User-Id")] string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("User ID is required");
        }

        var account = await _paymentService.CreateAccountAsync(userId);
        if (account == null)
        {
            return Conflict("Account already exists for this user");
        }

        return Ok(account);
    }

    [HttpGet("accounts/balance")]
    public async Task<ActionResult<decimal>> GetBalance([FromHeader(Name = "X-User-Id")] string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("User ID is required");
        }

        var account = await _paymentService.GetAccountAsync(userId);
        if (account == null)
        {
            return NotFound("Account not found");
        }

        return Ok(account.Balance);
    }

    [HttpPost("accounts/topup")]
    public async Task<ActionResult<Account>> TopUpAccount(
        [FromHeader(Name = "X-User-Id")] string userId,
        [FromBody] TopUpRequest request)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("User ID is required");
        }

        if (request.Amount <= 0)
        {
            return BadRequest("Amount must be greater than zero");
        }

        var account = await _paymentService.TopUpAccountAsync(userId, request.Amount);
        if (account == null)
        {
            return NotFound("Account not found");
        }

        return Ok(account);
    }
}

public class TopUpRequest
{
    public decimal Amount { get; set; }
}
