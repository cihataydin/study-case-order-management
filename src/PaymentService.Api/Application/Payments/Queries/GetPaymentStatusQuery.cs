using System;
using MediatR;
using PaymentService.Api.Application.Payments.Dtos;

namespace PaymentService.Api.Application.Payments.Queries;

public record GetPaymentStatusQuery(Guid PaymentId) : IRequest<PaymentDto?>;
