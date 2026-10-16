namespace NutriCare.Tests.Integrations;

using NutriCare.Application.Commands.Payments;
using NutriCare.Application.Handlers.Payments;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;

public class PaymentIntegrityTests
{
    [Fact]
    public async Task ClientCannotChangeAppointmentPrice()
    {
        var appointments = new MemoryRepository<Appointment>();
        appointments.Items.Add(new Appointment { Id = 1, PatientId = 7, NutritionistId = 8, PriceSnapshot = 5000, Currency = "PKR", Status = "Confirmed" });
        var gateway = new FakePaymentGateway();
        var handler = new CreatePaymentCommandHandler(appointments, new MemoryRepository<Payment>(), new MemoryRepository<Invoice>(), gateway, new MemoryUnitOfWork());
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new CreatePaymentCommand { PatientId = 7, RelatedToType = "Appointment", RelatedToId = 1, Amount = 1, Currency = "PKR", IdempotencyKey = "price-tamper-test-0001" }, default));
        Assert.Equal(0, gateway.Initializes);
    }

    [Fact]
    public async Task PendingRefundReservesFundsWithoutReportingRefundedMoney()
    {
        var payments = new MemoryRepository<Payment>(); payments.Items.Add(new Payment { Id = 1, Amount = 5000, Currency = "PKR", PaymentMethod = "Stripe", ProviderPaymentId = "pi_test", Status = "Completed" });
        var refunds = new MemoryRepository<PaymentRefund>();
        var handler = new RefundPaymentCommandHandler(payments, refunds, new MemoryRepository<Invoice>(), new MemoryRepository<Appointment>(), new FakePaymentGateway(), new MemoryUnitOfWork());
        var result = await handler.Handle(new RefundPaymentCommand { PaymentId = 1, Amount = 3000, IdempotencyKey = "refund-pending-test-01" }, default);
        Assert.Equal("Pending", result.Status); Assert.Equal(0m, payments.Items[0].RefundedAmount); Assert.Equal("Completed", payments.Items[0].Status);
        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new RefundPaymentCommand { PaymentId = 1, Amount = 3000, IdempotencyKey = "refund-pending-test-02" }, default));
    }

    [Fact]
    public async Task DuplicateAndLateFailureEventsDoNotRepeatFulfillmentOrRegressSettlement()
    {
        var payments = new MemoryRepository<Payment>(); payments.Items.Add(new Payment { Id = 1, PatientId = 7, Amount = 5000, Currency = "PKR", PaymentMethod = "Stripe", TransactionId = "transaction", ProviderSessionId = "cs_test", Status = "Pending" });
        var patients = new MemoryRepository<Patient>(); patients.Items.Add(new Patient { Id = 7, UserId = 70 });
        var users = new MemoryRepository<User>(); users.Items.Add(new User { Id = 70, Email = "patient@example.test" });
        var ledger = new MemoryRepository<ProviderWebhookEvent>(); var outbox = new RecordingOutbox(); var gateway = new FakePaymentGateway();
        var handler = new HandlePaymentWebhookCommandHandler(payments, new MemoryRepository<Invoice>(), ledger, new MemoryRepository<PaymentRefund>(), new MemoryRepository<Appointment>(), patients, users, gateway, outbox, new MemoryUnitOfWork());
        var command = new HandlePaymentWebhookCommand { Provider = "Stripe", Payload = "signed-body" };
        await handler.Handle(command, default); await handler.Handle(command, default);
        Assert.Single(ledger.Items); Assert.Single(outbox.Events); Assert.Equal("Completed", payments.Items[0].Status);
        gateway.Webhook = gateway.Webhook with { EventId = "evt_late_failure", Status = "failed" };
        await handler.Handle(command, default);
        Assert.Equal("Completed", payments.Items[0].Status); Assert.Single(outbox.Events); Assert.Equal(2, ledger.Items.Count);
    }

    [Fact]
    public async Task CurrencyMismatchCannotSettlePayment()
    {
        var payments = new MemoryRepository<Payment>(); payments.Items.Add(new Payment { Id = 1, Amount = 5000, Currency = "USD", PaymentMethod = "Stripe", TransactionId = "transaction", Status = "Pending" });
        var handler = new HandlePaymentWebhookCommandHandler(payments, new MemoryRepository<Invoice>(), new MemoryRepository<ProviderWebhookEvent>(), new MemoryRepository<PaymentRefund>(), new MemoryRepository<Appointment>(), new MemoryRepository<Patient>(), new MemoryRepository<User>(), new FakePaymentGateway(), new RecordingOutbox(), new MemoryUnitOfWork());
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => handler.Handle(new HandlePaymentWebhookCommand { Provider = "Stripe", Payload = "signed-body" }, default));
        Assert.Equal("Pending", payments.Items[0].Status);
    }
}
