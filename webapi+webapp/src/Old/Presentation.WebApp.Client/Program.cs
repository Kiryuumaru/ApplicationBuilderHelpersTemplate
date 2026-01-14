using Application.Client;
using Application.Client.Authentication.Interfaces;
using Application.Client.Authentication.Services;
using Application.Client.Iam.Interfaces;
using Application.Client.Iam.Services;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Presentation.WebApp.Client;
using Presentation.WebApp.Client.Services;
using Presentation.WebApp.Client.Commands;
using Application.Client.Authentication.Interfaces.Infrastructure;

return await ApplicationBuilderHelpers.ApplicationBuilder.Create()
    .AddApplication<ClientApplication>()
    .AddCommand<MainCommand>()
    .RunAsync(args);
