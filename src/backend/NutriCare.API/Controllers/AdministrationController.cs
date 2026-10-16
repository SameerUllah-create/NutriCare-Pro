namespace NutriCare.API.Controllers;

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;

[ApiController,Route("api/admin"),Authorize(Roles="Admin")]
public class AdministrationController(NutriCareDbContext db,UserManager<User> users,ICurrentUserService current,IEmailService email,IConfiguration configuration):ControllerBase
{
    [HttpPost("nutritionist")]
    public async Task<IActionResult> Provision(ProvisionNutritionistRequest input,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        if(db.Database.IsSqlServer()) await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='NutriCare:single-clinician',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51000,'Provisioning is busy',1;",ct);
        if(await db.Nutritionists.AnyAsync(n=>n.DeletedAt==null,ct)) throw new ConflictException("Single-nutritionist launch already has a clinician profile");
        var user=new User {UserName=input.Email,Email=input.Email,FirstName=input.FirstName,LastName=input.LastName,EmailConfirmed=false};
        var result=await users.CreateAsync(user,input.Password);if(!result.Succeeded)throw new NutriCare.Domain.Exceptions.ValidationException("Account failed password or uniqueness validation");
        var role=await users.AddToRoleAsync(user,"Nutritionist");if(!role.Succeeded)throw new InvalidOperationException("Clinician role assignment failed");
        var clinician=new Nutritionist {UserId=user.Id,LicenseNumber=input.LicenseNumber,Qualification=input.Qualification,Timezone="Asia/Karachi",IsVerified=false};db.Add(clinician);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        var token=await users.GenerateEmailConfirmationTokenAsync(user);var origin=(configuration["FrontendUrl"]??"http://localhost:3000").TrimEnd('/');
        await email.SendVerificationEmailAsync(user.Email!,$"{origin}/verify-email?userId={user.Id}&token={Uri.EscapeDataString(token)}");
        return StatusCode(201,ApiResponse<object>.SuccessResponse(new {clinician.Id,clinician.UserId,clinician.IsVerified},"Clinician created; email and professional verification are required",201));
    }

    [HttpPost("nutritionist/{id:long}/verify")]
    public async Task<IActionResult> Verify(long id,ClinicianVerificationRequest input,CancellationToken ct)
    {
        if(!input.ApprovalConfirmed || string.IsNullOrWhiteSpace(input.EvidenceReference))throw new ArgumentException("Explicit approval and verified credential evidence are required");
        var row=await db.Nutritionists.FindAsync([id],ct)??throw new EntityNotFoundException("Nutritionist",id);
        ContentManagementController.Concurrency(row,input.RowVersion);
        row.IsVerified=true;row.VerificationDate=DateTime.UtcNow;row.VerificationDocumentUrl=input.EvidenceReference;
        await db.SaveChangesAsync(ct);return Ok(ApiResponse<object>.SuccessResponse(new {row.Id,row.IsVerified,row.VerificationDate,row.RowVersion}));
    }

    [HttpGet("privacy-requests")]
    public async Task<IActionResult> Requests(CancellationToken ct)=>Ok(ApiResponse<object>.SuccessResponse(await db.DataRightsRequests.AsNoTracking().OrderByDescending(r=>r.CreatedAt).Take(500).ToListAsync(ct)));

    [HttpPut("privacy-requests/{id:long}")]
    public async Task<IActionResult> Review(long id,RightsReviewRequest input,CancellationToken ct)
    {
        var row=await db.DataRightsRequests.FindAsync([id],ct)??throw new EntityNotFoundException("Privacy request",id);ContentManagementController.Concurrency(row,input.RowVersion);
        if(input.Status is not ("InReview" or "Rejected" or "Completed"))throw new ArgumentException("Invalid review status");
        if(row.RequestType=="Deletion" && input.Status=="Completed")throw new ConflictException("Deletion requires an approved retention process; retain this request in review until it is implemented and verified");
        row.Status=input.Status;row.ReviewedByUserId=current.UserId;row.CompletedAt=input.Status is "Completed" or "Rejected"?DateTime.UtcNow:null;
        await db.SaveChangesAsync(ct);return Ok(ApiResponse<object>.SuccessResponse(row));
    }
}
public class ProvisionNutritionistRequest { [Required,EmailAddress]public string Email{get;set;}="";[Required,StringLength(100)]public string FirstName{get;set;}="";[Required,StringLength(100)]public string LastName{get;set;}="";[Required,MinLength(12)]public string Password{get;set;}="";[Required,StringLength(100)]public string LicenseNumber{get;set;}="";[Required,StringLength(500)]public string Qualification{get;set;}=""; }
public class ClinicianVerificationRequest {public bool ApprovalConfirmed{get;set;}[Required,StringLength(500)]public string EvidenceReference{get;set;}="";public byte[]? RowVersion{get;set;} }
public class RightsReviewRequest {public string Status{get;set;}="InReview";public byte[]? RowVersion{get;set;} }
