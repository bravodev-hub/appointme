namespace AppointMe.Api.Wolverine;

// Pattern from https://wolverinefx.net/guide/codegen.html.
public static class CodeGenerationDetection
{
    public static bool IsRunningCodegenCommand()
    {
        return Environment.GetCommandLineArgs().Contains("codegen");
    }
}
