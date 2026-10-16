namespace NutriCare.Tests.Integrations;

using System.Collections.Concurrent;
using System.Text.Json;
using NutriCare.Application.Commands.Payments;
using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Interfaces;

public sealed class CancellationRefundTests
{
    [Theory]
    [InlineData(25, true, "ApprovedEarlyPatientCancellation")]
    [InlineData(24, true, "ApprovedEarlyPatientCancellation")]
    [InlineData(23, false, "ShortNoticeRescheduleOnly")]
    [InlineData(6, false, "ShortNoticeRescheduleOnly")]
    [InlineData(5, false, "LateCancellationNoAutomaticRefund")]
    public void Patient_eligibility_uses_the_recorded_cancellation_time(int notice, bool expected, string reason)
    {
        var f = new RefundFixture(); var at = DateTime.UtcNow;
        f.Appointment.AppointmentDateTime = at.AddHours(notice);
        var result = CancellationRefundOutbox.Evaluate(f.Appointment, "Patient", at);
        Assert.Equal(expected, result.Eligible); Assert.Equal(reason, result.Reason);
    }

    [Fact]
    public void Provider_cancellation_is_refundable_at_short_notice_but_no_show_is_never_automatic()
    {
        var f = new RefundFixture(); var now = DateTime.UtcNow;
        f.Appointment.AppointmentDateTime = now.AddHours(1);
        Assert.True(CancellationRefundOutbox.Evaluate(f.Appointment, "Nutritionist", now).Eligible);
        f.Appointment.Status = "No-show";
        Assert.False(CancellationRefundOutbox.Evaluate(f.Appointment, "Nutritionist", now).Eligible);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("approval")]
    [InlineData("version")]
    [InlineData("effective")]
    public void Unapproved_or_untraceable_policy_never_authorizes_an_automatic_refund(string invalid)
    {
        var f = new RefundFixture(); var policy = JsonSerializer.Deserialize<CancellationPolicy>(f.Appointment.CancellationPolicySnapshotJson!)!;
        switch (invalid)
        {
            case "draft": policy.IsPublished = false; policy.Status = "Draft"; break;
            case "approval": policy.ApprovedByUserId = null; break;
            case "version": policy.Version++; break;
            case "effective": policy.EffectiveFrom = f.Appointment.CreatedAt.AddDays(1); break;
        }
        f.Appointment.CancellationPolicySnapshotJson = JsonSerializer.Serialize(policy);
        Assert.Null(CancellationRefundOutbox.Evaluate(f.Appointment, "Patient", f.Appointment.CancelledAt!.Value).Eligible);
    }

    [Fact]
    public async Task Lost_provider_response_recovers_one_refund_without_another_post()
    {
        var f = new RefundFixture(); f.Gateway.LoseNextResponse = true;
        var first = await f.Workflow().RefundAsync(f.Request(), default);
        Assert.Equal("Pending", first.Status); Assert.Equal(0, f.Payment.RefundedAmount);
        var retry = await f.Workflow().RefundAsync(f.Request(), default);
        Assert.Equal("Completed", retry.Status); Assert.Equal(1, f.Gateway.Posts);
        Assert.Single(f.Refunds.Items); Assert.Equal(2500, f.Payment.RefundedAmount);
    }

    [Fact]
    public async Task Known_pending_refund_is_read_until_confirmed_without_another_post()
    {
        var f = new RefundFixture(); f.Gateway.Status = "pending";
        Assert.Equal("Pending", (await f.Workflow().RefundAsync(f.Request(), default)).Status);
        Assert.Equal(0, f.Payment.RefundedAmount);
        f.Gateway.Status = "succeeded";
        Assert.Equal("Completed", (await f.Workflow().RefundAsync(f.Request(), default)).Status);
        Assert.Equal(1, f.Gateway.Posts);
    }

