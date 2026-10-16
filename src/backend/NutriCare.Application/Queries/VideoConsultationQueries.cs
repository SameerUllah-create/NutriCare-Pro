namespace NutriCare.Application.Queries.VideoConsultations;

using MediatR;
using NutriCare.Application.DTOs;

public class GetVideoConsultationByIdQuery : IRequest<VideoConsultationDto>
{
    public long ConsultationId { get; set; }
    public long RequestedByUserId { get; set; }
    public string RequestedByRole { get; set; } = string.Empty;
}

public class GetVideoConsultationByAppointmentQuery : IRequest<VideoConsultationDto>
{
    public long AppointmentId { get; set; }
    public long RequestedByUserId { get; set; }
    public string RequestedByRole { get; set; } = string.Empty;
}

public class GetVideoConsultationHistoryQuery : IRequest<IEnumerable<VideoConsultationDto>>
{
    public long RequestedByUserId { get; set; }
    public string RequestedByRole { get; set; } = string.Empty;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Status { get; set; }
}
