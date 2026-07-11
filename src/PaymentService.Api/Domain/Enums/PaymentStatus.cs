namespace PaymentService.Api.Domain.Enums;

public enum PaymentStatus
{
    Pending = 1,
    Success = 2,
    Failed = 3,
    Reversed = 4,
    RefundPending = 5
}
