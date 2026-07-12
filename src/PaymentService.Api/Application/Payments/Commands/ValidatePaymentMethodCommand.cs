using MediatR;
using Shared.Enums;

namespace PaymentService.Api.Application.Payments.Commands;

public record ValidatePaymentMethodCommand(PaymentMethod Method, string Identifier) : IRequest<bool>;
