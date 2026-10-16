namespace NutriCare.Tests.Integrations;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NutriCare.Application.Commands.Payments;
using NutriCare.Application.Handlers.Payments;
using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Infrastructure.Notifications;
using NutriCare.Persistence.Contexts;
using NutriCare.Persistence.Repositories;
using NutriCare.Tests.Configuration;

[Trait("Category", "SqlIntegration")]
public sealed class SqlCancellationRefundTests(IsolatedSqlDatabase fixture) : IClassFixture<IsolatedSqlDatabase>
{
    [Fact]
    public async Task Concurrent_manual_and_automatic_refunds_never_reserve_or_refund_more_than_capture()
    {
        var seed = await SeedAsync(); var provider = new CancellationRefundTests.RefundGateway();
        var manual = Task.Run(async () =>
        {
            await using var db = fixture.Open(); var gateway = new ScopedGateway(provider, db);
            try
            {
                await Workflow(db, gateway).RefundAsync(new RefundPaymentCommand { PaymentId = seed.PaymentId, Amount = 1000, IdempotencyKey = "manual-race-" + Guid.NewGuid().ToString("N") }, default);
                return true;
            }
            catch (ValidationException) { return false; }
            catch (ConflictException) { return false; }
        });
        var automatic = Task.Run(async () =>
        {
            await using var db = fixture.Open(); await Processor(db, new ScopedGateway(provider, db)).ProcessAsync(seed.InstructionId, default);
        });
        await Task.WhenAll(manual, automatic);
        // A manual provider response may finish after the worker's first summary. Wake and
        // rescan, using the same durable rows rather than another provider operation key.
        await using (var wake = fixture.Open())
        {
            await new CancellationRefundOutbox(new Repository<CancellationRefundInstruction>(wake), new Repository<AuditLog>(wake)).WakeAsync(seed.AppointmentId);
            await wake.SaveChangesAsync();
        }
        await using (var retry = fixture.Open()) await Processor(retry, new ScopedGateway(provider, retry)).ProcessAsync(seed.InstructionId, default);
        await using var verify = fixture.Open();
        var rows = await verify.PaymentRefunds.Where(r => r.PaymentId == seed.PaymentId).ToArrayAsync();
        Assert.Equal(2500, rows.Where(r => r.Status == "Completed").Sum(r => r.Amount));
        Assert.True(rows.Where(r => r.Status is "Pending" or "Completed").Sum(r => r.Amount) <= 2500);
        Assert.Equal(2500, (await verify.Payments.SingleAsync(p => p.Id == seed.PaymentId)).RefundedAmount);
        Assert.Equal("Completed", (await verify.CancellationRefundInstructions.SingleAsync(i => i.Id == seed.InstructionId)).Status);
    }

    [Fact]
    public async Task Two_workers_share_a_durable_lease_while_provider_io_holds_no_sql_transaction()
    {
        var seed = await SeedAsync(); var provider = new CancellationRefundTests.RefundGateway();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var firstDb = fixture.Open();
        var first = Processor(firstDb, new ScopedGateway(provider, firstDb, entered, release)).ProcessAsync(seed.InstructionId, default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await using var secondDb = fixture.Open();
            await Processor(secondDb, new ScopedGateway(provider, secondDb)).ProcessAsync(seed.InstructionId, default);
        }
        finally { release.TrySetResult(); }
        await first;
        Assert.Equal(1, provider.Posts);
        await using var verify = fixture.Open();
        Assert.Equal(1, await verify.PaymentRefunds.CountAsync(r => r.PaymentId == seed.PaymentId));
        Assert.Equal("Completed", (await verify.CancellationRefundInstructions.SingleAsync(i => i.Id == seed.InstructionId)).Status);
    }

