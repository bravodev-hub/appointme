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
using AppointMe.Api.Wolverine;
using AppointMe.Booking.Configuration;
using AppointMe.Crm.Configuration;
using AppointMe.Identity.Configuration;
using AppointMe.Organizations.Configuration;
using AppointMe.Shared.Configuration;
using AppointMe.Shared.Endpoints;
using JasperFx;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

var isCodegen = CodeGenerationDetection.IsRunningGeneration();

builder.Services
    .AddAppointMeErrorHandling()
    .AddAppointMeApiVersioning()
    .AddAppointMeMultiTenancy(tenancy => tenancy.FromHeader("X-Company-Id"))
    .AddAppointMeAuthentication(builder.Configuration)
    .AddAppointMeAuthorization()
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

builder.Services.AddDemoMode(builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddAppointMeDataProtection(builder.Configuration);

builder.Host.AddWolverine(builder.Configuration, builder.Environment, isCodegen);

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseExceptionHandler();
app.UseStaticFiles();
app.UseAppointMeMultiTenancy();
app.UseAuthentication();
app.UseAuthorization();

app.MapEndpoints();

if (!isCodegen)
{
    app.MapAppointMeHangfireDashboard();
}

app.MapFallbackToFile("index.html").AllowAnonymous();

return await app.RunJasperFxCommands(args);
