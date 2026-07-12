using System;
using MediatR;
using Shared.Enums;

namespace PaymentService.Api.Application.Payments.Commands;

public record ProcessPaymentCommand(Guid OrderId, decimal Amount, PaymentMethod Method) : IRequest<bool>;