    [Fact]
    public async Task Signed_capture_after_cancellation_wakes_the_existing_instruction_and_refunds_once()
    {
        var seed = await SeedAsync(pending: true); var provider = new CancellationRefundTests.RefundGateway();
        await using (var waiting = fixture.Open()) await Processor(waiting, new ScopedGateway(provider, waiting)).ProcessAsync(seed.InstructionId, default);
        Assert.Equal(0, provider.Posts);
        await using (var callback = fixture.Open())
        {
            var payment = await callback.Payments.SingleAsync(p => p.Id == seed.PaymentId);
            var unit = new UnitOfWork(callback); var users = new Repository<User>(callback);
            var gateway = new FakePaymentGateway { Webhook = new(payment.TransactionId, payment.ProviderPaymentId, "completed", "{}", "evt_" + Guid.NewGuid().ToString("N"),
                "checkout.session.completed", payment.Amount, payment.Currency, null, payment.ProviderSessionId) };
            var outbox = new NotificationService(new Repository<Notification>(callback), users, unit, new ConfigurationBuilder().Build());
            var cancellations = new CancellationRefundOutbox(new Repository<CancellationRefundInstruction>(callback), new Repository<AuditLog>(callback));
            var handler = new HandlePaymentWebhookCommandHandler(new Repository<Payment>(callback), new Repository<Invoice>(callback), new Repository<ProviderWebhookEvent>(callback),
                new Repository<PaymentRefund>(callback), new Repository<Appointment>(callback), new Repository<Patient>(callback), users, gateway, outbox, unit, cancellations);
            var request = new HandlePaymentWebhookCommand { Provider = "Stripe", Payload = "isolated-signed-capture-" + seed.PaymentId };
            await handler.Handle(request, default); await handler.Handle(request, default);
        }
        await using (var refund = fixture.Open()) await Processor(refund, new ScopedGateway(provider, refund)).ProcessAsync(seed.InstructionId, default);
        await using var verify = fixture.Open();
        Assert.Equal(1, provider.Posts);
        Assert.Equal(1, await verify.CancellationRefundInstructions.CountAsync(i => i.AppointmentId == seed.AppointmentId));
        Assert.Equal(1, await verify.PaymentRefunds.CountAsync(r => r.PaymentId == seed.PaymentId));
        Assert.Equal("Refunded", (await verify.Appointments.SingleAsync(a => a.Id == seed.AppointmentId)).PaymentStatus);
        Assert.Equal("Completed", (await verify.CancellationRefundInstructions.SingleAsync(i => i.Id == seed.InstructionId)).Status);
    }

    private async Task<Seed> SeedAsync(bool pending = false)
    {
        await using var db = fixture.Open(); var token = Guid.NewGuid().ToString("N"); var now = DateTime.UtcNow;
        var patientUser = new User { UserName = token + "@example.invalid", Email = token + "@example.invalid", EmailConfirmed = true, EmailNotificationsEnabled = false };
        var clinicianUser = new User { UserName = "clinician-" + token + "@example.invalid", Email = "clinician-" + token + "@example.invalid", EmailConfirmed = true };
        db.Users.AddRange(patientUser, clinicianUser); await db.SaveChangesAsync();
        var patient = new Patient { UserId = patientUser.Id };
        var clinician = new Nutritionist { UserId = clinicianUser.Id, IsVerified = true, LicenseNumber = "TEST-" + token };
        var policy = new CancellationPolicy { Name = "Isolated cancellation test", IsPublished = true, Status = "Published", Version = 1,
            ApprovedByUserId = clinicianUser.Id, ApprovedAt = now.AddDays(-1), EffectiveFrom = now.AddDays(-1), FullRefundHours = 24, FreeRescheduleHours = 6, ProviderCancellationRefund = true };
        db.Patients.Add(patient); db.Nutritionists.Add(clinician); db.CancellationPolicies.Add(policy); await db.SaveChangesAsync();
        var appointment = new Appointment { PatientId = patient.Id, NutritionistId = clinician.Id, AppointmentDateTime = now.AddDays(7), Duration = 45, BookingBufferMinutesSnapshot = 15,
            AppointmentType = "InPerson", Status = "Cancelled", CancelledBy = "Patient", CancelledAt = now, PriceSnapshot = 2500, Currency = "PKR", IsPaid = !pending,
            CancellationPolicyId = policy.Id, CancellationPolicyVersion = policy.Version, CancellationPolicySnapshotJson = JsonSerializer.Serialize(policy) };
        db.Appointments.Add(appointment); await db.SaveChangesAsync();
        var payment = new Payment { PatientId = patient.Id, NutritionistId = clinician.Id, RelatedToType = "Appointment", RelatedToId = appointment.Id,
            Amount = 2500, Currency = "PKR", PaymentMethod = "Stripe", Status = pending ? "Pending" : "Completed", TransactionId = token, ProviderPaymentId = "pi_" + token, ProviderSessionId = "cs_" + token };
        db.Payments.Add(payment); await db.SaveChangesAsync();
        var unit = new UnitOfWork(db); await unit.BeginTransactionAsync("test-cancellation-" + appointment.Id);
        await new CancellationRefundOutbox(new Repository<CancellationRefundInstruction>(db), new Repository<AuditLog>(db)).StageAsync(appointment, patientUser.Id, "Patient", now);
        await unit.CommitAsync();
        return new(appointment.Id, payment.Id, (await db.CancellationRefundInstructions.SingleAsync(i => i.AppointmentId == appointment.Id)).Id);
    }

