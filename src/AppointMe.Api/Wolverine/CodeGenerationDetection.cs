using System.Reflection;

namespace AppointMe.Api.Wolverine;

// Pattern from https://wolverinefx.net/guide/codegen.html.
public static class CodeGenerationDetection
{
    public static bool IsRunningGeneration()
    {
        return IsGeneratingOpenApiDocument() || Environment.GetCommandLineArgs().Contains("codegen");
    }

    // Build-time OpenAPI generation (Microsoft.Extensions.ApiDescription.Server) runs the app under this entry
    // assembly and starts the host against a mock server.
    public static bool IsGeneratingOpenApiDocument()
    {
        return Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
    }
}
