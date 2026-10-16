namespace NutriCare.Tests.Integrations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NutriCare.Application.Commands.Payments;
using NutriCare.Application.Handlers.Payments;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Infrastructure.Notifications;
using NutriCare.Persistence.Contexts;
using NutriCare.Persistence.Repositories;
using NutriCare.Tests.Configuration;

/// <summary>Runs only against fixture-created NutriCareTest_* databases, never an application database.</summary>
[Trait("Category", "SqlIntegration")]
public class SqlPaymentSafetyTests(IsolatedSqlDatabase fixture) : IClassFixture<IsolatedSqlDatabase>
{
    [Fact]
    public async Task ConcurrentDuplicateCallbacksSettleOnceAndQueueOneReceipt()
    {
        var token = Guid.NewGuid().ToString("N");
        long paymentId;
        await using (var db = fixture.Open())
        {
            var patient = new Patient { User = new User { UserName = token + "@example.test", Email = token + "@example.test", FirstName = "Test", LastName = "Patient", EmailNotificationsEnabled = false }, DateOfBirth = new DateTime(1990, 1, 1), Height = 170, CurrentWeight = 70 };
            db.Patients.Add(patient); await db.SaveChangesAsync();
            var payment = new Payment { PatientId = patient.Id, Amount = 5000, Currency = "PKR", PaymentMethod = "Stripe", TransactionId = token, ProviderSessionId = "cs_" + token, Status = "Pending" };
            db.Payments.Add(payment); await db.SaveChangesAsync(); paymentId = payment.Id;
        }
        async Task Process()
        {
            await using var db = fixture.Open();
            var unit = new UnitOfWork(db);
            var users = new Repository<User>(db);
            var gateway = new FakePaymentGateway { Webhook = new(token, "pi_" + token, "completed", "{}", "evt_" + token, "checkout.session.completed", 5000, "PKR", null, "cs_" + token) };
            var outbox = new NotificationService(new Repository<Notification>(db), users, unit, new ConfigurationBuilder().Build());
            var handler = new HandlePaymentWebhookCommandHandler(new Repository<Payment>(db), new Repository<Invoice>(db), new Repository<ProviderWebhookEvent>(db), new Repository<PaymentRefund>(db), new Repository<Appointment>(db), new Repository<Patient>(db), users, gateway, outbox, unit);
            await handler.Handle(new HandlePaymentWebhookCommand { Provider = "Stripe", Payload = "signed-body-" + token }, default);
        }
        await Task.WhenAll(Process(), Process());
        await using var verify = fixture.Open();
        Assert.Equal("Completed", (await verify.Payments.SingleAsync(p => p.Id == paymentId)).Status);
        Assert.Equal(1, await verify.ProviderWebhookEvents.CountAsync(e => e.PaymentId == paymentId));
        Assert.Equal(1, await verify.Notifications.CountAsync(n => n.EventKey == $"payment:{paymentId}:completed"));
    }

    [Fact]
    public async Task ConcurrentRefundsCannotReserveMoreThanSettledBalance()
    {
        long paymentId;
        var token = Guid.NewGuid().ToString("N");
        await using (var db = fixture.Open())
        {
            var patient = new Patient { User = new User { UserName = token + "@example.test", Email = token + "@example.test", FirstName = "Test", LastName = "Patient" }, DateOfBirth = new DateTime(1990, 1, 1), Height = 170, CurrentWeight = 70 };
            db.Patients.Add(patient); await db.SaveChangesAsync();
            var payment = new Payment { PatientId = patient.Id, Amount = 5000, Currency = "PKR", PaymentMethod = "Stripe", TransactionId = token, ProviderPaymentId = "pi_" + token, Status = "Completed" };
            db.Payments.Add(payment); await db.SaveChangesAsync(); paymentId = payment.Id;
        }
        async Task<bool> Refund(string suffix)
        {
            await using var db = fixture.Open();
            var handler = new RefundPaymentCommandHandler(new Repository<Payment>(db), new Repository<PaymentRefund>(db), new Repository<Invoice>(db), new Repository<Appointment>(db), new FakePaymentGateway(), new UnitOfWork(db));
            try { await handler.Handle(new RefundPaymentCommand { PaymentId = paymentId, Amount = 3000, IdempotencyKey = token + suffix }, default); return true; }
            catch (ValidationException) { return false; }
        }
        var results = await Task.WhenAll(Refund("a"), Refund("b"));
        Assert.Single(results, x => x);
        await using var verify = fixture.Open();
        Assert.Equal(3000m, await verify.PaymentRefunds.Where(r => r.PaymentId == paymentId && r.Status == "Pending").SumAsync(r => r.Amount));
        Assert.Equal(0m, (await verify.Payments.SingleAsync(p => p.Id == paymentId)).RefundedAmount);
    }
}