    private static PaymentRefundWorkflow Workflow(NutriCareDbContext db, ScopedGateway gateway) => new(new Repository<Payment>(db), new Repository<PaymentRefund>(db),
        new Repository<Invoice>(db), new Repository<Appointment>(db), gateway, gateway, new UnitOfWork(db), new Repository<AuditLog>(db));

    private static CancellationRefundProcessor Processor(NutriCareDbContext db, ScopedGateway gateway)
    {
        var unit = new UnitOfWork(db); var users = new Repository<User>(db);
        var workflow = new PaymentRefundWorkflow(new Repository<Payment>(db), new Repository<PaymentRefund>(db), new Repository<Invoice>(db), new Repository<Appointment>(db), gateway, gateway, unit, new Repository<AuditLog>(db));
        return new(new Repository<CancellationRefundInstruction>(db), new Repository<Appointment>(db), new Repository<Payment>(db), new Repository<PaymentRefund>(db),
            new Repository<Patient>(db), new Repository<Nutritionist>(db), users, new Repository<AuditLog>(db), workflow,
            new NotificationService(new Repository<Notification>(db), users, unit, new ConfigurationBuilder().Build()), unit);
    }

    private sealed record Seed(long AppointmentId, long PaymentId, long InstructionId);
    private sealed class ScopedGateway(CancellationRefundTests.RefundGateway inner, NutriCareDbContext db,
        TaskCompletionSource? entered = null, TaskCompletionSource? release = null) : IPaymentGatewayService, IPaymentRefundReconciliation
    {
        public Task<PaymentGatewayInitializeResult> InitializePaymentAsync(string provider, PaymentGatewayInitializeRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<PaymentGatewayWebhookResult> ParseWebhookAsync(string provider, string payload, string? signature, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public async Task<PaymentGatewayRefundResult> RefundAsync(string provider, string providerPaymentId, decimal amount, string currency, string? reason, CancellationToken cancellationToken = default, string? idempotencyKey = null)
        {
            Assert.Null(db.Database.CurrentTransaction);
            entered?.TrySetResult();
            if (release != null) await release.Task.WaitAsync(cancellationToken);
            return await inner.RefundAsync(provider, providerPaymentId, amount, currency, reason, cancellationToken, idempotencyKey);
        }
        public Task<PaymentGatewayRefundState?> FindAsync(string provider, string providerPaymentId, string operationKey, string? providerRefundId = null, CancellationToken cancellationToken = default)
        {
            Assert.Null(db.Database.CurrentTransaction);
            return inner.FindAsync(provider, providerPaymentId, operationKey, providerRefundId, cancellationToken);
        }
    }
}
