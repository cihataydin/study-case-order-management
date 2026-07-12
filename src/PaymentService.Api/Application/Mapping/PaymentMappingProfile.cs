using AutoMapper;
using PaymentService.Api.Domain.Entities;
using PaymentService.Api.Application.Payments.Features;

namespace PaymentService.Api.Application.Mapping;

public class PaymentMappingProfile : Profile
{
    public PaymentMappingProfile()
    {
        CreateMap<Payment, PaymentDto>();
    }
}
