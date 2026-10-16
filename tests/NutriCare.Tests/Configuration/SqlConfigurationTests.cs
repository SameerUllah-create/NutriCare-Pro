namespace NutriCare.Tests.Configuration;

using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Configuration;
using NutriCare.Persistence.Contexts;
using NutriCare.Persistence.Repositories;

public sealed class IsolatedSqlDatabase : IAsyncLifetime
{
    public string ConnectionString {get;private set;}="";
    private readonly string database="NutriCareTest_"+Guid.NewGuid().ToString("N");
    public NutriCareDbContext Open()=>new(new DbContextOptionsBuilder<NutriCareDbContext>().UseSqlServer(ConnectionString).Options);
    public async Task InitializeAsync()
    {
        var builder=new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("NUTRICARE_TEST_SQL_CONNECTION")??"Server=localhost;Integrated Security=true;Encrypt=true;TrustServerCertificate=true");
        builder.InitialCatalog=database;ConnectionString=builder.ConnectionString;
        await using var db=Open();await db.Database.MigrateAsync();
    }
    public async Task DisposeAsync()
    {
        var target=new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
        if(target!=database || !target.StartsWith("NutriCareTest_",StringComparison.Ordinal) || target.Length!=46)throw new InvalidOperationException("Refusing cleanup outside generated test database");
        await using var db=Open();await db.Database.EnsureDeletedAsync();
    }
}

[Trait("Category","SqlIntegration")]
public class SqlConfigurationTests(IsolatedSqlDatabase fixture):IClassFixture<IsolatedSqlDatabase>
{
    private static JsonElement Input(object value)=>JsonSerializer.SerializeToElement(value,new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static ConfigurationStore Store(NutriCareDbContext db,string role="Admin",long userId=1)=>new(db,new Actor(role,userId),new AppointmentReservationLock(db));

    [Fact]
    public async Task Seed_is_complete_draft_only_and_does_not_restore_edited_proposals()
    {
        await using var db=fixture.Open();await ProposedConfigurationSeeder.SeedAsync(db);
        Assert.Equal(10,await db.Services.CountAsync());Assert.Equal(3,await db.ServicePackages.CountAsync(p=>p.Name=="Starter" || p.Name=="Monthly" || p.Name=="Transformation"));Assert.Equal(6,await db.AvailabilityRules.CountAsync());
        Assert.False(await db.Services.AnyAsync(s=>s.IsPublished || s.IsActive));Assert.False(await db.ServicePackages.AnyAsync(s=>(s.Name=="Starter" || s.Name=="Monthly" || s.Name=="Transformation") && (s.IsPublished || s.IsActive)));Assert.False(await db.AvailabilityRules.AnyAsync(s=>s.IsPublished));
        Assert.False(await db.AvailabilityRules.AnyAsync(r=>r.DayOfWeek==DayOfWeek.Sunday));
        var plan=await db.Services.SingleAsync(s=>s.Name=="Personalized Diet Plan");Assert.Equal(48,plan.DeliveryHours);Assert.Equal(0,plan.DurationMinutes);Assert.Equal(2000,plan.Price);
        var policy=await db.CancellationPolicies.SingleAsync();Assert.Equal(24,policy.FullRefundHours);Assert.Equal(6,policy.FreeRescheduleHours);Assert.Equal(1,policy.MaxFreeReschedules);Assert.False(policy.IsPublished);
        plan.Name="Reviewed custom draft";await db.SaveChangesAsync();await ProposedConfigurationSeeder.SeedAsync(db);Assert.Equal(10,await db.Services.CountAsync());
        Assert.True(await db.ConfigurationRevisions.AnyAsync(h=>h.Action=="Seeded proposed draft"));
    }

    [Fact]
    public async Task Publication_requires_approval_effective_date_and_has_immutable_history()
    {
        await using var db=fixture.Open();
        var administrator=new User {FirstName="Configuration",LastName="Admin",Email=Guid.NewGuid()+"@example.test",IsActive=true,EmailConfirmed=true};
        var nutritionist=new User {FirstName="Verified",LastName="Nutritionist",Email=Guid.NewGuid()+"@example.test",IsActive=true,EmailConfirmed=true};
        db.Users.AddRange(administrator,nutritionist);await db.SaveChangesAsync();
        db.Nutritionists.Add(new Nutritionist {UserId=nutritionist.Id,IsVerified=true,LicenseNumber="CONFIG-"+Guid.NewGuid().ToString("N")});await db.SaveChangesAsync();
        var store=Store(db,"Admin",administrator.Id);var proposal=PackageConfigurationRulesTests.Complete();proposal.Name="Test package";proposal.EffectiveFrom=null;
        var row=(ServicePackage)await store.SaveAsync("packages",null,Input(proposal));
        Assert.False(row.IsPublished);
        await Assert.ThrowsAsync<ValidationException>(()=>store.PublishAsync("packages",row.Id,Input(new{expectedVersion=row.Version,approvalConfirmed=false}),true));
        await Assert.ThrowsAsync<ValidationException>(()=>store.PublishAsync("packages",row.Id,Input(new{expectedVersion=row.Version,approvalConfirmed=true}),true));
        await store.SaveAsync("packages",row.Id,Input(new{expectedVersion=row.Version,effectiveFrom=DateTime.UtcNow.AddDays(1)}));
        await Store(db,"Nutritionist",nutritionist.Id).ApprovePackageAsync(row.Id,Input(new{expectedVersion=row.Version,approvalConfirmed=true}));
        await store.PublishAsync("packages",row.Id,Input(new{expectedVersion=row.Version,approvalConfirmed=true}),true);
        Assert.True(row.IsPublished);Assert.Equal(administrator.Id,row.ApprovedByUserId);
        await Assert.ThrowsAsync<ConflictException>(()=>store.SaveAsync("packages",row.Id,Input(new{expectedVersion=row.Version,price=1})));
        var draft=(ServicePackage)await store.CreateRevisionAsync("packages",row.Id);Assert.False(draft.IsPublished);Assert.True(row.IsPublished);Assert.NotEqual(row.Id,draft.Id);
        Assert.True(await db.ConfigurationRevisions.CountAsync(h=>h.EntityType=="packages" && h.EntityId==row.Id)>=3);
    }

    [Fact]
    public async Task Separate_context_stale_version_is_rejected_by_sql_rowversion()
    {
        long id;byte[] stale;
        await using(var first=fixture.Open()) {var row=await Store(first).SaveAsync("packages",null,Input(new{name="Concurrency package",nameUrdu="پیکیج",durationWeeks=4,price=7500,currency="PKR"}));id=row.Id;stale=row.RowVersion;}
        await using(var second=fixture.Open())await Store(second).SaveAsync("packages",id,Input(new{rowVersion=stale,price=7600}));
        await using var third=fixture.Open();await Assert.ThrowsAsync<ConflictException>(()=>Store(third).SaveAsync("packages",id,Input(new{rowVersion=stale,price=100})));
    }

    [Fact]
    public async Task Patients_cannot_change_practice_configuration()
    {
        await using var db=fixture.Open();await Assert.ThrowsAsync<ForbiddenAccessException>(()=>Store(db,"Patient",8).SaveAsync("packages",null,Input(new{name="Unsafe",durationWeeks=2,price=1})));
    }

    private sealed class Actor(string role,long id):ICurrentUserService
    {public bool CanAccessClinicalData=>false;public long UserId=>id;public string Email=>"test@example.invalid";public string Role=>role;public bool IsAuthenticated=>true;}
}
