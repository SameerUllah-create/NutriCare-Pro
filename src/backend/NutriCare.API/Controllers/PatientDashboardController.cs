namespace NutriCare.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Entities;
using NutriCare.Persistence.Contexts;
using NutriCare.Application.Services;

[ApiController]
[Route("api/patient-dashboard")]
[Authorize(Roles = "Patient")]
public class PatientDashboardController : ControllerBase
{
    private readonly NutriCareDbContext _context;
    private readonly ClinicalPlanWorkflow _plans;

    public PatientDashboardController(NutriCareDbContext context, ClinicalPlanWorkflow plans)
    {
        _context = context;
        _plans = plans;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PatientDashboardDto>>> GetDashboard(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var patient = await _context.Patients
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.DeletedAt == null, cancellationToken);

        if (patient == null)
        {
            return NotFound(ApiResponse<PatientDashboardDto>.FailureResponse("Patient profile was not found", 404));
        }

        var now = DateTime.UtcNow;
        var upcomingQuery = _context.Appointments
            .Include(a => a.Nutritionist)
            .ThenInclude(n => n!.User)
            .Where(a => a.DeletedAt == null
                && a.PatientId == patient.Id
                && a.AppointmentDateTime >= now
                && a.Status != "Cancelled"
                && a.Status != "Rejected")
            .OrderBy(a => a.AppointmentDateTime);
        var upcomingAppointmentCount = await upcomingQuery.CountAsync(cancellationToken);
        var upcomingAppointments = await upcomingQuery.Take(5).ToListAsync(cancellationToken);

        var today = ClinicalTime.LocalToday();
        var activePlan = (await _plans.PatientPlansAsync(patient.Id, "Active", cancellationToken))
            .Where(p => p.StartDate.Date <= today && p.EndDate.Date >= today)
            .OrderByDescending(p => p.PublishedAt).FirstOrDefault();

        var planCompliance = activePlan == null
            ? new List<DietPlanCompliance>()
            : await _context.DietPlanCompliances
                .Where(c => c.DeletedAt == null && c.DietPlanId == activePlan.Id)
                .OrderByDescending(c => c.RecordDate)
                .Take(14)
                .ToListAsync(cancellationToken);

        var weightRecords = await _context.WeightTrackings
            .Where(w => w.DeletedAt == null && w.PatientId == patient.Id)
            .OrderByDescending(w => w.RecordDate)
            .Take(12)
            .ToListAsync(cancellationToken);

        var orderedWeightRecords = weightRecords.OrderBy(w => w.RecordDate).ToArray();
        var recentReports = await _context.MedicalReports
            .Where(r => r.DeletedAt == null && r.PatientId == patient.Id)
            .OrderByDescending(r => r.UploadedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        var notifications = await _context.Notifications
            .Where(n => n.DeletedAt == null && n.UserId == userId && n.Channel == "InApp")
            .OrderByDescending(n => n.CreatedAt)
            .Take(6)
            .ToListAsync(cancellationToken);

        var recentMessages = await _context.Messages
            .Include(m => m.Sender)
            .Where(m => m.DeletedAt == null && (m.SenderId == userId || m.ReceiverId == userId))
            .OrderByDescending(m => m.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        var unreadMessages = await _context.Messages
            .CountAsync(m => m.DeletedAt == null && m.ReceiverId == userId && !m.IsRead, cancellationToken);

        var dashboard = new PatientDashboardDto
        {
            Overview = new PatientDashboardOverviewDto
            {
                PatientName = FullName(patient.User),
                CurrentWeight = patient.CurrentWeight,
                TargetWeight = patient.TargetWeight,
                UpcomingAppointmentCount = upcomingAppointmentCount,
                UnreadNotificationCount = await _context.Notifications.CountAsync(n => n.UserId == userId && n.Channel == "InApp" && n.DeletedAt == null && !n.IsRead, cancellationToken),
                UnreadMessageCount = unreadMessages,
                LatestBMI = orderedWeightRecords.LastOrDefault()?.BMI
            },
            UpcomingAppointments = upcomingAppointments.Select(a => new PatientDashboardAppointmentDto
            {
                Id = a.Id,
                AppointmentDateTime = a.AppointmentDateTime,
                Duration = a.Duration,
                Status = a.Status,
                AppointmentType = a.AppointmentType,
                NutritionistName = FullName(a.Nutritionist?.User)
            }).ToArray(),
            DietPlanSummary = activePlan == null ? null : new PatientDashboardDietPlanDto
            {
                Id = activePlan.Id,
                Title = activePlan.Title,
                Status = activePlan.Status,
                DailyCalories = activePlan.DailyCalories,
                ProteinGrams = activePlan.ProteinGrams,
                CarbsGrams = activePlan.CarbsGrams,
                FatsGrams = activePlan.FatsGrams,
                WaterIntakeLiters = activePlan.WaterIntakeLiters,
                Supplements = activePlan.Supplements,
                Instructions = activePlan.Instructions,
                StartDate = activePlan.StartDate,
                EndDate = activePlan.EndDate,
                DaysRemaining = Math.Max(0, (activePlan.EndDate.Date - today).Days),
                AverageCompliancePercentage = planCompliance.Count == 0 ? 0 : Math.Round(planCompliance.Average(c => c.CompliancePercentage), 2)
            },
            WeightProgress = new PatientDashboardWeightProgressDto
            {
                StartWeight = orderedWeightRecords.FirstOrDefault()?.Weight,
                CurrentWeight = orderedWeightRecords.LastOrDefault()?.Weight ?? patient.CurrentWeight,
                WeightChange = orderedWeightRecords.Length == 0 ? null : orderedWeightRecords.Last().Weight - orderedWeightRecords.First().Weight,
                LatestBMI = orderedWeightRecords.LastOrDefault()?.BMI,
                Points = orderedWeightRecords.Select(w => new PatientDashboardWeightPointDto
                {
                    Date = w.RecordDate,
                    Weight = w.Weight,
                    BMI = w.BMI
                }).ToArray()
            },
            RecentReports = recentReports.Select(r => new PatientDashboardReportDto
            {
                Id = r.Id,
                ReportType = r.ReportType,
                OriginalFileName = r.OriginalFileName,
                ContentType = r.ContentType,
                FileSizeBytes = r.FileSizeBytes,
                Status = r.Status,
                UploadedAt = r.UploadedAt
            }).ToArray(),
            Notifications = notifications.Select(n => new PatientDashboardNotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                Channel = n.Channel,
                Status = n.Status,
                CreatedAt = n.CreatedAt
            }).ToArray(),
            Messages = new PatientDashboardMessageSummaryDto
            {
                TotalUnread = unreadMessages,
                RecentMessages = recentMessages.Select(m => new PatientDashboardMessageDto
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    SenderName = FullName(m.Sender),
                    MessageBody = m.MessageBody,
                    MessageType = m.MessageType,
                    IsRead = m.IsRead,
                    CreatedAt = m.CreatedAt
                }).ToArray()
            }
        };

        return Ok(ApiResponse<PatientDashboardDto>.SuccessResponse(dashboard));
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst("uid")?.Value;
        return long.TryParse(claim, out var userId) ? userId : 0;
    }

    private static string FullName(User? user)
    {
        if (user == null)
        {
            return string.Empty;
        }

        return $"{user.FirstName} {user.LastName}".Trim();
    }
}
