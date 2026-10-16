namespace NutriCare.API.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutriCare.Application.Commands.Payments;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.Payments;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;
using NutriCare.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IRepository<Patient> _patientRepository;
    private readonly NutriCareDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;

    public PaymentsController(
        IMediator mediator,
        IRepository<Patient> patientRepository,
        NutriCareDbContext context,
        INotificationService notificationService, IConfiguration configuration)
    {
        _mediator = mediator;
        _patientRepository = patientRepository;
        _context = context;
        _notificationService = notificationService;
        _configuration = configuration;
    }

    [HttpPost("process")]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> ProcessPayment([FromBody] CreatePaymentRequest request)
    {
        var patient = await GetCurrentPatientAsync();
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        var result = await _mediator.Send(new CreatePaymentCommand
        {
            PatientId = patient.Id,
            IdempotencyKey = idempotencyKey,
            NutritionistId = request.NutritionistId,
            Amount = request.Amount,
            Currency = request.Currency,
            PaymentMethod = request.PaymentMethod,
            Description = request.Description,
            RelatedToType = request.RelatedToType,
            RelatedToId = request.RelatedToId,
            SuccessUrl = TrustedReturnUrl(request.SuccessUrl, "success"),
            CancelUrl = TrustedReturnUrl(request.CancelUrl, "cancelled")
        });

        return CreatedAtAction(nameof(GetPayment), new { id = result.Id },
            ApiResponse<PaymentDto>.SuccessResponse(result, "Payment initialized successfully", 201));
    }

    [HttpGet("history")]
    [Authorize(Roles = "Patient")]
    public async Task<ActionResult<ApiResponse<IEnumerable<PaymentDto>>>> GetMyPaymentHistory(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var patient = await GetCurrentPatientAsync();
        var result = await _mediator.Send(new GetPaymentHistoryQuery
        {
            PatientId = patient.Id,
            Status = status,
            FromDate = fromDate,
            ToDate = toDate
        });

        return Ok(ApiResponse<IEnumerable<PaymentDto>>.SuccessResponse(result));
    }

    [HttpGet("patient/{patientId:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<PaymentDto>>>> GetPatientPaymentHistory(
        long patientId,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var result = await _mediator.Send(new GetPaymentHistoryQuery
        {
            PatientId = patientId,
            Status = status,
            FromDate = fromDate,
            ToDate = toDate
        });

        return Ok(ApiResponse<IEnumerable<PaymentDto>>.SuccessResponse(result));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<IEnumerable<PaymentDto>>>> GetAllPayments(
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var result = await _mediator.Send(new GetPaymentHistoryQuery
        {
            Status = status,
            FromDate = fromDate,
            ToDate = toDate
        });

        return Ok(ApiResponse<IEnumerable<PaymentDto>>.SuccessResponse(result));
    }

    [HttpGet("{id:long}")]
    [Authorize(Roles = "Patient,Admin")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> GetPayment(long id)
    {
        var result = await _mediator.Send(new GetPaymentByIdQuery { PaymentId = id });
        if (GetCurrentRole() == "Patient")
        {
            var patient = await GetCurrentPatientAsync();
            if (result.PatientId != patient.Id)
            {
                return Forbid();
            }
        }

        return Ok(ApiResponse<PaymentDto>.SuccessResponse(result));
    }

    [HttpGet("{id:long}/invoice")]
    [Authorize(Roles = "Patient,Admin")]
    public async Task<ActionResult<ApiResponse<InvoiceDto>>> GetInvoice(long id)
    {
        var payment = await _mediator.Send(new GetPaymentByIdQuery { PaymentId = id });
        if (GetCurrentRole() == "Patient")
        {
            var patient = await GetCurrentPatientAsync();
            if (payment.PatientId != patient.Id)
            {
                return Forbid();
            }
        }

        var result = await _mediator.Send(new GetInvoiceByPaymentQuery { PaymentId = id });
        return Ok(ApiResponse<InvoiceDto>.SuccessResponse(result));
    }

    [HttpPost("{id:long}/refund")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<PaymentRefundDto>>> RefundPayment(long id, [FromBody] RefundPaymentRequest request)
    {
        var result = await _mediator.Send(new RefundPaymentCommand
        {
            PaymentId = id,
            IdempotencyKey = Request.Headers["Idempotency-Key"].ToString(),
            Amount = request.Amount,
            Reason = request.Reason
        });

        var message = result.Status == "Completed" ? "Refund confirmed by the payment provider" :
            result.Status == "Failed" ? "The provider reported that this refund failed" : "Refund request recorded; provider confirmation is pending";
        return Ok(ApiResponse<PaymentRefundDto>.SuccessResponse(result, message));
    }

    [HttpPost("webhooks/{provider}")]
    [AllowAnonymous]
    [RequestSizeLimit(256 * 1024)]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> HandleWebhook(string provider)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault()
            ?? Request.Headers["X-Signature"].FirstOrDefault();

        PaymentDto result;
        try
        {
        result = await _mediator.Send(new HandlePaymentWebhookCommand
        {
            Provider = provider,
            Payload = payload,
            Signature = signature
        });
        }
        catch (UnsupportedProviderEventException)
        {
            return Ok(ApiResponse<object>.SuccessResponse(new { ignored = true }, "Signed event is not applicable"));
        }

        return Ok(ApiResponse<PaymentDto>.SuccessResponse(result, "Webhook processed successfully"));
    }

    private async Task<Patient> GetCurrentPatientAsync()
    {
        var userId = GetCurrentUserId();
        var patients = await _patientRepository.GetAsync(query => query.Where(p => p.UserId == userId && p.DeletedAt == null));
        return patients.FirstOrDefault()
            ?? throw new InvalidOperationException("Patient profile was not found for the current user");
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst("uid")?.Value;
        return long.TryParse(claim, out var userId) ? userId : 0;
    }

    private string GetCurrentRole()
    {
        return User.FindFirst("role")?.Value ?? string.Empty;
    }

    private string TrustedReturnUrl(string? requested, string state)
    {
        var origins = _configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
        var configured = _configuration["Frontend:BaseUrl"] ?? origins.FirstOrDefault();
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("https" or "http")) throw new InvalidOperationException("The portal origin is not configured");
        if (string.IsNullOrWhiteSpace(requested)) return new Uri(baseUri, $"/patient/payments?checkout={state}").ToString();
        if (!Uri.TryCreate(requested, UriKind.Absolute, out var target) || !origins.Append(baseUri.GetLeftPart(UriPartial.Authority)).Any(o => Uri.TryCreate(o, UriKind.Absolute, out var allowed) && allowed.Scheme == target.Scheme && allowed.Authority.Equals(target.Authority, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("The checkout return URL must use an approved portal origin");
        return target.ToString();
    }

    private async Task NotifyPaymentReceivedAsync(PaymentDto payment)
    {
        if (!payment.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var patient = await _context.Patients
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == payment.PatientId);

        var recipient = patient?.User == null
            ? null
            : new NotificationRecipient(
                patient.User.Id,
                patient.User.Email ?? string.Empty,
                patient.User.PhoneNumber,
                $"{patient.User.FirstName} {patient.User.LastName}".Trim());

        if (recipient == null)
        {
            return;
        }

        await _notificationService.SendPaymentReceivedAsync(
            recipient,
            payment.Id,
            payment.Amount,
            payment.Currency);
    }
}
