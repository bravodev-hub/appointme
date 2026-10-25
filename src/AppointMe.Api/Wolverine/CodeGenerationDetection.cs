using System.Reflection;

namespace AppointMe.Api.Wolverine;

// Pattern from https://wolverinefx.net/guide/codegen.html.
public static class CodeGenerationDetection
{
    public static bool IsRunningGeneration()
    {
        return Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider"
               || Environment.GetCommandLineArgs().Contains("codegen");
    }
}
