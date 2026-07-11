using AutoMapper;
using OrderService.Api.Application.Orders.Dtos;
using OrderService.Api.Domain.Entities;

namespace OrderService.Api.Application.Mapping;

public class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        CreateMap<Order, OrderDto>();
        CreateMap<OrderItem, OrderItemDto>();
    }
}
