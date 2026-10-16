namespace NutriCare.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.DTOs;
using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/payments/cancellation-refunds")]
public sealed class CancellationRefundsController(NutriCareDbContext db, CancellationRefundProcessor processor,
    ICurrentUserService actor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        var query = db.CancellationRefundInstructions.AsNoTracking().Where(i => i.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(i => i.Status == status);
        var rows = await query.OrderByDescending(i => i.CreatedAt).Take(Math.Clamp(limit, 1, 250))
            .Select(i => new { i.Id, i.AppointmentId, i.CancellationActor, i.CancelledAtUtc, i.PolicyId, i.PolicyVersion, i.Eligible, i.DecisionReason,
                i.ExpectedAmount, i.Currency, i.Status, i.AttemptCount, i.NextAttemptAt, i.LastErrorCode, i.UpdatedAt }).ToArrayAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(rows));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        var item = await db.CancellationRefundInstructions.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id && i.DeletedAt == null, cancellationToken);
        if (item == null) return NotFound(ApiResponse<object>.FailureResponse("Cancellation refund instruction was not found", 404));
        var payments = await db.Payments.AsNoTracking().Where(p => p.RelatedToType == "Appointment" && p.RelatedToId == item.AppointmentId && p.DeletedAt == null)
            .Select(p => new { p.Id, p.Amount, p.Currency, p.Status, p.RefundedAmount }).ToArrayAsync(cancellationToken);
        var ids = payments.Select(p => p.Id).ToArray();
        var refunds = await db.PaymentRefunds.AsNoTracking().Where(r => ids.Contains(r.PaymentId) && r.DeletedAt == null)
            .Select(r => new { r.Id, r.PaymentId, r.Amount, r.Currency, r.Status, r.ProviderRefundId, r.ProviderRequestStartedAt, r.LastErrorCode, r.RefundedAt, r.UpdatedAt }).ToArrayAsync(cancellationToken);
        return Ok(ApiResponse<object>.SuccessResponse(new { instruction = item, payments, refunds }));
    }

    [HttpPost("{id:long}/reconcile")]
    public async Task<IActionResult> Reconcile(long id, CancellationToken cancellationToken)
    {
        if (!await db.CancellationRefundInstructions.AnyAsync(i => i.Id == id && i.DeletedAt == null, cancellationToken))
            return NotFound(ApiResponse<object>.FailureResponse("Cancellation refund instruction was not found", 404));
        db.AuditLogs.Add(new AuditLog { UserId = actor.UserId, Action = "CancellationRefundReconciliationRequested", EntityType = "CancellationRefundInstruction", EntityId = id,
            Description = "Read-only provider reconciliation requested by administrator" });
        await db.SaveChangesAsync(cancellationToken);
        // This endpoint can recover an existing provider refund, but never issues a new POST.
        await processor.ProcessAsync(id, cancellationToken, readOnlyRecovery: true);
        return await Detail(id, cancellationToken);
    }
}
