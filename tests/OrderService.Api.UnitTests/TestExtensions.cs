using System.Reflection;
using OrderService.Api.Domain.Entities;
using OrderService.Api.Domain.Enums;

namespace OrderService.Api.UnitTests;

public static class TestExtensions
{
    public static Order WithStatus(this Order order, OrderStatus status)
    {
        typeof(Order).GetProperty("Status")?.SetValue(order, status);
        return order;
    }
}
