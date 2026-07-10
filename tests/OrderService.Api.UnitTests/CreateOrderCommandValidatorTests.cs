using System;
using System.Collections.Generic;
using System.Linq;
using OrderService.Api.Application.Orders.Commands;
using Shared.Events;
using Xunit;

namespace OrderService.Api.UnitTests;

public class CreateOrderCommandValidatorTests
{
    private readonly CreateOrderCommandValidator _validator;

    public CreateOrderCommandValidatorTests()
    {
        _validator = new CreateOrderCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_ShouldBeValid()
    {
        // Arrange
        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            IdempotencyKey: "unique-key",
            Items: new List<CreateOrderItemDto> { new(Guid.NewGuid(), 2, 100.00m) }, // 200 TL
            IsVip: false,
            PaymentMethod: "CreditCard"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithEmptyCustomerId_ShouldHaveValidationError()
    {
        // Arrange
        var command = new CreateOrderCommand(
            CustomerId: Guid.Empty,
            IdempotencyKey: "unique-key",
            Items: new List<CreateOrderItemDto> { new(Guid.NewGuid(), 1, 150.00m) },
            IsVip: false,
            PaymentMethod: "CreditCard"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateOrderCommand.CustomerId));
    }

    [Fact]
    public void Validate_WithEmptyIdempotencyKey_ShouldHaveValidationError()
    {
        // Arrange
        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            IdempotencyKey: string.Empty,
            Items: new List<CreateOrderItemDto> { new(Guid.NewGuid(), 1, 150.00m) },
            IsVip: false,
            PaymentMethod: "CreditCard"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateOrderCommand.IdempotencyKey));
    }

    [Fact]
    public void Validate_WithNoItems_ShouldHaveValidationError()
    {
        // Arrange
        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            IdempotencyKey: "unique-key",
            Items: new List<CreateOrderItemDto>(),
            IsVip: false,
            PaymentMethod: "CreditCard"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Order must contain at least one item.");
    }

    [Fact]
    public void Validate_WithTooManyItems_ShouldHaveValidationError()
    {
        // Arrange
        var items = Enumerable.Range(0, 21)
            .Select(_ => new CreateOrderItemDto(Guid.NewGuid(), 1, 10.00m))
            .ToList();

        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            IdempotencyKey: "unique-key",
            Items: items,
            IsVip: false,
            PaymentMethod: "CreditCard"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Maximum items per order is 20.");
    }

    [Theory]
    [InlineData(50.00)]
    [InlineData(99.99)]
    public void Validate_WithTotalAmountLessThan100_ShouldHaveValidationError(decimal price)
    {
        // Arrange
        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            IdempotencyKey: "unique-key",
            Items: new List<CreateOrderItemDto> { new(Guid.NewGuid(), 1, price) },
            IsVip: false,
            PaymentMethod: "CreditCard"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Minimum order amount is 100 TL.");
    }

    [Fact]
    public void Validate_WithTotalAmountGreaterThan50000_ShouldHaveValidationError()
    {
        // Arrange
        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            IdempotencyKey: "unique-key",
            Items: new List<CreateOrderItemDto> { new(Guid.NewGuid(), 1, 50001.00m) },
            IsVip: false,
            PaymentMethod: "CreditCard"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Maximum order amount is 50,000 TL.");
    }

    [Theory]
    [InlineData("Bitcoin")]
    [InlineData("CashOnDelivery")]
    [InlineData("")]
    public void Validate_WithInvalidPaymentMethod_ShouldHaveValidationError(string method)
    {
        // Arrange
        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            IdempotencyKey: "unique-key",
            Items: new List<CreateOrderItemDto> { new(Guid.NewGuid(), 1, 150.00m) },
            IsVip: false,
            PaymentMethod: method
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Payment method must be CreditCard, Wallet, or BankTransfer.");
    }

    [Fact]
    public void Validate_WithInvalidItemDetails_ShouldHaveValidationError()
    {
        // Arrange
        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            IdempotencyKey: "unique-key",
            Items: new List<CreateOrderItemDto>
            {
                new(Guid.Empty, 1, 150.00m), // Empty ProductId
                new(Guid.NewGuid(), 0, 150.00m), // Zero Quantity
                new(Guid.NewGuid(), 1, -10.00m) // Negative Price
            },
            IsVip: false,
            PaymentMethod: "CreditCard"
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("ProductId"));
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("Quantity"));
        Assert.Contains(result.Errors, e => e.PropertyName.Contains("UnitPrice"));
    }
}
