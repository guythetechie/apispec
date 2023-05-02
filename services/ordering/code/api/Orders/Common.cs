using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace api.Orders;

internal static class Services
{
    public static void Configure(IServiceCollection services)
    {
        Delete.ConfigureServices(services);
    }
}

internal static class Endpoints
{
    public static void Configure(IEndpointRouteBuilder builder)
    {
        var groupBulder = builder.MapGroup("/v1/orders");

        Delete.ConfigureEndpoint(groupBulder);
    }
}