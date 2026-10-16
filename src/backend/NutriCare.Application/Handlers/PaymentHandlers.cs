namespace NutriCare.Application.Handlers.Payments;

using System.Security.Cryptography;
using System.Text;
using MediatR;
using NutriCare.Application.Commands.Payments;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.Payments;
using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;

public class CreatePaymentCommandHandler(IRepository<Appointment> appointments,
    IRepository<Payment> payments, IRepository<Invoice> invoices, IPaymentGatewayService gateway, IUnitOfWork unitOfWork)
    : IRequestHandler<CreatePaymentCommand, PaymentDto>
{
    public async Task<PaymentDto> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        PaymentHandlerHelpers.ValidateIdempotencyKey(request.IdempotencyKey);
        if (!request.PaymentMethod.Equals("Stripe", StringComparison.OrdinalIgnoreCase)) throw new ValidationException("This payment provider is not available");
        if (request.RelatedToType != "Appointment" || !request.RelatedToId.HasValue)
            throw new ValidationException("Checkout requires an appointment");
        var appointmentId = request.RelatedToId.Value;
        Payment payment;
        await unitOfWork.BeginTransactionAsync($"payment:appointment:{appointmentId}");
        try
        {
            var appointment = await PaymentHandlerHelpers.GetRequiredAsync(appointments, appointmentId);
            if (appointment.PatientId != request.PatientId) throw new ForbiddenAccessException("Appointment access denied");
            if (appointment.PackageEnrollmentId.HasValue) throw new ConflictException("This appointment is covered by a package credit and cannot be charged separately");
            if (appointment.Status is "Cancelled" or "Rejected" or "Completed" or "No-show") throw new ConflictException("Appointment cannot be paid in its current state");
            if (!appointment.PriceSnapshot.HasValue || appointment.PriceSnapshot <= 0) throw new ConflictException("An approved price snapshot is required before checkout");
            if (request.Amount != appointment.PriceSnapshot || !request.Currency.Equals(appointment.Currency, StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("The checkout amount must match the appointment quote");
            if (request.NutritionistId.HasValue && request.NutritionistId != appointment.NutritionistId)
                throw new ValidationException("The clinician does not match the appointment");
            var provider = PaymentHandlerHelpers.NormalizeProvider(request.PaymentMethod);
            var matching = await payments.GetAsync(q => q.Where(p => p.PatientId == request.PatientId && p.IdempotencyKey == request.IdempotencyKey && p.DeletedAt == null));
            payment = matching.FirstOrDefault()!;
            if (payment != null)
            {
                if (payment.RelatedToId != appointmentId || payment.PaymentMethod != provider || payment.Amount != appointment.PriceSnapshot)
                    throw new ConflictException("The idempotency key was already used for another request");
                if (!string.IsNullOrWhiteSpace(payment.ProviderSessionId) || payment.Status != "Pending")
                {
                    await unitOfWork.CommitAsync();
                    return PaymentHandlerHelpers.ToDto(payment);
                }
            }
            else
            {
                var existing = await payments.GetAsync(q => q.Where(p => p.RelatedToType == "Appointment" && p.RelatedToId == appointmentId && p.DeletedAt == null && (p.Status == "Pending" || p.Status == "Completed")));
                if (appointment.IsPaid || existing.Any()) throw new ConflictException("Checkout already exists for this appointment; use the existing payment");
                var invoice = new Invoice
                {
                    InvoiceNumber = PaymentHandlerHelpers.CreateInvoiceNumber(), PatientId = appointment.PatientId,
                    NutritionistId = appointment.NutritionistId, Subtotal = appointment.PriceSnapshot.Value,
                    TotalAmount = appointment.PriceSnapshot.Value, Currency = appointment.Currency,
                    Description = "Consultation", RelatedToType = "Appointment", RelatedToId = appointment.Id,
                    Status = "Issued", IssuedAt = DateTime.UtcNow
                };
                await invoices.AddAsync(invoice);
                payment = new Payment
                {
                    PatientId = appointment.PatientId, NutritionistId = appointment.NutritionistId, Invoice = invoice,
                    Amount = appointment.PriceSnapshot.Value, Currency = appointment.Currency,
                    PaymentMethod = provider, TransactionId = PaymentHandlerHelpers.CreateTransactionId(provider),
                    Description = "Consultation", RelatedToType = "Appointment", RelatedToId = appointment.Id,
                    Status = "Pending", IdempotencyKey = request.IdempotencyKey
                };
                await payments.AddAsync(payment);
            }
            await unitOfWork.CommitAsync();
        }
        catch { await unitOfWork.RollbackAsync(); throw; }

        // The durable intent and stable provider key survive a timeout or process crash.
        var result = await gateway.InitializePaymentAsync(payment.PaymentMethod,
            new PaymentGatewayInitializeRequest(payment.Amount, payment.Currency, "Consultation", payment.TransactionId,
                request.SuccessUrl ?? "", request.CancelUrl ?? "", $"nc-checkout-{payment.TransactionId}"), cancellationToken);
        await unitOfWork.BeginTransactionAsync($"payment:appointment:{appointmentId}");
        try
        {
            await payments.ReloadAsync(payment);
            if (payment.ProviderSessionId != null && payment.ProviderSessionId != result.ProviderPaymentId)
                throw new ConflictException("The provider returned another checkout identity");
            if (payment.ProviderPaymentId != null && result.PaymentIntentId != null && payment.ProviderPaymentId != result.PaymentIntentId)
                throw new ConflictException("The provider returned another payment intent");
            payment.ProviderSessionId = result.ProviderPaymentId;
            payment.ProviderPaymentId ??= result.PaymentIntentId;
            payment.CheckoutUrl = result.CheckoutUrl;
            payment.GatewayResponse = result.RawResponse;
            await payments.UpdateAsync(payment);
            await unitOfWork.CommitAsync();
        }
        catch { await unitOfWork.RollbackAsync(); throw; }
        return PaymentHandlerHelpers.ToDto(payment, payment.Invoice);
    }
}

public class RefundPaymentCommandHandler(IRepository<Payment> payments, IRepository<PaymentRefund> refunds,
    IRepository<Invoice> invoices, IRepository<Appointment> appointments, IPaymentGatewayService gateway, IUnitOfWork unitOfWork,
    IPaymentRefundReconciliation? reconciliation = null, IRepository<AuditLog>? audits = null)
    : IRequestHandler<RefundPaymentCommand, PaymentRefundDto>
{
    public Task<PaymentRefundDto> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
        => new PaymentRefundWorkflow(payments, refunds, invoices, appointments, gateway,
            reconciliation ?? gateway as IPaymentRefundReconciliation ?? throw new InvalidOperationException("Refund reconciliation is not configured"),
            unitOfWork, audits).RefundAsync(request, cancellationToken);
}

public class HandlePaymentWebhookCommandHandler(IRepository<Payment> payments, IRepository<Invoice> invoices,
    IRepository<ProviderWebhookEvent> events, IRepository<PaymentRefund> refunds, IRepository<Appointment> appointments,
    IRepository<Patient> patients, IRepository<User> users, IPaymentGatewayService gateway,
    INotificationOutbox notifications, IUnitOfWork unitOfWork, ICancellationRefundOutbox? cancellationRefunds = null) : IRequestHandler<HandlePaymentWebhookCommand, PaymentDto>
{
    public async Task<PaymentDto> Handle(HandlePaymentWebhookCommand request, CancellationToken cancellationToken)
    {
        var webhook = await gateway.ParseWebhookAsync(request.Provider, request.Payload, request.Signature, cancellationToken);
        if (string.IsNullOrWhiteSpace(webhook.EventId)) throw new ValidationException("Provider event identity is required");
        var provider = PaymentHandlerHelpers.NormalizeProvider(request.Provider);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Payload)));
        var matchedPayment = (await payments.GetAsync(q => q.Where(p => p.DeletedAt == null && p.PaymentMethod == provider &&
            ((webhook.TransactionId != "" && p.TransactionId == webhook.TransactionId) ||
             (webhook.ProviderPaymentId != null && p.ProviderPaymentId == webhook.ProviderPaymentId))))).SingleOrDefault()
            ?? throw new EntityNotFoundException("Payment", 0);
        await unitOfWork.BeginTransactionAsync($"payment:{matchedPayment.Id}");
        try
        {
            var previous = (await events.GetAsync(q => q.Where(e => e.Provider == provider && e.EventId == webhook.EventId))).FirstOrDefault();
            if (previous != null)
            {
                if (previous.PayloadHash != hash) throw new ForbiddenAccessException("Provider event identity was reused with different content");
                var duplicate = await PaymentHandlerHelpers.GetRequiredAsync(payments, previous.PaymentId);
                await unitOfWork.CommitAsync();
                return PaymentHandlerHelpers.ToDto(duplicate);
            }
            var payment = matchedPayment;
            await payments.ReloadAsync(payment);
            if (webhook.Currency != payment.Currency || !webhook.Amount.HasValue) throw new ForbiddenAccessException("Provider payment does not match the expected currency");
            if (webhook.ProviderRefundId != null)
            {
                var refund = (await refunds.GetAsync(q => q.Where(r => r.PaymentId == payment.Id && r.ProviderRefundId == webhook.ProviderRefundId && r.DeletedAt == null))).SingleOrDefault()
                    ?? throw new EntityNotFoundException("Refund", 0);
                await refunds.ReloadAsync(refund);
                if (refund.Amount != webhook.Amount) throw new ForbiddenAccessException("Refund amount does not match the requested refund");
                var state = PaymentHandlerHelpers.MapProviderStatus(webhook.Status);
                if (refund.Status != "Completed")
                {
                    refund.Status = state;
                    if (state == "Completed") refund.RefundedAt = DateTime.UtcNow;
                    refund.LastErrorCode = state == "Failed" ? "ProviderRefundFailed" : webhook.Status == "requires_action" ? "ProviderActionRequired" : null;
                    refund.ProcessingToken = null; refund.ProcessingUntil = null;
                    await refunds.UpdateAsync(refund);
                    await unitOfWork.SaveChangesAsync();
                    await PaymentHandlerHelpers.ReconcileRefundsAsync(payment, refunds, payments, invoices, appointments);
                }
            }
            else
            {
                if (webhook.Amount != payment.Amount || (!string.IsNullOrWhiteSpace(payment.ProviderSessionId) && payment.ProviderSessionId != webhook.ProviderSessionId))
                    throw new ForbiddenAccessException("Provider payment does not match the expected checkout");
                var state = PaymentHandlerHelpers.MapProviderStatus(webhook.Status);
                // Never regress a settled/refunded payment or let a late failure undo fulfillment.
                if (payment.Status is not ("Completed" or "PartialRefund" or "Refunded"))
                {
                    payment.ProviderSessionId ??= webhook.ProviderSessionId;
                    payment.ProviderPaymentId = webhook.ProviderPaymentId ?? payment.ProviderPaymentId;
                    if (state == "Completed" && string.IsNullOrWhiteSpace(payment.ProviderPaymentId)) throw new ValidationException("A settled payment intent is required");
                    payment.Status = state;
                    if (state == "Completed")
                    {
                        payment.PaidAt = webhook.OccurredAt ?? DateTime.UtcNow;
                        if (payment.InvoiceId.HasValue)
                        {
                            var invoice = await PaymentHandlerHelpers.GetRequiredAsync(invoices, payment.InvoiceId.Value);
                            invoice.Status = "Paid"; invoice.PaidAt = payment.PaidAt;
                            await invoices.UpdateAsync(invoice);
                        }
                        if (payment.RelatedToType == "Appointment" && payment.RelatedToId.HasValue)
                        {
                            var appointment = await PaymentHandlerHelpers.GetRequiredAsync(appointments, payment.RelatedToId.Value);
                            if (appointment.PatientId != payment.PatientId || appointment.PriceSnapshot != payment.Amount || appointment.Currency != payment.Currency)
                                throw new ConflictException("Appointment quote no longer matches payment");
                            appointment.IsPaid = true;
                            appointment.PaymentStatus = appointment.Status is "Cancelled" or "Rejected" ? "RefundReviewRequired" : "Paid";
                            await appointments.UpdateAsync(appointment);
                        }
                        var patient = await PaymentHandlerHelpers.GetRequiredAsync(patients, payment.PatientId);
                        var user = await users.GetByIdAsync(patient.UserId);
                        if (user != null) await notifications.EnqueueAsync(new NotificationRecipient(user.Id, user.Email ?? "", user.PhoneNumber, ""),
                            new NotificationRequest("PaymentReceived", "Payment received", $"Your payment of {payment.Amount:0.00} {payment.Currency} was received.", "Payment", payment.Id, "/patient/payments", ["InApp", "Email"]),
                            $"payment:{payment.Id}:completed", cancellationToken);
                    }
                    await payments.UpdateAsync(payment);
                }
            }
            if (cancellationRefunds != null && payment.RelatedToType == "Appointment" && payment.RelatedToId.HasValue)
                await cancellationRefunds.WakeAsync(payment.RelatedToId.Value, cancellationToken);
            await events.AddAsync(new ProviderWebhookEvent { Provider = provider, EventId = webhook.EventId, PaymentId = payment.Id, PayloadHash = hash, ProcessedAt = DateTime.UtcNow });
            await unitOfWork.CommitAsync();
            return PaymentHandlerHelpers.ToDto(payment);
        }
        catch { await unitOfWork.RollbackAsync(); throw; }
    }
}

