using System.ComponentModel;
using System.Diagnostics;

namespace AppointMe.Aspire;

/// <summary>
/// Fails the AppHost fast when the ASP.NET Core HTTPS development certificate is missing or untrusted.
/// Without it the API's https profile cannot start and Aspire silently runs Keycloak over plain HTTP,
/// which otherwise surfaces much later as an opaque 500 from the login endpoint.
/// </summary>
internal static class DevCertificate
{
    private const string TrustCommand = "dotnet dev-certs https --trust";

    public static void EnsureTrusted()
    {
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

        int exitCode;
        try
        {
            using var process = Process.Start(new ProcessStartInfo(dotnet, "dev-certs https --check --trust")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            process.WaitForExit();
            exitCode = process.ExitCode;
        }
        catch (Win32Exception exception)
        {
            Console.Error.WriteLine($"warning: could not verify the HTTPS development certificate ({exception.Message}).");
            return;
        }

        if (exitCode == 0)
        {
            return;
        }

        // Linux has no single system trust store the dev-certs tool can verify against, so a failed
        // check there is often a false negative — warn instead of blocking startup.
        if (OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine(
                $"warning: the HTTPS development certificate is missing or not trusted. Run '{TrustCommand}' if HTTPS fails.");
            return;
        }

        Console.Error.WriteLine(
            $"""
             error: the ASP.NET Core HTTPS development certificate is missing or not trusted.
             The API, the frontend and Keycloak all serve HTTPS locally and need it. Run once:

                 {TrustCommand}

             If a certificate exists but is still reported untrusted (e.g. created by an older SDK):

                 dotnet dev-certs https --clean
                 {TrustCommand}
             """);
        Environment.Exit(1);
    }
}
