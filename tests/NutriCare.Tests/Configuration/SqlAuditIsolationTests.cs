namespace NutriCare.Tests.Configuration;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NutriCare.API.Middleware;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Persistence.Contexts;

[Trait("Category","SqlIntegration")]
public class SqlAuditIsolationTests(IsolatedSqlDatabase fixture):IClassFixture<IsolatedSqlDatabase>
{
    [Fact]
    public async Task Failed_request_audit_does_not_commit_the_business_context_and_records_safe_outcome()
    {
        long patientId;
        await using(var initial=fixture.Open())
        {
            var email=Guid.NewGuid().ToString("N")+"@example.test";
            var patient=new Patient {User=new User {UserName=email,Email=email,FirstName="Audit",LastName="Test"},CurrentWeight=70};
            initial.Add(patient);await initial.SaveChangesAsync();patientId=patient.Id;
        }
        await using var business=fixture.Open();
        var tracked=await business.Patients.SingleAsync(p=>p.Id==patientId);
        var context=new DefaultHttpContext();context.Request.Method="POST";context.Request.Path="/api/progress/"+patientId;
        var middleware=new AuditLoggingMiddleware(_=>
        {
            tracked.CurrentWeight=120;
            throw new ValidationException("Private business failure detail");
        },NullLogger<AuditLoggingMiddleware>.Instance);
        await Assert.ThrowsAsync<ValidationException>(()=>middleware.InvokeAsync(context,new SeparateContexts(fixture)));
        await using var verify=fixture.Open();
        Assert.Equal(70,(await verify.Patients.SingleAsync(p=>p.Id==patientId)).CurrentWeight);
        var audit=await verify.AuditLogs.SingleAsync();Assert.False(audit.IsSuccess);Assert.Equal(400,audit.StatusCode);
        Assert.Equal(patientId,audit.EntityId);Assert.Equal(nameof(ValidationException),audit.ErrorMessage);
        Assert.DoesNotContain("Private business failure",audit.Description);
    }
    private sealed class SeparateContexts(IsolatedSqlDatabase fixture):IDbContextFactory<NutriCareDbContext>
    {
        public NutriCareDbContext CreateDbContext()=>fixture.Open();
        public Task<NutriCareDbContext> CreateDbContextAsync(CancellationToken cancellationToken=default)=>Task.FromResult(fixture.Open());
    }
}
