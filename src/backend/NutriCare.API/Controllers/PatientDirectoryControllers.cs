namespace NutriCare.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;

[ApiController, Route("api/patients"), Authorize]
public class PatientsController(NutriCareDbContext db,IClinicalAccessService access,ICurrentUserService current,IClinicalConsentService consent):ControllerBase
{
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id,CancellationToken ct)
    {
        await access.EnsurePatientAccessAsync(id,true,ct);
        var row=await db.Patients.AsNoTracking().Where(p=>p.Id==id && p.DeletedAt==null).Select(p=>new PatientDto { Id=p.Id,UserId=p.UserId,DateOfBirth=p.DateOfBirth,Gender=p.Gender,BloodGroup=p.BloodGroup,Height=p.Height,CurrentWeight=p.CurrentWeight,TargetWeight=p.TargetWeight,MedicalHistory=p.MedicalHistory,Allergies=p.Allergies,CreatedAt=p.CreatedAt,UpdatedAt=p.UpdatedAt }).SingleOrDefaultAsync(ct)??throw new EntityNotFoundException("Patient",id);
        return Ok(ApiResponse<PatientDto>.SuccessResponse(row));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id,UpdatePatientRequest input,CancellationToken ct)
    {
        var patient=await db.Patients.SingleOrDefaultAsync(p=>p.Id==id && p.UserId==current.UserId && p.DeletedAt==null,ct)??throw new ForbiddenAccessException("Only the patient may edit this profile");
        await consent.RequireAsync(current.UserId,"HealthData",cancellationToken:ct);
        if (input.Height is <20 or >300 || input.CurrentWeight is <1 or >500 || input.TargetWeight is <1 or >500 || input.DateOfBirth>DateTime.UtcNow) throw new ArgumentException("Profile measurements or date of birth are invalid");
        if(input.Height.HasValue)patient.Height=input.Height.Value;if(input.CurrentWeight.HasValue)patient.CurrentWeight=input.CurrentWeight.Value;
        if(input.DateOfBirth.HasValue)patient.DateOfBirth=input.DateOfBirth.Value.Date;
        patient.Gender=input.Gender;patient.BloodGroup=input.BloodGroup;patient.TargetWeight=input.TargetWeight;patient.MedicalHistory=input.MedicalHistory;patient.Allergies=input.Allergies;
        await db.SaveChangesAsync(ct);return await Get(id,ct);
    }

    [HttpGet("{id:long}/weight-history")]
    public async Task<IActionResult> WeightHistory(long id,DateTime? fromDate,DateTime? toDate,CancellationToken ct)
    {
        await access.EnsurePatientAccessAsync(id,true,ct);
        return Ok(ApiResponse<object>.SuccessResponse(await db.WeightTrackings.AsNoTracking().Where(w=>w.PatientId==id && w.DeletedAt==null && (!fromDate.HasValue || w.RecordDate>=fromDate) && (!toDate.HasValue || w.RecordDate<=toDate)).OrderBy(w=>w.RecordDate).Take(1000).ToListAsync(ct)));
    }
}

[ApiController,Route("api/nutritionists")]
public class NutritionistsController(NutriCareDbContext db,IClinicalSchedulingService scheduling):ControllerBase
{
    [HttpGet,AllowAnonymous]
    public async Task<IActionResult> List(int skip=0,int take=10,CancellationToken ct=default) => Ok(ApiResponse<object>.SuccessResponse(await PublicQuery().Skip(Math.Max(0,skip)).Take(Math.Clamp(take,1,50)).ToListAsync(ct)));

    private IQueryable<object> PublicQuery()=>db.Nutritionists.AsNoTracking().Where(n=>n.DeletedAt==null && n.IsVerified && n.User!=null && n.User.IsActive && n.User.EmailConfirmed && n.User.DeletedAt==null).OrderBy(n=>n.Id).Select(n=>(object)new {n.Id,n.UserId,firstName=n.User!.FirstName,lastName=n.User.LastName,n.Qualification,n.Specializations,n.Experience,n.Bio,n.IsVerified,n.Timezone});

    [HttpGet("{id:long}"),AllowAnonymous]
    public async Task<IActionResult> Get(long id,CancellationToken ct)
    {
        var row=await db.Nutritionists.AsNoTracking().Where(n=>n.Id==id && n.DeletedAt==null && n.IsVerified && n.User!=null && n.User.IsActive && n.User.EmailConfirmed && n.User.DeletedAt==null).Select(n=>new {n.Id,firstName=n.User!.FirstName,lastName=n.User.LastName,n.Qualification,n.Specializations,n.Experience,n.Bio,n.IsVerified,n.Timezone}).SingleOrDefaultAsync(ct)??throw new EntityNotFoundException("Nutritionist",id);
        return Ok(ApiResponse<object>.SuccessResponse(row));
    }

    [HttpGet("{id:long}/available-slots"),AllowAnonymous]
    public async Task<IActionResult> Slots(long id,DateTime fromDate,DateTime toDate,long? serviceId,CancellationToken ct)
    {
        if(!await db.Nutritionists.AnyAsync(n=>n.Id==id && n.DeletedAt==null && n.IsVerified && n.User!=null && n.User.IsActive && n.User.EmailConfirmed && n.User.DeletedAt==null,ct)) return Ok(ApiResponse<object>.SuccessResponse(Array.Empty<DateTime>()));
        return Ok(ApiResponse<object>.SuccessResponse(await scheduling.GetAvailableSlotsAsync(id,serviceId,fromDate,toDate,ct)));
    }

    [HttpGet("{id:long}/reviews"),AllowAnonymous]
    public async Task<IActionResult> Reviews(long id,int skip=0,int take=10,CancellationToken ct=default) => Ok(ApiResponse<object>.SuccessResponse(await db.PatientFeedbacks.AsNoTracking().Where(f=>f.Status=="Approved" && f.PublicationConsent && f.WithdrawnAt==null && f.DeletedAt==null && db.Appointments.Any(a=>a.Id==f.AppointmentId && a.NutritionistId==id)).OrderByDescending(f=>f.CreatedAt).Skip(Math.Max(0,skip)).Take(Math.Clamp(take,1,50)).Select(f=>new {f.Id,f.Rating,f.Content,f.CreatedAt}).ToListAsync(ct)));
}
