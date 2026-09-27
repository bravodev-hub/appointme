using AppointMe.Aspire;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

if (!builder.ExecutionContext.IsPublishMode)
{
    DevCertificate.EnsureTrusted();
}

var passwordParameter = builder.AddParameter("sqlPassword", "Password1");

var sqlServer = builder
    .AddSqlServer("appointme-sql", passwordParameter)
    .WithImage("mssql/server:2025-CU1-ubuntu-24.04")
    .WithHostPort(60740)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var database = sqlServer.AddDatabase("AppointMeSql", "AppointMe");


var keycloak = builder.AddKeycloak("keycloak", 8082,
        builder.AddParameter("username", "admin"),
        builder.AddParameter("password", "admin"))
    .WithDataVolume("AppointMe-Keycloak")
    .WithRealmImport("appointme-realm.json")
    .WithLifetime(ContainerLifetime.Persistent);

// Keycloak's single endpoint is named "http", but Aspire switches its scheme to https whenever a trusted
// dev certificate is available. Handing the API this reference (instead of the fixed https URL in its
// appsettings.Development.json, which stays for running outside Aspire) keeps the API on whatever scheme
// Keycloak actually serves.
var keycloakEndpoint = keycloak.GetEndpoint("http");

var mailpit = builder.AddContainer("Mailpit", "axllent/mailpit")
    .WithEndpoint(8026, 8025, name: "web-ui", scheme: "http")
    .WithEndpoint(1026, 1025, name: "smtp-server")
    .WithLifetime(ContainerLifetime.Persistent);

var appointmeApi = builder.AddProject<AppointMe_Api>("appointme-api", launchProfileName: "https")
    .WithReference(database)
    .WithReference(keycloak)
    .WithEnvironment("Authentication__Keycloak__Authority", ReferenceExpression.Create($"{keycloakEndpoint}/realms/appointme"))
    .WithEnvironment("KeycloakAdmin__BaseUrl", keycloakEndpoint)
    .WaitFor(database)
    .WaitFor(keycloak)
    .WaitFor(mailpit)
    .WithExternalHttpEndpoints();

builder.AddViteApp(name: "appointme-frontend", workingDirectory: "../AppointMe.Frontend", packageManager: "npm")
    .WithNpmPackageInstallation()
    .WithHttpsEndpoint(port: 5173, env: "PORT")
    .WithReference(appointmeApi)
    .WaitFor(appointmeApi);

await builder.Build().RunAsync();