public class GetPaymentHistoryQueryHandler : IRequestHandler<GetPaymentHistoryQuery, IEnumerable<PaymentDto>>
{
    private readonly IRepository<Payment> _paymentRepository;

    public GetPaymentHistoryQueryHandler(IRepository<Payment> paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<IEnumerable<PaymentDto>> Handle(GetPaymentHistoryQuery request, CancellationToken cancellationToken)
    {
        var payments = await _paymentRepository.GetAsync(query =>
        {
            var filtered = query.Where(p => p.DeletedAt == null);
            if (request.PatientId.HasValue)
            {
                filtered = filtered.Where(p => p.PatientId == request.PatientId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                filtered = filtered.Where(p => p.Status == request.Status);
            }

            if (request.FromDate.HasValue)
            {
                filtered = filtered.Where(p => p.CreatedAt >= request.FromDate.Value.Date);
            }

            if (request.ToDate.HasValue)
            {
                filtered = filtered.Where(p => p.CreatedAt <= request.ToDate.Value.Date.AddDays(1).AddTicks(-1));
            }

            return filtered.OrderByDescending(p => p.CreatedAt);
        });

        return payments.Select(p => PaymentHandlerHelpers.ToDto(p)).ToArray();
    }
}

public class GetPaymentByIdQueryHandler : IRequestHandler<GetPaymentByIdQuery, PaymentDto>
{
    private readonly IRepository<Payment> _paymentRepository;

    public GetPaymentByIdQueryHandler(IRepository<Payment> paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<PaymentDto> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await PaymentHandlerHelpers.GetRequiredAsync(_paymentRepository, request.PaymentId);
        return PaymentHandlerHelpers.ToDto(payment);
    }
}

public class GetInvoiceByPaymentQueryHandler : IRequestHandler<GetInvoiceByPaymentQuery, InvoiceDto>
{
    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<Invoice> _invoiceRepository;

    public GetInvoiceByPaymentQueryHandler(IRepository<Payment> paymentRepository, IRepository<Invoice> invoiceRepository)
    {
        _paymentRepository = paymentRepository;
        _invoiceRepository = invoiceRepository;
    }

    public async Task<InvoiceDto> Handle(GetInvoiceByPaymentQuery request, CancellationToken cancellationToken)
    {
        var payment = await PaymentHandlerHelpers.GetRequiredAsync(_paymentRepository, request.PaymentId);
        if (!payment.InvoiceId.HasValue)
        {
            throw new EntityNotFoundException("Invoice", 0);
        }

        var invoice = await PaymentHandlerHelpers.GetRequiredAsync(_invoiceRepository, payment.InvoiceId.Value);
        return PaymentHandlerHelpers.ToInvoiceDto(invoice);
    }
}

internal static class PaymentHandlerHelpers
{
    internal static async Task<T> GetRequiredAsync<T>(IRepository<T> repository, long id) where T : BaseEntity
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity == null || entity.IsDeleted)
        {
            throw new EntityNotFoundException(typeof(T).Name, id);
        }

        return entity;
    }

