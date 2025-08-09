using AbsolutePathHelpers;
using Application.Abstractions.Application;
using ApplicationBuilderHelpers;
using ApplicationBuilderHelpers.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Application.Common.Configuration.Extensions;

namespace Presentation.Commands;

public abstract class BaseCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] THostApplicationBuilder> : Application.Abstractions.Application.BaseCommand<THostApplicationBuilder>
    where THostApplicationBuilder : IHostApplicationBuilder
{
    public override IApplicationConstants ApplicationConstants { get; } = Build.ApplicationConstants.Instance;
}
