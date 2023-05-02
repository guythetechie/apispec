using core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace api;

public sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureBuilder(builder);

        var application = builder.Build();
        ConfigureApplication(application);

        await application.RunAsync();
    }

    private static void ConfigureBuilder(WebApplicationBuilder builder)
    {
        ConfigureServices(builder.Services);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddApplicationInsightsTelemetry();
        services.AddAuthentication();

        ConfigureJson(services);

        Orders.Services.Configure(services);
    }

    private static void ConfigureJson(IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
        });
    }

    private static void ConfigureApplication(WebApplication application)
    {
        application.UseExceptionHandler(ConfigureExceptionHandler);
        application.UseStatusCodePages();

        ConfigureRoutes(application);
    }

    private static void ConfigureExceptionHandler(IApplicationBuilder builder)
    {
        builder.Run(async context => await TypedResults.Json(new ApiError { Code = new ApiErrorCode.InternalServerError(), Message = "An error has occurred." }, statusCode: StatusCodes.Status500InternalServerError)
               .ExecuteAsync(context));
    }

    private static void ConfigureRoutes(WebApplication application)
    {
        Orders.Endpoints.Configure(application);
    }
}