using AbsolutePathHelpers;
using Application.AppEnvironment.Services;
using Application.Client.Common.Extensions;
using Application.Common.Extensions;
using Application.Common.Interfaces;
using Application.Common.Interfaces.Application;
using Application.Common.Services;
using Application.Server.Authorization.Extensions;
using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Attributes;
using Asp.Versioning;
using Domain.Authorization.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.JSInterop;
using Microsoft.OpenApi;
using Presentation.WebApp.Client;
using Presentation.WebApp.Filters;
using Scalar.AspNetCore;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Threading;
using static Microsoft.AspNetCore.Components.Web.RenderMode;

namespace Presentation.WebApp.Commands;

[Command("Main subcommand.")]
internal class MainCommand : Build.BaseCommand<WebApplicationBuilder>
{
    [CommandOption("urls", Description = "Server listening URLs (semicolon-separated)", EnvironmentVariable = "ASPNETCORE_URLS")]
    public required string Urls { get; set; }

    protected override ValueTask<WebApplicationBuilder> ApplicationBuilder(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls(Urls.Split(';', StringSplitOptions.RemoveEmptyEntries));
        
        return new ValueTask<WebApplicationBuilder>(builder);
    }

    public override void AddConfigurations(ApplicationHostBuilder applicationBuilder, IConfiguration configuration)
    {
        base.AddConfigurations(applicationBuilder, configuration);

        var urls = Urls.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var url = urls[0].Replace("::", "localhost");

        configuration.SetApiEndpoint(new Uri(url));
    }

    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

        services.AddScoped<ApiExceptionFilter>();
        services.AddScoped<Controllers.V1.Auth.Shared.AuthResponseFactory>();

        // Add services to the container.
        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        services.AddHttpClient(Options.DefaultName, (sp, client) =>
        {
            var applicationConstants = sp.GetRequiredService<IApplicationConstants>();
            client.DefaultRequestHeaders.Add("Client-Agent", applicationConstants.AppName);
        });

        services.AddHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), ["live"]);

        services.AddServiceDiscovery();

        services.ConfigureHttpClientDefaults(http =>
        {
            http.AddServiceDiscovery();
        });

        services.AddProblemDetails();
        services.AddOpenApi(options =>
        {
            options.ShouldInclude = _ => true;
        });

        // Add API Versioning
        services
            .AddApiVersioning(options =>
            {
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new UrlSegmentApiVersionReader()
                );
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ApiVersionSelector = new CurrentImplementationApiVersionSelector(options);
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        services.AddControllers(options =>
        {
            options.ModelBinderProviders.Insert(0, new ModelBinders.FromJwtModelBinderProvider());
            options.Filters.AddService<ApiExceptionFilter>();
        });

        // Add Authentication
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Add Authorization
        services.AddAuthorization();

        // Add SignalR
        services.AddSignalR();

        // Add Email Service (mock for now)
        services.AddSingleton<IEmailService, MockEmailService>();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = Build.Constants.AppTitle,
                Description = Build.Constants.AppDescription
            });

            // Add JWT Authentication to Swagger
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter your JWT token in the format: Bearer {your token}"
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });

            // Add generic filters that work with AllowedValuesAttribute
            options.SchemaFilter<AllowedValuesSchemaFilter>();

            options.EnableAnnotations();

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            options.IncludeXmlComments(xmlPath, true);
        });
    }

    public override void AddMappings(ApplicationHost applicationHost, IHost host)
    {
        base.AddMappings(applicationHost, host);

        var app = (WebApplication)host;

        app.UseExceptionHandler();

        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Presentation.WebApp.Client._Imports).Assembly);


        app.MapControllers();

        app.MapOpenApi();

        app.MapScalarApiReference(options =>
        {
            options.EnablePersistentAuthentication();
            options.AddPreferredSecuritySchemes("Bearer");
            options.WithTitle(Build.Constants.AppTitle);
            options.WithTheme(ScalarTheme.DeepSpace);
            options.HideModels();

            options.WithOpenApiRoutePattern("swagger/{documentName}/swagger.json");
            options.WithDefaultHttpClient(ScalarTarget.Python, ScalarClient.Curl);

            options.WithCustomCss("""
                .show-api-client-button span {
                    display: none;
                }
                .show-api-client-button::after {
                    content: 'Run Request';
                }
                """);
        });

        //app.MapGet("/", () => Results.Redirect("/scalar/v1"))
        //    .ExcludeFromDescription();
    }

    public override void AddMiddlewares(ApplicationHost applicationHost, IHost host)
    {
        base.AddMiddlewares(applicationHost, host);

        var app = (WebApplication)host;

        app.UseSwagger(options =>
        {
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
        });

        //app.UseHttpsRedirection();

        // Add Authentication and Authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();
    }
}
