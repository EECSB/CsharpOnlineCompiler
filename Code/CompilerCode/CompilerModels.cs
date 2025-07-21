using Microsoft.CodeAnalysis;

namespace CsharpOnlineCompiler.Code.CompilerCode
{
    public class BlazorBoot
    {
        public bool cacheBootResources { get; set; }
        public object[] config { get; set; }
        public bool debugBuild { get; set; }
        public string entryAssembly { get; set; }
        public bool linkerEnabled { get; set; }
        public Resources resources { get; set; }
    }

    public class Resources
    {
        public Dictionary<string, string> assembly { get; set; }
        public Dictionary<string, string> pdb { get; set; }
        public Dictionary<string, string> runtime { get; set; }
    }

    public class CompilationResult
    {
        public CompilationResult(bool success, List<CompilationMessage> messages, byte[] assemblyDLL)
        {
            Success = success;
            Messages = messages;
            AssemblyDLL = assemblyDLL;
        }

        public bool Success { get; set; }
        public List<CompilationMessage> Messages { get; set; }
        public byte[] AssemblyDLL { get; set; }

        public class CompilationMessage
        {
            public CompilationMessage(DiagnosticSeverity severity, string message)
            {
                Message = message;
                Severity = severity;
            }

            public string Message { get; set; }
            public DiagnosticSeverity Severity { get; set; }
        }
    }
}
