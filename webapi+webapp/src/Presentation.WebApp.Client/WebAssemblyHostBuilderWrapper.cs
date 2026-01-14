using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;

namespace Presentation.WebApp.Client;

internal class WebAssemblyHostBuilderWrapper(WebAssemblyHostBuilder webAssemblyHostBuilder) : IHostApplicationBuilder
{
    public WebAssemblyHostBuilder WebAssemblyHostBuilder => webAssemblyHostBuilder;

    public IDictionary<object, object> Properties => new Dictionary<object, object>();

    public IConfigurationManager Configuration => new WrappedConfigurationManager(webAssemblyHostBuilder.Services, webAssemblyHostBuilder);

    public IHostEnvironment Environment => new WrappedHostEnvironment(webAssemblyHostBuilder.Services, webAssemblyHostBuilder);

    public ILoggingBuilder Logging => webAssemblyHostBuilder.Logging;

    public IMetricsBuilder Metrics => new WrappedMetricsBuilder(webAssemblyHostBuilder.Services);

    public IServiceCollection Services => webAssemblyHostBuilder.Services;

    public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull
        => webAssemblyHostBuilder.ConfigureContainer(factory, configure);

    private sealed class WrappedHostEnvironment(IServiceCollection services, WebAssemblyHostBuilder webAssemblyHostBuilder) : IHostEnvironment
    {
        public IServiceCollection Services { get; } = services;
        public string EnvironmentName { get => webAssemblyHostBuilder.HostEnvironment.Environment; set => throw new NotImplementedException(); }
        public string ApplicationName { get => Build.ApplicationConstants.Instance.AppName; set => throw new NotImplementedException(); }
        public string ContentRootPath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public IFileProvider ContentRootFileProvider { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }

    private sealed class WrappedMetricsBuilder(IServiceCollection services) : IMetricsBuilder
    {
        public IServiceCollection Services { get; } = services;
    }

    private sealed class WrappedConfigurationManager(IServiceCollection services, WebAssemblyHostBuilder webAssemblyHostBuilder) : IConfigurationManager
    {
        public string? this[string key] { get => webAssemblyHostBuilder.Configuration[key]; set => webAssemblyHostBuilder.Configuration[key] = value; }

        public IServiceCollection Services { get; } = services;

        public IDictionary<string, object> Properties => (webAssemblyHostBuilder.Configuration as IConfigurationBuilder).Properties;

        public IList<IConfigurationSource> Sources => (webAssemblyHostBuilder.Configuration as IConfigurationBuilder).Sources;

        public IConfigurationBuilder Add(IConfigurationSource source)
            => webAssemblyHostBuilder.Configuration.Add(source);

        public IConfigurationRoot Build()
            => webAssemblyHostBuilder.Configuration.Build();

        public IEnumerable<IConfigurationSection> GetChildren()
            => (webAssemblyHostBuilder.Configuration as IConfiguration).GetChildren();

        public IChangeToken GetReloadToken()
            => webAssemblyHostBuilder.Configuration.GetReloadToken();

        public IConfigurationSection GetSection(string key)
            => webAssemblyHostBuilder.Configuration.GetSection(key);
    }
}
