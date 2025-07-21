using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Net.Http.Json;

namespace CsharpOnlineCompiler.Code.CompilerCode
{
    public class Compiler
    {
        #region Constructors ////////////////////////////////////

        public Compiler(HttpClient client)
        {
            InitializeMetadataReferences(client);
        }

        #endregion //////////////////////////////////////////////


        #region Models //////////////////////////////////////////



        #endregion /////////////////////////////////////////////


        #region Properties /////////////////////////////////////

        private Task initializationTask;

        public Task InitializationTask
        {
            get { return initializationTask; }
        }


        public Dictionary<string, byte[]> DynamicallyAddedDLLs { get; set; } = new Dictionary<string, byte[]>();
        public Dictionary<string, byte[]> ReferencesDLLs { get; set; } = new Dictionary<string, byte[]>();
        public List<MetadataReference> References { get; set; }
        public HashSet<string> ExistingReferences { get; set; }

        #endregion /////////////////////////////////////////////


        #region Initialization /////////////////////////////////

        static byte[] StreamToByteArray(Stream input)
        {
            //Initialize a byte array with the size of the stream.
            byte[] buffer = new byte[input.Length];

            //Read the entire stream into the buffer.
            int bytesRead = 0;
            while (bytesRead < buffer.Length)
            {
                int read = input.Read(buffer, bytesRead, buffer.Length - bytesRead);
                if (read == 0) //Is end of stream.
                    break;
                bytesRead += read;
            }

            return buffer;
        }

        private void InitializeMetadataReferences(HttpClient client)
        {
            async Task InitializeInternal()
            {
                var response = await client.GetFromJsonAsync<BlazorBoot>("_framework/blazor.boot.json");

                ExistingReferences = response.resources.assembly.Keys.ToHashSet();

                var assemblies = await Task.WhenAll(response.resources.assembly.Keys.Select(x => client.GetAsync("_framework/" + x)));

                var references = new List<MetadataReference>(assemblies.Length);
                foreach (var asm in assemblies)
                {
                    if (asm.Headers.TryGetValues("Location", out var locationHeaderValues))
                    {
                        string targetUrl = locationHeaderValues.FirstOrDefault();
                    }

                    string nameOfDll = asm.RequestMessage.RequestUri.AbsoluteUri.Split("/").Last();

                    using (var task = await asm.Content.ReadAsStreamAsync())
                    {
                        ReferencesDLLs.Add(nameOfDll, StreamToByteArray(task));
                        task.Seek(0, SeekOrigin.Begin);
                        var asdf = MetadataReference.CreateFromStream(task);
                        references.Add(asdf);
                    }
                }

                References = references;
            }

            initializationTask = InitializeInternal();
        }

        public void AddLateReference(IEnumerable<(string name, Stream stream)> dllStreams, bool skipDuplicates = false)
        {
            foreach (var stream in dllStreams)
            {
                if (ExistingReferences.Contains(stream.name))
                {
                    continue;
                }
                else
                {
                    ExistingReferences.Add(stream.name);

                    //var reference = MetadataReference.CreateFromStream(stream.stream);
                    //References.Add(reference);

                    using (var memoryStream = new MemoryStream())
                    {
                        stream.stream.CopyTo(memoryStream);
                        byte[] byteArray = memoryStream.ToArray();
                        DynamicallyAddedDLLs.Add(stream.name, byteArray);

                        var reference = MetadataReference.CreateFromImage(byteArray);
                        References.Add(reference);
                    }
                }
            }
        }

        #endregion /////////////////////////////////////////////


        #region Entry point /////////////////////////////////////

        public async Task<CompilationResult> GetCompilation(string script, LanguageVersion languageVersion, OptimizationLevel optimizationLevel)
        {
            //Wait for the metadata references to be initialized.
            if (initializationTask.Status != TaskStatus.RanToCompletion)
                await initializationTask;

            //Compile the source code and return the result.
            return compile(script, languageVersion, optimizationLevel);
        }

        #endregion /////////////////////////////////////////////


        #region Methods ////////////////////////////////////////

        public void DynamicallyAddReference(Stream stream)
        {
            References.Add(MetadataReference.CreateFromStream(stream));
        }

        private CompilationResult compile(string source, LanguageVersion languageVersion = LanguageVersion.Latest, OptimizationLevel optimizationLevel = OptimizationLevel.Debug)
        {
            #region Compile ////////////////////////////////////////

            //Parse the source code into a syntax tree.

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(languageVersion));

            /*//Set the compilation options and compile the syntax tree.
            var compilation = CSharpCompilation.Create
            (
                "DynamicCode",
                new List<SyntaxTree>() { syntaxTree }, 
                References, 
                new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary, //OutputKind.ConsoleApplication
                    optimizationLevel: OptimizationLevel.Debug, //Release
                    allowUnsafe: true
                ).WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
                {
                    //{ "CS0219", ReportDiagnostic.Suppress }, //Disable warning CS0219 "The variable 'unusedVariable' is assigned but its value is never used"
                    { "CS1998", ReportDiagnostic.Suppress }
                })
            );*/

            var compilation = CSharpCompilation.Create("DynamicCode")
            .WithOptions(new CSharpCompilationOptions(
                outputKind: OutputKind.ConsoleApplication,
                concurrentBuild: false, //https://stackoverflow.com/questions/68716771/why-csharpcompilation-getdiagnostics-method-in-blazor-wasm-application-throw-sys
                optimizationLevel: optimizationLevel,
                allowUnsafe: true
                ))
            .AddReferences(References)
            .AddSyntaxTrees(syntaxTree);

            #endregion /////////////////////////////////////////////


            #region Create message list from compilation results ///

            //Get the diagnostic messages from the compilation, create a list of message strings from them.
            ImmutableArray<Diagnostic> diagnostics = compilation.GetDiagnostics();

            var messageList = new List<CompilationResult.CompilationMessage>();
            bool error = false;
            foreach (Diagnostic diagnostic in diagnostics)
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    error = true;

                var newMessage = new CompilationResult.CompilationMessage
                (
                    diagnostic.Severity,
                    diagnostic.ToString()
                );

                messageList.Add(newMessage);
            }

            #endregion /////////////////////////////////////////////


            //If there were errors, return the error messages.
            if (error)
                return new CompilationResult(!error, messageList, null);


            //If there were no errors, create a byte[] from the compiled assembly and return it.
            byte[] assembly = null;
            using (var outputAssembly = new MemoryStream())
            {
                compilation.Emit(outputAssembly);
                assembly = outputAssembly.ToArray();
            }

            return new CompilationResult(!error, messageList, assembly);
        }

        #endregion //////////////////////////////////////////////
    }
}