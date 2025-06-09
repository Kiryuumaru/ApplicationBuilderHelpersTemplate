using Application.Common.Extensions;
using Application.Configuration.Interfaces;
using Application.LocalStore.Interfaces;
using Application.LocalStore.Services;
using Application.Logger.Extensions;
using ApplicationBuilderHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Presentation.Commands;

public class MainCommand : BaseCommand<HostApplicationBuilder>
{
    public MainCommand() : base("Main subcommand.")
    {
    }

    protected override ValueTask<HostApplicationBuilder> ApplicationBuilder(CancellationToken stoppingToken)
    {
        return new ValueTask<HostApplicationBuilder>(Host.CreateApplicationBuilder());
    }

    protected override async ValueTask Run(ApplicationHost<HostApplicationBuilder> applicationHost, CancellationToken stoppingToken)
    {
        await base.Run(applicationHost, stoppingToken);

        var logger = applicationHost.Services.GetRequiredService<ILogger<MainCommand>>();
        var localStoreFactory = applicationHost.Services.GetRequiredService<LocalStoreFactory>();

        using var _ = logger.BeginScopeMap<MainCommand>(scopeMap: new Dictionary<string, object?>
        {
            { "AppName", ApplicationConstants.AppName },
            { "AppTitle", ApplicationConstants.AppTitle },
            { "AppTag", ApplicationConstants.AppTag }
        });

        logger.LogTrace("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogDebug("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogInformation("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogWarning("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogError("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);
        logger.LogCritical("Running {AppName} ({AppTitle})", ApplicationConstants.AppName, ApplicationConstants.AppTitle);

        logger.LogInformation("Boolean (true): {Value}", true);
        logger.LogInformation("Boolean (false): {Value}", false);
        logger.LogInformation("Integer: {Value}", 123);
        logger.LogInformation("Float: {Value}", 123.45f);
        logger.LogInformation("Double: {Value}", 3.14159);
        logger.LogInformation("Decimal: {Value}", 99.99m);
        logger.LogInformation("String: {Value}", "Hello, world!");
        logger.LogInformation("Null: {Value}", null!);
        logger.LogInformation("DateTime: {Value}", DateTime.Now);
        logger.LogInformation("Guid: {Value}", Guid.NewGuid());
        logger.LogInformation("Object: {@Value}", new { Name = "Test", Age = 42 });
        logger.LogInformation("Array: {@Value}", new[] { 1, 2, 3 });
        logger.LogInformation("Enum: {Value}", ConsoleColor.Red);

        var localStore = await localStoreFactory.OpenStore(cancellationToken: stoppingToken);

        await localStore.Set("TestKey", "TestValue", stoppingToken);

        var value = await localStore.Get("TestKey", stoppingToken);

        logger.LogInformation("Retrieved value from local store: {Value}", value.Value);













        var sampleObj1 = new Dictionary<string, object>
        {
            { "Val1", "val 1" },
            { "Val2", 123 },
            { "Val3", DateTimeOffset.UtcNow }
        };
        var sampleObj2 = new SampleClass()
        {
            Val1 = "val 1",
            Val2 = 123,
            Val3 = DateTimeOffset.UtcNow,
        };

        //var ss1 = YamlHelpers.Serialize(sampleObj1);
        //var ss2 = YamlHelpers.Serialize(sampleObj2, new SS());

        //logger.LogInformation("Retrieved value from serialized yaml: {Value}", ss1);
        //logger.LogInformation("Retrieved value from serialized yaml: {Value}", ss2);


        // Setup the input
        var jsonConverted1 = await YamlJsonConverter.ConvertToJson(Document);
        var yamlConverted1 = await YamlJsonConverter.ConvertToYaml(jsonConverted1);
        var jsonConverted2 = await YamlJsonConverter.ConvertToJson(yamlConverted1);
        var yamlConverted2 = await YamlJsonConverter.ConvertToYaml(jsonConverted2);
        var jsonConverted3 = await YamlJsonConverter.ConvertToJson(yamlConverted2);
        var yamlConverted3 = await YamlJsonConverter.ConvertToYaml(jsonConverted3);
        var jsonConverted4 = await YamlJsonConverter.ConvertToJson(yamlConverted3);
        var yamlConverted4 = await YamlJsonConverter.ConvertToYaml(jsonConverted4);
        var jsonConverted5 = await YamlJsonConverter.ConvertToJson(yamlConverted4);
        var yamlConverted5 = await YamlJsonConverter.ConvertToYaml(jsonConverted5);
        var jsonConverted6 = await YamlJsonConverter.ConvertToJson(yamlConverted5);
        var yamlConverted6 = await YamlJsonConverter.ConvertToYaml(jsonConverted6);

        logger.LogInformation("--------------------------------------------------");
        logger.LogInformation("\n\norig:\n{Value}", Document);
        logger.LogInformation("--------------------------------------------------");
        logger.LogInformation("Yaml to json 1:\n{Value}", jsonConverted1[0].RootElement.ToString());
        logger.LogInformation("--------------------------------------------------");
        logger.LogInformation("Yaml to json n:\n{Value}", jsonConverted6[0].RootElement.ToString());
        logger.LogInformation("--------------------------------------------------");
        logger.LogInformation("json to yaml 4:\n{Value}", yamlConverted4);
        logger.LogInformation("--------------------------------------------------");
        logger.LogInformation("json to yaml 5:\n{Value}", yamlConverted5);
        logger.LogInformation("--------------------------------------------------");
        logger.LogInformation("json to yaml 6:\n{Value}", yamlConverted6);
        logger.LogInformation("--------------------------------------------------");

        var qwe = 1;
    }



    private const string Document = """
        specialDelivery: >+
          Follow the Yellow Brick
          Road to the Emerald City.
          Pay no attention to the
          man behind the curtain.


        """;
    private const string Document1 = """
        ---
        receipt:    Oz-Ware Purchase Invoice
        date:        2007-08-06
        customer:
          given:   Dorothy
          family:  Gale
        
        items:
        - part_no:   A4786
          descrip:   Water Bucket (Filled)
          price:     1.47
          quantity:  4
        
        - part_no:   E1628
          descrip:   High Heeled "Ruby" Slippers
          price:     100.27
          quantity:  1
        
        bill-to:  &id001
          street: |
            123 Tornado Alley
            Suite 16



          city:   East Westville
          state:  KS
        
        ship-to:  *id001
        
        specialDelivery:  >+
          Follow the Yellow Brick
          Road to the Emerald City.
          Pay no attention to the
          man behind the curtain.

        ---
        receipt:    MelcSS Ware Purchase Invoice
        date:        2007-08-07
        customer:
          given:   Clynt
          family:  Rupinta
        
        items:
        - part_no:   A4786
          descrip:   Water Bucket (Filled)
          price:     1.47
          quantity:  4
        """;

    [YamlSerializable]
    class SampleClass
    {
        public required string Val1 { get; set; }

        public required int Val2 { get; set; }

        public required DateTimeOffset Val3 { get; set; }
    }

    [YamlStaticContext]
    [YamlSerializable(typeof(SampleClass))]
    partial class SS : StaticContext
    {

    }
}
