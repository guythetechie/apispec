using core;
using FsCheck;
using FsCheck.Fluent;
using System;
using System.Linq;

namespace common;

public static class Generator
{
    public static Gen<OrderId> OrderId { get; } =
        from value in GenExtensions.GenerateDefault<Guid>()
        select new OrderId { Value = value };

    public static Gen<Order> Order { get; } =
        from id in OrderId
        select new Order
        {
            Id = id
        };
}
