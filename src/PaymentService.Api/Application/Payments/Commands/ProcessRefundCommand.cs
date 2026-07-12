using System;
using MediatR;

namespace PaymentService.Api.Application.Payments.Commands;

public record ProcessRefundCommand(Guid PaymentId) : IRequest<bool>;
