namespace NutriCare.API.Controllers;

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.DTOs;
using NutriCare.Application.Services;
using NutriCare.Domain.Entities;
using NutriCare.Domain.Exceptions;
using NutriCare.Domain.Interfaces;
using NutriCare.Persistence.Contexts;

[ApiController, Route("api/profile"), Authorize]
public class ProfileController(NutriCareDbContext db, UserManager<User> users, ICurrentUserService current, IClinicalConsentService consent) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(ApiResponse<object>.SuccessResponse(await ReadAsync(ct)));

    [HttpPut]
    public async Task<IActionResult> Update(ProfileRequest input, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == current.UserId && u.DeletedAt == null, ct);
        user.FirstName = input.FirstName.Trim(); user.LastName = input.LastName.Trim();
        if (user.PhoneNumber != input.PhoneNumber) user.PhoneNumberConfirmed = false;
        user.PhoneNumber = input.PhoneNumber;
        var patient = await db.Patients.SingleOrDefaultAsync(p => p.UserId == current.UserId && p.DeletedAt == null, ct);
        if (patient != null)
        {
            if (input.Height.HasValue && input.Height.Value != patient.Height || input.CurrentWeight.HasValue && input.CurrentWeight.Value != patient.CurrentWeight ||
                input.TargetWeight != patient.TargetWeight || input.MedicalHistory != patient.MedicalHistory || input.Allergies != patient.Allergies || input.CurrentMedications != patient.CurrentMedications)
                await consent.RequireAsync(current.UserId,"HealthData",cancellationToken:ct);
            if (input.DateOfBirth is DateTime birth && (birth.Date > ClinicalTime.LocalToday() || birth.Year < 1900))
                throw new NutriCare.Domain.Exceptions.ValidationException("Date of birth is invalid");
            if (input.PreferredLanguage != null && input.PreferredLanguage is not ("English" or "Urdu" or "en" or "ur"))
                throw new NutriCare.Domain.Exceptions.ValidationException("Choose English or Urdu");
            if (input.DateOfBirth.HasValue) patient.DateOfBirth = input.DateOfBirth.Value.Date;
            patient.Gender = input.Gender; patient.BloodGroup = input.BloodGroup;
            if (input.Height.HasValue) patient.Height = input.Height.Value;
            if (input.CurrentWeight.HasValue) patient.CurrentWeight = input.CurrentWeight.Value;
            patient.TargetWeight = input.TargetWeight;
            patient.MedicalHistory = input.MedicalHistory; patient.Allergies = input.Allergies;
            patient.CurrentMedications = input.CurrentMedications;
            patient.PreferredLanguage = input.PreferredLanguage is "Urdu" or "ur" ? "Urdu" : "English";
            patient.NotificationPreference = input.NotificationPreference;
        }
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse<object>.SuccessResponse(await ReadAsync(ct), "Profile saved"));
    }

    private async Task<object> ReadAsync(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == current.UserId && u.DeletedAt == null, ct);
        var roles = await users.GetRolesAsync(user);
        return new { user = new UserDto { Id = user.Id, Email = user.Email ?? "", FirstName = user.FirstName, LastName = user.LastName,
            PhoneNumber = user.PhoneNumber, ProfileImageUrl = user.ProfileImageUrl, Role = roles.FirstOrDefault() ?? "Patient",
            IsEmailVerified = user.EmailConfirmed, IsPhoneVerified = user.PhoneNumberConfirmed, IsActive = user.IsActive,
            CreatedAt = user.CreatedAt, UpdatedAt = user.UpdatedAt },
            patient = await db.Patients.AsNoTracking().Where(p => p.UserId == current.UserId && p.DeletedAt == null)
                .Select(p => new { p.Id,p.UserId,p.DateOfBirth,p.Gender,p.BloodGroup,p.Height,p.CurrentWeight,p.TargetWeight,p.MedicalHistory,p.Allergies,p.CurrentMedications,p.PreferredLanguage,p.NotificationPreference }).SingleOrDefaultAsync(ct),
            nutritionist = await db.Nutritionists.AsNoTracking().Where(n => n.UserId == current.UserId && n.DeletedAt == null)
                .Select(n => new { n.Id,n.UserId,n.LicenseNumber,n.Qualification,n.Specializations,n.Experience,n.Bio,n.IsVerified,n.Timezone }).SingleOrDefaultAsync(ct) };
    }
}

public class ProfileRequest
{
    [Required, StringLength(100, MinimumLength = 1)] public string FirstName { get; set; } = "";
    [Required, StringLength(100, MinimumLength = 1)] public string LastName { get; set; } = "";
    [Phone, StringLength(30)] public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    [StringLength(40)] public string? Gender { get; set; }
    [StringLength(10)] public string? BloodGroup { get; set; }
    [Range(20,300)] public decimal? Height { get; set; }
    [Range(1,500)] public decimal? CurrentWeight { get; set; }
    [Range(1,500)] public decimal? TargetWeight { get; set; }
    [StringLength(5000)] public string? MedicalHistory { get; set; }
    [StringLength(5000)] public string? Allergies { get; set; }
    [StringLength(5000)] public string? CurrentMedications { get; set; }
    [StringLength(20)] public string? PreferredLanguage { get; set; }
    [StringLength(100)] public string? NotificationPreference { get; set; }
}
