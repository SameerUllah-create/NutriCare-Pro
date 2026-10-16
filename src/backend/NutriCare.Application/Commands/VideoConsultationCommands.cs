namespace NutriCare.Application.Commands.VideoConsultations;

using MediatR;
using NutriCare.Application.DTOs;

public class ScheduleVideoConsultationCommand : IRequest<VideoConsultationDto>
{
    public long AppointmentId { get; set; }
    public long RequestedByUserId { get; set; }
    public string RequestedByRole { get; set; } = string.Empty;
    public string Provider { get; set; } = "GoogleMeet";
    public string? Notes { get; set; }
}

public class JoinVideoConsultationCommand : IRequest<JoinVideoConsultationDto>
{
    public long ConsultationId { get; set; }
    public long RequestedByUserId { get; set; }
    public string RequestedByRole { get; set; } = string.Empty;
}

public class CompleteVideoConsultationCommand : IRequest<VideoConsultationDto>
{
    public long ConsultationId { get; set; }
    public long RequestedByUserId { get; set; }
    public string RequestedByRole { get; set; } = string.Empty;
    public string? RecordingUrl { get; set; }
    public string? Notes { get; set; }
}
