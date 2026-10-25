using AppointMe.Api.Antiforgery;
using AppointMe.Api.ApiVersioning;
using AppointMe.Api.Authentication;
using AppointMe.Api.Authentication.DemoLogin;
using AppointMe.Api.Authorization;
using AppointMe.Api.DataProtection;
using AppointMe.Api.ErrorHandling;
using AppointMe.Api.Hangfire;
using AppointMe.Api.Json;
using AppointMe.Api.MultiTenancy;
using AppointMe.Api.OpenApi;
using AppointMe.Api.OpenTelemetry;
using AppointMe.Api.SecurityHeaders;
using AppointMe.Api.Wolverine;
using AppointMe.Booking.Configuration;
using AppointMe.Crm.Configuration;
using AppointMe.Identity.Configuration;
using AppointMe.Organizations.Configuration;
using AppointMe.Shared.Configuration;
using AppointMe.Shared.Database.Migrations;
using AppointMe.Shared.Endpoints;
using JasperFx;
using Microsoft.AspNetCore.HttpOverrides;

var isCodegen = CodeGenerationDetection.IsRunningGeneration();

// Code-generation runs (Wolverine's `codegen` command and the build-time OpenAPI document) boot the app without its
// real configuration; the Codegen environment's appsettings.Codegen.json supplies placeholder values.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = isCodegen ? "Codegen" : null,
});

builder.Services
    .AddAppointMeErrorHandling()
    .AddAppointMeApiVersioning()
    .AddAppointMeMultiTenancy(tenancy => tenancy.FromHeader("X-Company-Id"))
    .AddAppointMeAuthentication(builder.Configuration)
    .AddAppointMeAuthorization()
    .AddAppointMeAntiforgery()
    .AddAppointMeJsonOptions()
    .AddAppointMeOpenApi()
    .AddAppointMeOpenTelemetry();

if (!isCodegen)
{
    builder.Services.AddAppointMeHangfire(builder.Configuration);
}

builder.Services
    .AddSharedModule(builder.Configuration)
    .AddIdentityModule(builder.Configuration)
    .AddOrganizationsModule()
    .AddCrmModule()
    .AddBookingModule();

if (isCodegen)
{
    // Build-time OpenAPI generation starts the host; it must not reach for the database.
    builder.Services.DisableDatabaseMigrations();
}

builder.Services.AddDemoMode(builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    // The Cloudflare Worker fronting devtest rewrites Host to the azurewebsites
    // origin and carries the public hostname here instead.
    options.ForwardedHostHeaderName = "X-Original-Host";
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

builder.Services.AddAppointMeDataProtection(builder.Configuration);

builder.Host.AddWolverine(builder.Configuration, builder.Environment, isCodegen);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseAppointMeSecurityHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseExceptionHandler();
app.UseStaticFiles();
app.UseAppointMeMultiTenancy();
app.UseAuthentication();
app.UseAuthorization();
app.UseAppointMeAntiforgery();

app.MapEndpoints();

if (!isCodegen)
{
    app.MapAppointMeHangfireDashboard();
}

app.MapFallbackToFile("index.html").AllowAnonymous();

return await app.RunJasperFxCommands(args);
