using AbsolutePathHelpers;
using Application.AppEnvironment.Services;
using Application.Authorization.Extensions;
using Application.Common.Extensions;
using Application.Common.Interfaces;
using Application.Common.Services;
using Application.Configuration.Extensions;
using Application.Logger.Extensions;
using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Attributes;
using Asp.Versioning;
using Domain.Authorization.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
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
using Microsoft.OpenApi;
using Presentation.WebApi.ConfigureOptions;
using Presentation.WebApi.Models.SchemaFilters;
using Scalar.AspNetCore;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Presentation.WebApi.Commands;

[Command("Main subcommand.")]
internal class MainCommand : Build.BaseCommand<WebApplicationBuilder>
{
    [CommandOption("file-asset-vault-registry-dir", Description = "Viana edge grid use a file asset vault registry dir instead of service registered from infrastructure", EnvironmentVariable = "VIANA_EDGE_GRID_FILE_ASSET_VAULT_REGISTRY_DIR")]
    public AbsolutePath? FileAssetVaultRegistryPath { get; set; }

    [CommandOption("urls", Description = "Server listening URLs (semicolon-separated)", EnvironmentVariable = "ASPNETCORE_URLS")]
    public string? Urls { get; set; }

    protected override ValueTask<WebApplicationBuilder> ApplicationBuilder(CancellationToken stoppingToken)
    {
        var builder = WebApplication.CreateBuilder();
        
        // Configure URLs if specified
        if (!string.IsNullOrEmpty(Urls))
        {
            builder.WebHost.UseUrls(Urls.Split(';', StringSplitOptions.RemoveEmptyEntries));
        }
        
        return new ValueTask<WebApplicationBuilder>(builder);
    }

    public override void AddServices(ApplicationHostBuilder applicationBuilder, IServiceCollection services)
    {
        base.AddServices(applicationBuilder, services);

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
        });

        // Add Authentication
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.ConfigureOptions<ConfigureJwtBearerOptions>();

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

        app.MapGet("/", () => Results.Redirect("/scalar/v1"))
            .ExcludeFromDescription();
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
