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
            Items: new List<CreateOrderItemDto> { new(Guid.NewGuid(), 2) }, // 200 TL
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
            Items: new List<CreateOrderItemDto> { new(Guid.NewGuid(), 1) },
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
            Items: new List<CreateOrderItemDto> { new(Guid.NewGuid(), 1) },
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
            .Select(_ => new CreateOrderItemDto(Guid.NewGuid(), 1))
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
    [InlineData("Bitcoin")]
    [InlineData("CashOnDelivery")]
    [InlineData("")]
    public void Validate_WithInvalidPaymentMethod_ShouldHaveValidationError(string method)
    {
        // Arrange
        var command = new CreateOrderCommand(
            CustomerId: Guid.NewGuid(),
            IdempotencyKey: "unique-key",
            Items: new List<CreateOrderItemDto> { new(Guid.NewGuid(), 1) },
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
                new(Guid.Empty, 1), // Empty ProductId
                new(Guid.NewGuid(), 0) // Zero Quantity
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
    }
}
