using common;
using core;
using LanguageExt;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace api.Orders;

#pragma warning disable CA1716 // Identifiers should not match keywords
public static class Get
#pragma warning restore CA1716 // Identifiers should not match keywords
{
    public delegate ValueTask<Option<(Order, ETag)>> FindOrder(OrderId orderId, CancellationToken cancellationToken);

    internal static void ConfigureEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/{orderId}", GetHandler.Handle);
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        GetServices.Configure(services);
    }
}

file static class GetHandler
{
    public static async ValueTask<IResult> Handle(HttpRequest request,
                                                  [FromServices] Get.FindOrder findOrder,
                                                  CancellationToken cancellationToken)
    {
        return await HttpHandler.Get(request,
                                     TryGetOrderId,
                                     async orderId => await findOrder(orderId, cancellationToken),
                                     Serialization.Serialize);
    }

    private static Either<string, OrderId> TryGetOrderId(string orderId)
    {
        return Guid.TryParse(orderId, out var guid)
                ? new OrderId { Value = guid }
                : "Order ID must be a GUID.";
    }
}

file static class GetServices
{
    public static void Configure(IServiceCollection services)
    {
        services.AddSingleton(FindOrder);
    }

    private static Get.FindOrder FindOrder(IServiceProvider provider)
    {
        return async (orderId, cancellationToken) => await ValueTask.FromResult(Prelude.None);
    }
}