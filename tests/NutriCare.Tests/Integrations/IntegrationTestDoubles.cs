namespace NutriCare.Tests.Integrations;

using System.Reflection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NutriCare.Domain.Interfaces;

internal sealed class MemoryRepository<T> : IRepository<T> where T : class
{
    public List<T> Items { get; } = [];
    public Task<T?> GetByIdAsync(long id) => Task.FromResult(Items.SingleOrDefault(x => (long?)x.GetType().GetProperty("Id")?.GetValue(x) == id));
    public Task<IEnumerable<T>> GetAllAsync() => Task.FromResult<IEnumerable<T>>(Items.ToArray());
    public Task<IEnumerable<T>> GetAsync(Func<IQueryable<T>, IQueryable<T>>? predicate = null) => Task.FromResult<IEnumerable<T>>((predicate?.Invoke(Items.AsQueryable()) ?? Items.AsQueryable()).ToArray());
    public Task<T> AddAsync(T entity)
    {
        var id = entity.GetType().GetProperty("Id");
        if (id != null && (long?)id.GetValue(entity) == 0) id.SetValue(entity, Items.Count + 1L);
        Items.Add(entity); return Task.FromResult(entity);
    }
    public Task<T> UpdateAsync(T entity) => Task.FromResult(entity);
    public async Task<bool> DeleteAsync(long id) { var entity = await GetByIdAsync(id); return entity != null && Items.Remove(entity); }
    public async Task<bool> ExistsAsync(long id) => await GetByIdAsync(id) != null;
}

internal sealed class MemoryUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync() => Task.FromResult(1);
    public Task<bool> BeginTransactionAsync(string? resource = null) => Task.FromResult(true);
    public Task<bool> CommitAsync() => Task.FromResult(true);
    public Task<bool> RollbackAsync() => Task.FromResult(true);
    public void Dispose() { }
}

internal sealed class FakeEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Production;
    public string ApplicationName { get; set; } = "IntegrationTests";
    public string ContentRootPath { get; set; } = Path.GetTempPath();
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed class NoNetworkClients : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => throw new InvalidOperationException("A security test attempted network access");
}

internal sealed class RecordingOutbox : INotificationOutbox
{
    public List<string> Events { get; } = [];
    public Task EnqueueAsync(NotificationRecipient recipient, NotificationRequest request, string eventKey, CancellationToken cancellationToken = default)
    { Events.Add(eventKey); return Task.CompletedTask; }
}

internal sealed class FakePaymentGateway : IPaymentGatewayService, IPaymentRefundReconciliation
{
    private readonly Dictionary<string, PaymentGatewayRefundState> refundStates = new();
    public int Initializes { get; private set; }
    public PaymentGatewayWebhookResult Webhook { get; set; } = new("transaction", "pi_test", "completed", "{}", "evt_test", "checkout.session.completed", 5000m, "PKR", null, "cs_test");
    public string RefundStatus { get; set; } = "pending";
    public Task<PaymentGatewayInitializeResult> InitializePaymentAsync(string provider, PaymentGatewayInitializeRequest request, CancellationToken cancellationToken = default)
    { Initializes++; return Task.FromResult(new PaymentGatewayInitializeResult("cs_test", "https://checkout.stripe.com/test", "pending", "{}", "pi_test")); }
    public Task<PaymentGatewayRefundResult> RefundAsync(string provider, string providerPaymentId, decimal amount, string currency, string? reason, CancellationToken cancellationToken = default, string? idempotencyKey = null)
    {
        var state = new PaymentGatewayRefundState("re_test_" + (refundStates.Count + 1), providerPaymentId, amount, currency, RefundStatus, idempotencyKey);
        refundStates[idempotencyKey!] = state;
        return Task.FromResult(new PaymentGatewayRefundResult(state.ProviderRefundId, RefundStatus, "{}"));
    }
    public Task<PaymentGatewayRefundState?> FindAsync(string provider, string providerPaymentId, string operationKey, string? providerRefundId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(refundStates.TryGetValue(operationKey, out var state) ? state with { Status = RefundStatus } : null);
    public Task<PaymentGatewayWebhookResult> ParseWebhookAsync(string provider, string payload, string? signature, CancellationToken cancellationToken = default) => Task.FromResult(Webhook);
}