    internal static void ValidateIdempotencyKey(string key)
    {
        if (key.Length is < 16 or > 100 || key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new ValidationException("Idempotency-Key must contain 16 to 100 letters, digits, hyphens or underscores");
    }

    internal static async Task ReconcileRefundsAsync(Payment payment, IRepository<PaymentRefund> refunds, IRepository<Payment> payments, IRepository<Invoice> invoices, IRepository<Appointment> appointments)
    {
        var successful = await refunds.GetAsync(q => q.Where(r => r.PaymentId == payment.Id && r.DeletedAt == null && r.Status == "Completed"));
        foreach (var entry in successful) await refunds.ReloadAsync(entry);
        payment.RefundedAmount = successful.Sum(r => r.Amount);
        if (payment.RefundedAmount > payment.Amount) throw new ConflictException("Refund reconciliation exceeds payment balance");
        payment.Status = payment.RefundedAmount >= payment.Amount ? "Refunded" : payment.RefundedAmount > 0 ? "PartialRefund" : "Completed";
        payment.RefundedAt = successful.Any() ? successful.Max(r => r.RefundedAt) : null;
        await payments.UpdateAsync(payment);
        if (payment.InvoiceId.HasValue)
        {
            var invoice = await GetRequiredAsync(invoices, payment.InvoiceId.Value);
            await invoices.ReloadAsync(invoice);
            invoice.Status = payment.Status == "Refunded" ? "Refunded" : payment.Status == "PartialRefund" ? "PartiallyRefunded" : "Paid";
            await invoices.UpdateAsync(invoice);
        }
        if (payment.RelatedToType == "Appointment" && payment.RelatedToId.HasValue)
        {
            var appointment = await GetRequiredAsync(appointments, payment.RelatedToId.Value);
            await appointments.ReloadAsync(appointment);
            var related = (await payments.GetAsync(q => q.Where(p => p.RelatedToType == "Appointment" && p.RelatedToId == appointment.Id && p.DeletedAt == null))).ToArray();
            foreach (var other in related.Where(p => p.Id != payment.Id)) await payments.ReloadAsync(other);
            var captured = related.Where(p => p.Status is "Completed" or "PartialRefund" or "Refunded").ToArray();
            var remaining = captured.Sum(p => p.Amount - p.RefundedAmount);
            var refunded = captured.Sum(p => p.RefundedAmount);
            appointment.IsPaid = remaining > 0;
            appointment.PaymentStatus = remaining == 0 && captured.Length > 0 && !related.Any(p => p.Status == "Pending") ? "Refunded" :
                refunded > 0 ? "PartiallyRefunded" : appointment.Status is "Cancelled" or "Rejected" ? "RefundReviewRequired" : "Paid";
            await appointments.UpdateAsync(appointment);
        }
    }

    internal static string NormalizeProvider(string provider)
    {
        return provider.Trim() switch
        {
            var p when string.Equals(p, "Stripe", StringComparison.OrdinalIgnoreCase) => "Stripe",
            var p when string.Equals(p, "JazzCash", StringComparison.OrdinalIgnoreCase) => "JazzCash",
            var p when string.Equals(p, "EasyPaisa", StringComparison.OrdinalIgnoreCase) => "EasyPaisa",
            _ => throw new ValidationException("Unsupported payment provider")
        };
    }

    internal static string MapProviderStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "succeeded" or "paid" or "completed" or "success" => "Completed",
            "failed" or "cancelled" or "canceled" or "expired" => "Failed",
            "refunded" => "Refunded",
            "partialrefund" or "partially_refunded" => "PartialRefund",
            _ => "Pending"
        };
    }

    internal static string CreateTransactionId(string provider)
    {
        var prefix = NormalizeProvider(provider).ToUpperInvariant();
        var value = $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        return value[..Math.Min(64, value.Length)];
    }

    internal static string CreateInvoiceNumber()
    {
        return $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..24];
    }

    internal static PaymentDto ToDto(Payment payment, Invoice? invoice = null)
    {
        return new PaymentDto
        {
            Id = payment.Id,
            PatientId = payment.PatientId,
            NutritionistId = payment.NutritionistId,
            InvoiceId = payment.InvoiceId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            PaymentMethod = payment.PaymentMethod,
            TransactionId = payment.TransactionId,
            ProviderPaymentId = payment.ProviderPaymentId,
            CheckoutUrl = payment.CheckoutUrl,
            Status = payment.Status,
            Description = payment.Description,
            RelatedToType = payment.RelatedToType,
            RelatedToId = payment.RelatedToId,
            RefundedAmount = payment.RefundedAmount,
            RefundReason = payment.RefundReason,
            RefundedAt = payment.RefundedAt,
            PaidAt = payment.PaidAt,
            Invoice = invoice == null ? null : ToInvoiceDto(invoice),
            CreatedAt = payment.CreatedAt,
            UpdatedAt = payment.UpdatedAt
        };
    }

    internal static InvoiceDto ToInvoiceDto(Invoice invoice)
    {
        return new InvoiceDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            PatientId = invoice.PatientId,
            NutritionistId = invoice.NutritionistId,
            Subtotal = invoice.Subtotal,
            TaxAmount = invoice.TaxAmount,
            DiscountAmount = invoice.DiscountAmount,
            TotalAmount = invoice.TotalAmount,
            Currency = invoice.Currency,
            Status = invoice.Status,
            IssuedAt = invoice.IssuedAt,
            DueAt = invoice.DueAt,
            PaidAt = invoice.PaidAt,
            Description = invoice.Description,
            RelatedToType = invoice.RelatedToType,
            RelatedToId = invoice.RelatedToId,
            CreatedAt = invoice.CreatedAt,
            UpdatedAt = invoice.UpdatedAt
        };
    }

    internal static PaymentRefundDto ToRefundDto(PaymentRefund refund)
    {
        return new PaymentRefundDto
        {
            Id = refund.Id,
            PaymentId = refund.PaymentId,
            Amount = refund.Amount,
            Currency = refund.Currency,
            Status = refund.Status,
            Reason = refund.Reason,
            ProviderRefundId = refund.ProviderRefundId,
            RefundedAt = refund.RefundedAt,
            CreatedAt = refund.CreatedAt,
            UpdatedAt = refund.UpdatedAt
        };
    }
}
