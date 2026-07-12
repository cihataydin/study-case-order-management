using System;
using NSubstitute;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PaymentService.Api.Application.Payments.Commands;
using PaymentService.Api.Application.Payments.Queries;
using PaymentService.Api.Application.Payments.Dtos;
using PaymentService.Api.Domain.Entities;
using PaymentService.Api.Domain.Enums;
using PaymentService.Api.Infrastructure.Data;
using PaymentService.Api.Infrastructure.Data;
using Shared.Enums;
using Xunit;

namespace PaymentService.Api.UnitTests;

public class PaymentFeaturesTests : IDisposable
{
    private readonly PaymentDbContext _dbContext;

    public PaymentFeaturesTests()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PaymentDbContext(options);
    }

    [Fact]
    public async Task GetPaymentStatus_WithExistingPayment_ShouldReturnPayment()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var payment = new Payment
        {
            Id = paymentId,
            OrderId = Guid.NewGuid(),
            Amount = 150.00m,
            Method = PaymentMethod.CreditCard,
            Status = PaymentStatus.Success
        };
        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var mapper = Substitute.For<AutoMapper.IMapper>();
        mapper.Map<PaymentDto>(Arg.Any<Payment>()).Returns(c => {
            var p = c.Arg<Payment>();
            return new PaymentDto(p.Id, p.OrderId, p.Amount, p.Status, p.Method, p.CreatedAt, p.UpdatedAt);
        });
        var handler = new GetPaymentStatusQueryHandler(_dbContext, mapper);

        // Act
        var result = await handler.Handle(new GetPaymentStatusQuery(paymentId), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(paymentId, result.Id);
        Assert.Equal(PaymentStatus.Success, result.Status);
    }

    [Fact]
    public async Task GetPaymentStatus_WithNonExistentPayment_ShouldReturnNull()
    {
        // Arrange
        var mapper = Substitute.For<AutoMapper.IMapper>();
        var handler = new GetPaymentStatusQueryHandler(_dbContext, mapper);

        // Act
        var result = await handler.Handle(new GetPaymentStatusQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ProcessRefund_WithSuccessfulPayment_ShouldUpdateToRefundPending()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var payment = new Payment
        {
            Id = paymentId,
            OrderId = Guid.NewGuid(),
            Amount = 200.00m,
            Method = PaymentMethod.Wallet,
            Status = PaymentStatus.Success
        };
        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var handler = new ProcessRefundCommandHandler(_dbContext);

        // Act
        var result = await handler.Handle(new ProcessRefundCommand(paymentId), CancellationToken.None);

        // Assert
        Assert.True(result);

        var updatedPayment = await _dbContext.Payments.FindAsync(paymentId);
        Assert.NotNull(updatedPayment);
        Assert.Equal(PaymentStatus.RefundPending, updatedPayment.Status);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Failed)]
    [InlineData(PaymentStatus.Reversed)]
    [InlineData(PaymentStatus.RefundPending)]
    public async Task ProcessRefund_WithNonSuccessfulPayment_ShouldReturnFalse(PaymentStatus status)
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var payment = new Payment
        {
            Id = paymentId,
            OrderId = Guid.NewGuid(),
            Amount = 200.00m,
            Method = PaymentMethod.Wallet,
            Status = status
        };
        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync();

        var handler = new ProcessRefundCommandHandler(_dbContext);

        // Act
        var result = await handler.Handle(new ProcessRefundCommand(paymentId), CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(PaymentMethod.CreditCard, "1234567812345678", true)] // 16 digits
    [InlineData(PaymentMethod.CreditCard, "12345678", false)] // 8 digits
    [InlineData(PaymentMethod.Wallet, "user_123", true)] // any string
    [InlineData(PaymentMethod.Wallet, "", false)] // empty string
    [InlineData(PaymentMethod.BankTransfer, "TR123456789012345678901234", true)] // 26 chars starting with TR
    [InlineData(PaymentMethod.BankTransfer, "TR123", false)] // invalid length
    [InlineData(PaymentMethod.BankTransfer, "EN123456789012345678901234", false)] // invalid start
    [InlineData((PaymentMethod)99, "123", false)]
    public async Task ValidatePaymentMethod_ShouldValidateBasedOnMethod(PaymentMethod method, string identifier, bool expected)
    {
        // Arrange
        var handler = new ValidatePaymentMethodCommandHandler();

        // Act
        var result = await handler.Handle(new ValidatePaymentMethodCommand(method, identifier), CancellationToken.None);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ProcessPayment_ShouldCreateSuccessfulPayment()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var handler = new ProcessPaymentCommandHandler(_dbContext);

        // Act
        var result = await handler.Handle(new ProcessPaymentCommand(orderId, 300.00m, PaymentMethod.BankTransfer), CancellationToken.None);

        // Assert
        Assert.True(result);

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
        Assert.NotNull(payment);
        Assert.Equal(300.00m, payment.Amount);
        Assert.Equal(PaymentMethod.BankTransfer, payment.Method);
        Assert.Equal(PaymentStatus.Success, payment.Status);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
