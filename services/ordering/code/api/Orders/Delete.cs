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

public static class Delete
{
    public delegate ValueTask<Either<HttpHandler.DeleteError, Unit>> DeleteOrder(OrderId orderId, ETag eTag, CancellationToken cancellationToken);

    internal static void ConfigureEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapDelete("/{orderId}", FileScopedHandler.Handle);
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        FileScopedServices.Configure(services);
    }
}

file static class FileScopedHandler
{
    public static async ValueTask<IResult> Handle(HttpRequest request,
                                                  [FromServices] Delete.DeleteOrder deleteOrder,
                                                  CancellationToken cancellationToken)
    {
        return await HttpHandler.Delete(request,
                                        TryGetOrderId,
                                        async (orderId, eTag) => await deleteOrder(orderId, eTag, cancellationToken));
    }

    private static Either<string, OrderId> TryGetOrderId(string orderId)
    {
        return Guid.TryParse(orderId, out var guid)
                ? new OrderId { Value = guid }
                : "Order ID must be a GUID.";
    }
}

file static class FileScopedServices
{
    public static void Configure(IServiceCollection services)
    {
        services.AddSingleton(DeleteOrder);
    }

    private static Delete.DeleteOrder DeleteOrder(IServiceProvider provider)
    {
        return async (orderId, eTag, cancellationToken) => await ValueTask.FromResult(Prelude.unit);
    }
}