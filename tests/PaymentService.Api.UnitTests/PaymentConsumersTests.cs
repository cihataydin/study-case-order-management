using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PaymentService.Api.Application.Consumers;
using PaymentService.Api.Domain.Entities;
using PaymentService.Api.Domain.Enums;
using PaymentService.Api.Infrastructure.Data;
using Polly;
using Polly.Retry;
using Shared.Enums;
using Shared.Events;
using Xunit;

namespace PaymentService.Api.UnitTests;

public class PaymentConsumersTests : IDisposable
{
    private readonly PaymentDbContext _dbContext;
    private readonly ILogger<StockReservedEventConsumer> _logger;

    public PaymentConsumersTests()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PaymentDbContext(options);
        _logger = Substitute.For<ILogger<StockReservedEventConsumer>>();
    }

    private class CustomRandom : Random
    {
        private readonly int _value;
        public CustomRandom(int value) => _value = value;
        public override int Next(int minValue, int maxValue) => _value;
    }

    private void InjectMockRandomAndZeroDelayPolicy(StockReservedEventConsumer consumer, int randomValue)
    {
        // Set Random Generator directly
        StockReservedEventConsumer.RandomGenerator = new CustomRandom(randomValue);

        // Inject Zero-delay retry policy with onRetry callback so it logs as expected
        var zeroDelayPolicy = Policy
            .Handle<TimeoutException>()
            .WaitAndRetryAsync(3, _ => TimeSpan.Zero,
                (exception, timeSpan, retryCount, ctx) =>
                {
                    _logger.LogWarning($"Payment simulation timeout. Retrying... Attempt {retryCount}");
                });

        typeof(StockReservedEventConsumer)
            .GetField("_retryPolicy", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(consumer, zeroDelayPolicy);
    }

    [Fact]
    public async Task Consume_WhenPaymentSimulationSucceeds_ShouldSaveSuccessAndPublishProcessedEvent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var message = new StockReservedEvent(orderId, 150.00m, PaymentMethod.CreditCard, false);
        var context = Substitute.For<ConsumeContext<StockReservedEvent>>();
        context.Message.Returns(message);

        var consumer = new StockReservedEventConsumer(_dbContext, _logger);
        InjectMockRandomAndZeroDelayPolicy(consumer, 50); // 50 <= 85 means Success

        // Act
        await consumer.Consume(context);

        // Assert
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
        Assert.NotNull(payment);
        Assert.Equal(PaymentStatus.Success, payment.Status);
        Assert.Equal(150.00m, payment.Amount);

        await context.Received(1).Publish(
            Arg.Is<PaymentProcessedEvent>(e => e.OrderId == orderId && e.PaymentId == payment.Id),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Consume_WhenPaymentSimulationFails_ShouldSaveFailedAndPublishFailedEvent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var message = new StockReservedEvent(orderId, 150.00m, PaymentMethod.CreditCard, false);
        var context = Substitute.For<ConsumeContext<StockReservedEvent>>();
        context.Message.Returns(message);

        var consumer = new StockReservedEventConsumer(_dbContext, _logger);
        InjectMockRandomAndZeroDelayPolicy(consumer, 99); // 99 > 95 means Fail

        // Act
        await consumer.Consume(context);

        // Assert
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
        Assert.NotNull(payment);
        Assert.Equal(PaymentStatus.Failed, payment.Status);

        await context.Received(1).Publish(
            Arg.Is<PaymentFailedEvent>(e => e.OrderId == orderId && e.Reason == "Card declined by bank"),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Consume_WhenPaymentSimulationTimesOutAndEventuallyFails_ShouldRetryAndSaveFailed()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var message = new StockReservedEvent(orderId, 150.00m, PaymentMethod.CreditCard, false);
        var context = Substitute.For<ConsumeContext<StockReservedEvent>>();
        context.Message.Returns(message);

        var consumer = new StockReservedEventConsumer(_dbContext, _logger);
        InjectMockRandomAndZeroDelayPolicy(consumer, 90); // 90 > 85 && 90 <= 95 means Timeout

        // Act
        await consumer.Consume(context);

        // Assert
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
        Assert.NotNull(payment);
        Assert.Equal(PaymentStatus.Failed, payment.Status);

        // Should log retry message warning
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Payment simulation timeout")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );

        await context.Received(1).Publish(
            Arg.Is<PaymentFailedEvent>(e => e.OrderId == orderId && e.Reason == "Payment gateway unavailable"),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Consume_WithLargeAmount_ShouldTriggerFraudSimulation()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var message = new StockReservedEvent(orderId, 15000.00m, PaymentMethod.CreditCard, false); // > 10,000
        var context = Substitute.For<ConsumeContext<StockReservedEvent>>();
        context.Message.Returns(message);

        var consumer = new StockReservedEventConsumer(_dbContext, _logger);
        InjectMockRandomAndZeroDelayPolicy(consumer, 50); // Success

        // Act
        await consumer.Consume(context);

        // Assert
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
        Assert.NotNull(payment);
        Assert.Equal(PaymentStatus.Success, payment.Status);

        // Should log fraud verification message
        _logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Simulating additional fraud verification")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        );
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
