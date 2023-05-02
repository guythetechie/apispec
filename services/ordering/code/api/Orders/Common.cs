using common;
using core;
using LanguageExt;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace api.Orders;

internal static class Services
{
    public static void Configure(IServiceCollection services)
    {
        Delete.ConfigureServices(services);
        Get.ConfigureServices(services);
    }
}

internal static class Endpoints
{
    public static void Configure(IEndpointRouteBuilder builder)
    {
        var groupBulder = builder.MapGroup("/v1/orders");

        Delete.ConfigureEndpoint(groupBulder);
        Get.ConfigureEndpoint(groupBulder);
    }
}

#pragma warning disable CA1724 // Type names should not match namespaces
public static class Serialization
#pragma warning restore CA1724 // Type names should not match namespaces
{
    public static JsonObject Serialize(Order order)
    {
        return new JsonObject
        {
            ["id"] = order.Id.Value
        };
    }

    public static Order Deserialize(JsonObject json)
    {
        return TryDeserialize(json)
                .IfLeft(error => throw new JsonException(error));
    }

    public static Either<string, Order> TryDeserialize(JsonObject json)
    {
        return from id in TryGetOrderId(json)
               select new Order
               {
                   Id = id
               };
    }

    public static Either<string, OrderId> TryGetOrderId(JsonObject json)
    {
        return json.TryGetStringProperty("id")
                   .Bind<Guid>(value => Guid.TryParse(value, out var guid) ? guid : "Order ID must be a GUID.")
                   .Map(guid => new OrderId { Value = guid });
    }
}