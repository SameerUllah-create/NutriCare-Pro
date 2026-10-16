namespace NutriCare.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutriCare.Application.DTOs;
using NutriCare.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using NutriCare.Persistence.Contexts;

[ApiController]
[Route("api/availability")]
public sealed class AvailabilityController(IClinicalSchedulingService scheduling, NutriCareDbContext db) : ControllerBase
{
    [HttpGet("{nutritionistId:long}/slots")]
    [AllowAnonymous]
    public async Task<IActionResult> Slots(long nutritionistId, [FromQuery] long serviceId, [FromQuery] DateTimeOffset fromDate, [FromQuery] DateTimeOffset toDate, CancellationToken cancellationToken)
    {
        if (!await db.Nutritionists.AnyAsync(n=>n.Id==nutritionistId && n.DeletedAt==null && n.IsVerified && n.User!=null && n.User.IsActive && n.User.EmailConfirmed && n.User.DeletedAt==null,cancellationToken))
            return Ok(ApiResponse<object>.SuccessResponse(Array.Empty<DateTime>()));
        return Ok(ApiResponse<object>.SuccessResponse(await scheduling.GetAvailableSlotsAsync(nutritionistId,serviceId,fromDate.UtcDateTime,toDate.UtcDateTime,cancellationToken)));
    }
}
