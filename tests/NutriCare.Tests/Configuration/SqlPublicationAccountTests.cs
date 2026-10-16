namespace NutriCare.Tests.Configuration;

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.API.Controllers;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Configuration;
using NutriCare.Persistence.Repositories;

[Trait("Category","SqlIntegration")]
public class SqlPublicationAccountTests(IsolatedSqlDatabase fixture):IClassFixture<IsolatedSqlDatabase>
{
    [Fact]
    public async Task Unconfirmed_provider_cannot_publish_and_deactivation_hides_an_approved_service()
    {
        await using var db=fixture.Open();var suffix=Guid.NewGuid().ToString("N");
        var clinician=new Nutritionist {LicenseNumber=suffix,IsVerified=true,User=new User {UserName=suffix+"@example.test",Email=suffix+"@example.test",FirstName="Test",LastName="Clinician",EmailConfirmed=false}};
        db.Add(clinician);await db.SaveChangesAsync();
        static JsonElement Input(object value)=>JsonSerializer.SerializeToElement(value,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var store=new ConfigurationStore(db,new Staff(),new AppointmentReservationLock(db));
        var service=(Service)await store.SaveAsync("services",null,Input(new {name="Verified service",nameUrdu="مشاورت",durationMinutes=45,price=2500,currency="PKR",mode="Online",nutritionistId=clinician.Id,effectiveFrom=DateTime.UtcNow.AddMinutes(-1)}));
        await Assert.ThrowsAsync<ValidationException>(()=>store.PublishAsync("services",service.Id,Input(new {approvalConfirmed=true,expectedVersion=service.Version}),true));
        clinician.User!.EmailConfirmed=true;await db.SaveChangesAsync();
        await store.PublishAsync("services",service.Id,Input(new {approvalConfirmed=true,expectedVersion=service.Version}),true);
        var catalog=new PublicCatalogController(db);
        static IEnumerable<Service> Rows(IActionResult result)=>(IEnumerable<Service>)Assert.IsType<ApiResponse<object>>(Assert.IsType<OkObjectResult>(result).Value).Data!;
        Assert.Single(Rows(await catalog.Services(default)));
        clinician.User.IsActive=false;await db.SaveChangesAsync();
        Assert.Empty(Rows(await catalog.Services(default)));
        Assert.True((await db.Services.SingleAsync()).IsPublished); // Hiding availability does not silently rewrite approved configuration.
    }
    private sealed class Staff:ICurrentUserService
    {public long UserId=>1;public string Email=>"staff@example.test";public string Role=>"Admin";public bool IsAuthenticated=>true;public bool CanAccessClinicalData=>false;}
}
