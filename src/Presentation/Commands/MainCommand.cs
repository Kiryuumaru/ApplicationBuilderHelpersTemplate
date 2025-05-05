using Application.Configuration.Interfaces;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Presentation.Commands;

public class MainCommand : BaseCommand<HostApplicationBuilder>
{
    public override IApplicationConstants ApplicationConstants { get; } = new ApplicationConstants();

    public MainCommand() : base("init", "Init subcommand.")
    {
    }

    protected override ValueTask<HostApplicationBuilder> ApplicationBuilder(CancellationToken stoppingToken)
    {
        return new ValueTask<HostApplicationBuilder>(Host.CreateApplicationBuilder());
    }
}

internal class ApplicationConstants : IApplicationConstants
{
    public string AppNameSnakeCase { get; } = "vianactl";

    public string AppTag { get; } = Build.Constants.AppTag;
}
