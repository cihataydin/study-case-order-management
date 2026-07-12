using System;
using PaymentService.Api.Domain.Enums;

namespace PaymentService.Api.Application.Payments.Dtos;

public record PaymentDto(Guid Id, Guid OrderId, decimal Amount, PaymentStatus Status, string Method, DateTime CreatedAt, DateTime? UpdatedAt);
