namespace NutriCare.Application.Queries.Payments;

using MediatR;
using NutriCare.Application.DTOs;

public class GetPaymentHistoryQuery : IRequest<IEnumerable<PaymentDto>>
{
    public long? PatientId { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class GetPaymentByIdQuery : IRequest<PaymentDto>
{
    public long PaymentId { get; set; }
}

public class GetInvoiceByPaymentQuery : IRequest<InvoiceDto>
{
    public long PaymentId { get; set; }
}
