namespace NutriCare.API.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NutriCare.Application.Commands.VideoConsultations;
using NutriCare.Application.DTOs;
using NutriCare.Application.Queries.VideoConsultations;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Patient,Nutritionist,Admin")]
public class VideoConsultationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public VideoConsultationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("schedule")]
    [Authorize(Roles = "Nutritionist,Admin")]
    public async Task<ActionResult<ApiResponse<VideoConsultationDto>>> Schedule([FromBody] ScheduleVideoConsultationRequest request)
    {
        var result = await _mediator.Send(new ScheduleVideoConsultationCommand
        {
            AppointmentId = request.AppointmentId,
            Provider = request.Provider,
            Notes = request.Notes,
            RequestedByUserId = GetCurrentUserId(),
            RequestedByRole = GetCurrentRole()
        });

        return CreatedAtAction(nameof(GetById), new { id = result.Id },
            ApiResponse<VideoConsultationDto>.SuccessResponse(result, "Video consultation scheduled successfully", 201));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<VideoConsultationDto>>> GetById(long id)
    {
        var result = await _mediator.Send(new GetVideoConsultationByIdQuery
        {
            ConsultationId = id,
            RequestedByUserId = GetCurrentUserId(),
            RequestedByRole = GetCurrentRole()
        });

        return Ok(ApiResponse<VideoConsultationDto>.SuccessResponse(result));
    }

    [HttpGet("appointment/{appointmentId:long}")]
    public async Task<ActionResult<ApiResponse<VideoConsultationDto>>> GetByAppointment(long appointmentId)
    {
        var result = await _mediator.Send(new GetVideoConsultationByAppointmentQuery
        {
            AppointmentId = appointmentId,
            RequestedByUserId = GetCurrentUserId(),
            RequestedByRole = GetCurrentRole()
        });

        return Ok(ApiResponse<VideoConsultationDto>.SuccessResponse(result));
    }

    [HttpPost("{id:long}/join")]
    public async Task<ActionResult<ApiResponse<JoinVideoConsultationDto>>> Join(long id)
    {
        var result = await _mediator.Send(new JoinVideoConsultationCommand
        {
            ConsultationId = id,
            RequestedByUserId = GetCurrentUserId(),
            RequestedByRole = GetCurrentRole()
        });

        return Ok(ApiResponse<JoinVideoConsultationDto>.SuccessResponse(result, "Meeting link generated successfully"));
    }

    [HttpPut("{id:long}/complete")]
    [Authorize(Roles = "Nutritionist,Admin")]
    public async Task<ActionResult<ApiResponse<VideoConsultationDto>>> Complete(long id, [FromBody] CompleteVideoConsultationRequest request)
    {
        var result = await _mediator.Send(new CompleteVideoConsultationCommand
        {
            ConsultationId = id,
            RequestedByUserId = GetCurrentUserId(),
            RequestedByRole = GetCurrentRole(),
            RecordingUrl = request.RecordingUrl,
            Notes = request.Notes
        });

        return Ok(ApiResponse<VideoConsultationDto>.SuccessResponse(result, "Video consultation completed successfully"));
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiResponse<IEnumerable<VideoConsultationDto>>>> History(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? status = null)
    {
        var result = await _mediator.Send(new GetVideoConsultationHistoryQuery
        {
            RequestedByUserId = GetCurrentUserId(),
            RequestedByRole = GetCurrentRole(),
            FromDate = fromDate,
            ToDate = toDate,
            Status = status
        });

        return Ok(ApiResponse<IEnumerable<VideoConsultationDto>>.SuccessResponse(result));
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirst("uid")?.Value;
        return long.TryParse(claim, out var userId) ? userId : 0;
    }

    private string GetCurrentRole()
    {
        return User.FindFirst("role")?.Value ?? string.Empty;
    }
}
