using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace api.tests;

internal sealed class TestFactory : WebApplicationFactory<Program>
{
    public Action<IServiceCollection> ConfigureServices { get; init; } = _ => { };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(ConfigureServices);
    }
}