    [Fact]
    public async Task Unknown_submission_older_than_23_hours_stays_reserved_for_review()
    {
        var f = new RefundFixture();
        var refund = new PaymentRefund { Id = 1, PaymentId = f.Payment.Id, Amount = 2500, Currency = "PKR", Status = "Pending", IdempotencyKey = f.Request().IdempotencyKey,
            CreatedAt = DateTime.UtcNow.AddHours(-25), ProviderRequestStartedAt = DateTime.UtcNow.AddHours(-25) };
        f.Refunds.Items.Add(refund);
        await f.Workflow().ReconcileAsync(f.Payment.Id, refund.Id, default);
        Assert.Equal(0, f.Gateway.Posts); Assert.Equal("Pending", refund.Status);
        Assert.Equal("ExpiredIdempotencyWindow", refund.LastErrorCode); Assert.Equal(0, f.Payment.RefundedAmount);
    }

    [Fact]
    public async Task Signed_completion_wins_over_a_stale_pending_provider_read()
    {
        var f = new RefundFixture(); f.Gateway.Status = "pending";
        await f.Workflow().RefundAsync(f.Request(), default);
        var refund = f.Refunds.Items.Single();
        f.Gateway.AfterRead = () =>
        {
            refund.Status = "Completed"; refund.RefundedAt = DateTime.UtcNow;
            f.Payment.RefundedAmount = 2500; f.Payment.Status = "Refunded";
        };
        await f.Workflow().ReconcileAsync(f.Payment.Id, refund.Id, default);
        Assert.Equal("Completed", refund.Status); Assert.Equal("Refunded", f.Payment.Status);
        Assert.Equal(2500, f.Payment.RefundedAmount); Assert.Equal(1, f.Gateway.Posts);
    }

    [Fact]
    public async Task Payment_identity_or_currency_mismatch_never_marks_money_refunded()
    {
        var f = new RefundFixture();
        f.Gateway.ReadOverride = new("re_other", "pi_other", 2500, "USD", "succeeded", "nc-refund-1-refund-policy-test-0001");
        f.Refunds.Items.Add(new PaymentRefund { Id = 1, PaymentId = f.Payment.Id, Amount = 2500, Currency = "PKR", Status = "Pending", IdempotencyKey = f.Request().IdempotencyKey, ProviderRefundId = "re_other" });
        await f.Workflow().ReconcileAsync(f.Payment.Id, 1, default);
        Assert.Equal(0, f.Gateway.Posts); Assert.Equal(0, f.Payment.RefundedAmount);
        Assert.Equal("ProviderRefundMismatch", f.Refunds.Items.Single().LastErrorCode);
    }

