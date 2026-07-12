using MediatR;

namespace PaymentService.Api.Application.Payments.Commands;

public record ValidatePaymentMethodCommand(string Method, string Identifier) : IRequest<bool>;
