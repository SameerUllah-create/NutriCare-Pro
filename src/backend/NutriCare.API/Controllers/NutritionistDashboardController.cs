namespace NutriCare.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Entities;
using NutriCare.Persistence.Contexts;
using NutriCare.Application.Services;

[ApiController]
[Route("api/nutritionist-dashboard")]
[Authorize(Roles = "Nutritionist")]
public class NutritionistDashboardController : ControllerBase
{
    private readonly NutriCareDbContext _context;

    public NutritionistDashboardController(NutriCareDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<NutritionistDashboardDto>>> GetDashboard(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var nutritionist = await _context.Nutritionists
            .Include(n => n.User)
            .FirstOrDefaultAsync(n => n.UserId == userId && n.DeletedAt == null, cancellationToken);

        if (nutritionist == null)
        {
            return NotFound(ApiResponse<NutritionistDashboardDto>.FailureResponse("Nutritionist profile was not found", 404));
        }

        var now = DateTime.UtcNow;
        var zone = ClinicalTime.Zone(string.IsNullOrWhiteSpace(nutritionist.Timezone) ? ClinicalTime.DefaultZone : nutritionist.Timezone);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, zone);
        var todayStart = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified), zone);
        var todayEnd = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localNow.Date.AddDays(1), DateTimeKind.Unspecified), zone).AddTicks(-1);
        var weekStart = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localNow.Date.AddDays(-6), DateTimeKind.Unspecified), zone);
        var monthStart = TimeZoneInfo.ConvertTimeToUtc(new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified), zone);

        var todaysAppointments = await _context.Appointments
            .Include(a => a.Patient)
            .ThenInclude(p => p!.User)
            .Where(a => a.DeletedAt == null
                && a.NutritionistId == nutritionist.Id
                && _context.ClinicalAssignments.Any(r => r.PatientId == a.PatientId && r.NutritionistId == nutritionist.Id && r.DeletedAt == null && r.StartedAt <= now && (!r.EndedAt.HasValue || r.EndedAt > now))
                && a.AppointmentDateTime >= todayStart
                && a.AppointmentDateTime <= todayEnd
                && a.Status != "Cancelled"
                && a.Status != "Rejected")
            .OrderBy(a => a.AppointmentDateTime)
            .ToListAsync(cancellationToken);

        var pendingConsultations = await _context.Appointments
            .Include(a => a.Patient)
            .ThenInclude(p => p!.User)
            .Where(a => a.DeletedAt == null
                && a.NutritionistId == nutritionist.Id
                && _context.ClinicalAssignments.Any(r => r.PatientId == a.PatientId && r.NutritionistId == nutritionist.Id && r.DeletedAt == null && r.StartedAt <= now && (!r.EndedAt.HasValue || r.EndedAt > now))
                && (a.Status == "Pending" || a.Status == "Scheduled"))
            .OrderBy(a => a.AppointmentDateTime)
            .Take(8)
            .ToListAsync(cancellationToken);

        var activePatientIds = await _context.ClinicalAssignments
            .Where(a => a.DeletedAt == null && a.NutritionistId == nutritionist.Id && a.StartedAt <= now && (!a.EndedAt.HasValue || a.EndedAt > now))
            .Select(a => a.PatientId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var patientProfiles = await _context.Patients
            .Include(p => p.User)
            .Where(p => activePatientIds.Contains(p.Id) && p.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var weightRecords = await _context.WeightTrackings
            .Where(w => w.DeletedAt == null && activePatientIds.Contains(w.PatientId))
            .OrderBy(w => w.RecordDate)
            .ToListAsync(cancellationToken);

        var progress = patientProfiles
            .Select(patient =>
            {
                var records = weightRecords.Where(w => w.PatientId == patient.Id).OrderBy(w => w.RecordDate).ToArray();
                return new NutritionistPatientProgressDto
                {
                    PatientId = patient.Id,
                    PatientName = FullName(patient.User),
                    StartWeight = records.FirstOrDefault()?.Weight,
                    CurrentWeight = records.LastOrDefault()?.Weight ?? patient.CurrentWeight,
                    WeightChange = records.Length == 0 ? null : records.Last().Weight - records.First().Weight,
                    LatestBMI = records.LastOrDefault()?.BMI,
                    LastRecordDate = records.LastOrDefault()?.RecordDate,
                    ProgressRecordCount = records.Length
                };
            })
            .OrderByDescending(p => p.LastRecordDate)
            .ThenBy(p => p.PatientName)
            .Take(8)
            .ToArray();

        var payments = await _context.Payments
            .Where(p => p.DeletedAt == null && p.NutritionistId == nutritionist.Id)
            .ToListAsync(cancellationToken);

        var excludedCurrencyCount = payments.Count(p => p.Currency != "PKR");
        var reportingPayments = payments.Where(p => p.Currency == "PKR").ToArray();
        var completedPayments = reportingPayments.Where(p => p.Status is "Completed" or "PartiallyRefunded" or "Refunded").ToArray();
        var recentMessages = await _context.Messages
            .Include(m => m.Sender)
            .Where(m => m.DeletedAt == null && (m.SenderId == userId || m.ReceiverId == userId))
            .OrderByDescending(m => m.CreatedAt)
            .Take(6)
            .ToListAsync(cancellationToken);

        var unreadMessages = await _context.Messages
            .CountAsync(m => m.DeletedAt == null && m.ReceiverId == userId && !m.IsRead, cancellationToken);

        var monthRevenue = completedPayments
            .Where(p => (p.PaidAt ?? p.CreatedAt) >= monthStart)
            .Sum(p => p.Amount - p.RefundedAmount);

        var dashboard = new NutritionistDashboardDto
        {
            Overview = new NutritionistDashboardOverviewDto
            {
                NutritionistName = FullName(nutritionist.User),
                TodaysAppointmentCount = todaysAppointments.Count,
                PendingConsultationCount = pendingConsultations.Count,
                ActivePatientCount = activePatientIds.Count,
                UnreadMessageCount = unreadMessages,
                MonthRevenue = monthRevenue,
                RatingAverage = nutritionist.RatingAverage
            },
            TodaysAppointments = todaysAppointments.Select(ToAppointmentDto).ToArray(),
            PendingConsultations = pendingConsultations.Select(ToAppointmentDto).ToArray(),
            PatientProgressOverview = progress,
            RevenueSummary = new NutritionistRevenueSummaryDto
            {
                Currency = "PKR",
                ExcludedCurrencyCount = excludedCurrencyCount,
                TodayRevenue = completedPayments
                    .Where(p => (p.PaidAt ?? p.CreatedAt) >= todayStart && (p.PaidAt ?? p.CreatedAt) <= todayEnd)
                    .Sum(p => p.Amount - p.RefundedAmount),
                WeekRevenue = completedPayments
                    .Where(p => (p.PaidAt ?? p.CreatedAt) >= weekStart)
                    .Sum(p => p.Amount - p.RefundedAmount),
                MonthRevenue = monthRevenue,
                TotalRevenue = completedPayments.Sum(p => p.Amount - p.RefundedAmount),
                RefundedAmount = reportingPayments.Sum(p => p.RefundedAmount),
                RevenueByDay = completedPayments
                    .Where(p => (p.PaidAt ?? p.CreatedAt) >= weekStart)
                    .GroupBy(p => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(p.PaidAt ?? p.CreatedAt, DateTimeKind.Utc), zone).Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new NutritionistRevenuePointDto
                    {
                        Date = g.Key,
                        Amount = g.Sum(p => p.Amount - p.RefundedAmount)
                    })
                    .ToArray()
            },
            RecentMessages = new NutritionistMessageSummaryDto
            {
                TotalUnread = unreadMessages,
                Messages = recentMessages.Select(m => new NutritionistMessageDto
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

        return Ok(ApiResponse<NutritionistDashboardDto>.SuccessResponse(dashboard));
    }

    private static NutritionistDashboardAppointmentDto ToAppointmentDto(Appointment appointment)
    {
        return new NutritionistDashboardAppointmentDto
        {
            Id = appointment.Id,
            PatientId = appointment.PatientId,
            PatientName = FullName(appointment.Patient?.User),
            AppointmentDateTime = appointment.AppointmentDateTime,
            Duration = appointment.Duration,
            Status = appointment.Status,
            AppointmentType = appointment.AppointmentType,
            Notes = appointment.Notes
        };
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