    [Fact]
    public async Task Active_worker_lease_prevents_duplicate_processing_and_expired_lease_recovers()
    {
        var f = new RefundFixture(); await f.StageAsync();
        var instruction = f.Instructions.Items.Single();
        instruction.Status = "Processing"; instruction.LeaseToken = "crashed-worker"; instruction.LeaseExpiresAt = DateTime.UtcNow.AddMinutes(5);
        await f.Processor().ProcessAsync(instruction.Id, default);
        Assert.Equal(0, f.Gateway.Posts);
        instruction.LeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1); instruction.NextAttemptAt = DateTime.UtcNow.AddMinutes(-1);
        await f.Processor().ProcessAsync(instruction.Id, default);
        Assert.Equal("Completed", instruction.Status); Assert.Equal(1, f.Gateway.Posts);
    }

    [Fact]
    public async Task Cancellation_before_capture_waits_and_a_later_capture_wakes_the_same_instruction()
    {
        var f = new RefundFixture(); f.Payment.Status = "Pending"; await f.StageAsync();
        var instruction = f.Instructions.Items.Single();
        await f.Processor().ProcessAsync(instruction.Id, default);
        Assert.Equal("AwaitingSettlement", instruction.Status); Assert.Equal(0, f.Gateway.Posts);
        f.Payment.Status = "Completed"; await f.Outbox.WakeAsync(f.Appointment.Id);
        await f.Processor().ProcessAsync(instruction.Id, default);
        Assert.Equal("Completed", instruction.Status); Assert.Single(f.Refunds.Items); Assert.Single(f.Instructions.Items);
    }

    [Fact]
    public async Task Every_captured_payment_is_refunded_including_a_capture_after_prior_completion()
    {
        var f = new RefundFixture(); await f.StageAsync(); var instruction = f.Instructions.Items.Single();
        await f.Processor().ProcessAsync(instruction.Id, default);
        Assert.Equal("Completed", instruction.Status);
        f.Payments.Items.Add(new Payment { Id = 2, PatientId = 10, Amount = 2500, Currency = "PKR", PaymentMethod = "Stripe", ProviderPaymentId = "pi_late", Status = "Completed", RelatedToType = "Appointment", RelatedToId = 80 });
        await f.Outbox.WakeAsync(f.Appointment.Id); await f.Processor().ProcessAsync(instruction.Id, default);
        Assert.Equal("Completed", instruction.Status); Assert.Equal(2, f.Gateway.Posts);
        Assert.All(f.Payments.Items, p => Assert.Equal(p.Amount, p.RefundedAmount));
        Assert.Equal(2, f.Refunds.Items.Count);
    }

    [Fact]
    public async Task Existing_manual_reservation_is_preserved_and_only_uncovered_money_is_automatically_reserved()
    {
        var f = new RefundFixture(); f.Gateway.Status = "pending";
        await f.Workflow().RefundAsync(new RefundPaymentCommand { PaymentId = 1, Amount = 1000, IdempotencyKey = "manual-partial-refund-001" }, default);
        await f.StageAsync(); await f.Processor().ProcessAsync(f.Instructions.Items.Single().Id, default);
        Assert.Equal(2500, f.Refunds.Items.Where(r => r.Status == "Pending").Sum(r => r.Amount));
        Assert.Equal(1000, f.Refunds.Items.Single(r => r.IdempotencyKey == "manual-partial-refund-001").Amount);
        Assert.Equal(1500, f.Refunds.Items.Single(r => r.IdempotencyKey!.StartsWith("cancel-", StringComparison.Ordinal)).Amount);
        Assert.Equal(0, f.Payment.RefundedAmount);
        Assert.Equal("AwaitingProvider", f.Instructions.Items.Single().Status);
    }

    [Fact]
    public async Task Admin_read_only_reconciliation_never_creates_a_new_provider_refund()
    {
        var f = new RefundFixture(); await f.StageAsync();
        await f.Processor().ProcessAsync(f.Instructions.Items.Single().Id, default, readOnlyRecovery: true);
        Assert.Equal(0, f.Gateway.Posts); Assert.Empty(f.Refunds.Items);
        Assert.Equal("NeedsReview", f.Instructions.Items.Single().Status);
    }

    internal sealed class RefundFixture
    {
        public MemoryRepository<Payment> Payments { get; } = new();
        public MemoryRepository<PaymentRefund> Refunds { get; } = new();
        public MemoryRepository<Appointment> Appointments { get; } = new();
        public MemoryRepository<CancellationRefundInstruction> Instructions { get; } = new();
        public MemoryRepository<Patient> Patients { get; } = new();
        public MemoryRepository<Nutritionist> Nutritionists { get; } = new();
        public MemoryRepository<User> Users { get; } = new();
        public MemoryRepository<AuditLog> Audits { get; } = new();
        public RecordingOutbox Notifications { get; } = new();
        public RefundGateway Gateway { get; } = new();
        public MemoryUnitOfWork Unit { get; } = new();
        public Payment Payment => Payments.Items[0];
        public Appointment Appointment => Appointments.Items[0];
        public CancellationRefundOutbox Outbox => new(Instructions, Audits);

        public RefundFixture()
        {
            var now = DateTime.UtcNow;
            var policy = new CancellationPolicy { Id = 70, Version = 1, IsPublished = true, Status = "Published", ApprovedByUserId = 2,
                ApprovedAt = now.AddDays(-2), EffectiveFrom = now.AddDays(-2), FullRefundHours = 24, FreeRescheduleHours = 6, ProviderCancellationRefund = true };
            Appointments.Items.Add(new Appointment { Id = 80, PatientId = 10, NutritionistId = 20, CreatedAt = now.AddDays(-1), AppointmentDateTime = now.AddDays(7),
                Status = "Cancelled", CancelledAt = now, CancelledBy = "Patient", CancellationPolicyId = 70, CancellationPolicyVersion = 1,
                CancellationPolicySnapshotJson = JsonSerializer.Serialize(policy), PriceSnapshot = 2500, Currency = "PKR", IsPaid = true });
            Payments.Items.Add(new Payment { Id = 1, PatientId = 10, Amount = 2500, Currency = "PKR", PaymentMethod = "Stripe", ProviderPaymentId = "pi_test", Status = "Completed", RelatedToType = "Appointment", RelatedToId = 80 });
            Patients.Items.Add(new Patient { Id = 10, UserId = 1 }); Nutritionists.Items.Add(new Nutritionist { Id = 20, UserId = 2 });
            Users.Items.AddRange([new User { Id = 1, Email = "patient@example.invalid" }, new User { Id = 2 }]);
        }
        public Task StageAsync() => Outbox.StageAsync(Appointment, 1, "Patient", Appointment.CancelledAt!.Value);
        public RefundPaymentCommand Request() => new() { PaymentId = Payment.Id, Amount = 2500, IdempotencyKey = "refund-policy-test-0001" };
        public PaymentRefundWorkflow Workflow() => new(Payments, Refunds, new MemoryRepository<Invoice>(), Appointments, Gateway, Gateway, Unit, Audits);
        public CancellationRefundProcessor Processor() => new(Instructions, Appointments, Payments, Refunds, Patients, Nutritionists, Users, Audits, Workflow(), Notifications, Unit);
    }

    internal sealed class RefundGateway : IPaymentGatewayService, IPaymentRefundReconciliation
    {
        public ConcurrentDictionary<string, PaymentGatewayRefundState> States { get; } = new();
        private int posts;
        public int Posts => posts;
        public string Status { get; set; } = "succeeded";
        public bool LoseNextResponse { get; set; }
        public PaymentGatewayRefundState? ReadOverride { get; set; }
        public Action? AfterRead { get; set; }
        public Action? AssertOutsideTransaction { get; set; }
        public Task<PaymentGatewayInitializeResult> InitializePaymentAsync(string provider, PaymentGatewayInitializeRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No checkout in refund tests");
        public Task<PaymentGatewayWebhookResult> ParseWebhookAsync(string provider, string payload, string? signature, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No external callbacks in this test gateway");
        public Task<PaymentGatewayRefundResult> RefundAsync(string provider, string providerPaymentId, decimal amount, string currency, string? reason, CancellationToken cancellationToken = default, string? idempotencyKey = null)
        {
            AssertOutsideTransaction?.Invoke();
            var count = Interlocked.Increment(ref posts);
            var state = new PaymentGatewayRefundState("re_test_" + count, providerPaymentId, amount, currency, Status, idempotencyKey);
            States[idempotencyKey!] = state;
            if (LoseNextResponse) { LoseNextResponse = false; throw new TimeoutException("Test response lost after provider accepted refund"); }
            return Task.FromResult(new PaymentGatewayRefundResult(state.ProviderRefundId, state.Status, "{}"));
        }
        public Task<PaymentGatewayRefundState?> FindAsync(string provider, string providerPaymentId, string operationKey, string? providerRefundId = null, CancellationToken cancellationToken = default)
        {
            AssertOutsideTransaction?.Invoke();
            var result = ReadOverride ?? (States.TryGetValue(operationKey, out var state) ? state with { Status = Status } : null);
            AfterRead?.Invoke(); return Task.FromResult(result);
        }
    }
}
