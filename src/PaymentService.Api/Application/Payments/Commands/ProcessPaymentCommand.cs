using System;
using MediatR;

namespace PaymentService.Api.Application.Payments.Commands;

public record ProcessPaymentCommand(Guid OrderId, decimal Amount, string Method) : IRequest<bool>;
