using System;
using PaymentService.Api.Domain.Enums;
using Shared.Enums;

namespace PaymentService.Api.Application.Payments.Dtos;

public record PaymentDto(Guid Id, Guid OrderId, decimal Amount, PaymentStatus Status, PaymentMethod Method, DateTime CreatedAt, DateTime? UpdatedAt);
