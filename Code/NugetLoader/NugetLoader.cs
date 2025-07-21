using CsharpOnlineCompiler.Code.NugetLoader.Response_Models;
using CsharpOnlineCompiler.Code.NugetLoader.ResponseModels;
using Microsoft.CodeAnalysis;
using NuGet.Packaging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

using BlazorBoot = CsharpOnlineCompiler.Code.CompilerCode.BlazorBoot;

namespace CsharpOnlineCompiler.Code.NugetLoader
{
    public class NugetLoader
    {
        public NugetLoader()
        {
            httpClient = new HttpClient();
            disposeHttpClient = true;

            initializeMetadataReferences();
        }

        public NugetLoader(HttpClient client)
        {
            httpClient = client;

            initializeMetadataReferences();
        }

        public NugetLoader(HashSet<string> existingReferences, List<MetadataReference> references, HttpClient client)
        {
            this.existingReferences = existingReferences;
            this.references = references;
            httpClient = client;
        }

        ~NugetLoader()
        {
            if (disposeHttpClient)
                httpClient.Dispose();
        }

        #region Properties and variables ////////////////////////////////////////////////////////////////

        bool disposeHttpClient = false;

        private static Task initializationTask = Task.CompletedTask;

        private HttpClient httpClient;
        private HashSet<string> existingReferences;
        private List<MetadataReference> references;


        //This url https://api.nuget.org/v3/index.json returns the JSON with NuGet endpoints.

        //For now I'll just hardcode the primary query(with autocomplete) endpoint.
        //Maybe the secondary should be added as fallback.
        //And maybe the url should be retrieved by "@type": "SearchAutocompleteService" in case it changes.
        private readonly string autocompleteUrlBase = "https://azuresearch-usnc.nuget.org/autocomplete";
        private readonly string queryUrlBase = "https://azuresearch-usnc.nuget.org/query";

        #endregion //////////////////////////////////////////////////////////////////////////////////////


        #region Methods /////////////////////////////////////////////////////////////////////////////////

        private void initializeMetadataReferences()
        {
            async Task InitializeInternal()
            {
                var response = await httpClient.GetFromJsonAsync<BlazorBoot>("_framework/blazor.boot.json");

                existingReferences = response.resources.assembly.Keys.ToHashSet();

                var assemblies = await Task.WhenAll(response.resources.assembly.Keys.Select(x => httpClient.GetAsync("_framework/" + x)));

                references = new List<MetadataReference>(assemblies.Length);
                foreach (var asm in assemblies)
                {
                    using (var task = await asm.Content.ReadAsStreamAsync())
                    {
                        references.Add(MetadataReference.CreateFromStream(task));
                    }
                }
            }

            initializationTask = InitializeInternal();
        }



        public async Task<IEnumerable<string>> QueryAutoComplete(string query)
        {
            string queryURL = $"{autocompleteUrlBase}?q={query}";

            string jsonReponse = await httpClient.GetStringAsync(queryURL);

            var autocompleteResponse = JsonSerializer.Deserialize<AutocompleteResponseModel>(jsonReponse);

            return autocompleteResponse.Data;
        }

        public async Task<PackageData> QueryPackageDetails(string nugetPackageName)
        {
            string queryURL = $"{queryUrlBase}?q={nugetPackageName}";

            string jsonReponse = await httpClient.GetStringAsync(queryURL);

            var packageInfo = JsonSerializer.Deserialize<Packages>(jsonReponse);

            var packageData = packageInfo.Data.Where(x => x.PackageId == nugetPackageName).First();

            return packageData;
        }

        public async Task<IEnumerable<(string name, Stream stream)>> GetPackageAndDependencies(string selectedNugetPackageUrl, HashSet<string> existingPackages)
        {
            string jsonReponse = await httpClient.GetStringAsync(selectedNugetPackageUrl);

            var packageModel = JsonSerializer.Deserialize<PackageModel>(jsonReponse);

            string nupkgURL = packageModel.PackageContent;

            var response = await httpClient.GetStreamAsync(nupkgURL);

            //Wait for the metadata references to be initialized.
            if (initializationTask.Status != TaskStatus.RanToCompletion)
                await initializationTask;

            return await getPackageAndDependencies(response, existingPackages);
        }

        private async Task<IEnumerable<(string name, Stream stream)>> getPackageAndDependencies(Stream stream, HashSet<string> existingPackages)
        {
            List<(string name, Stream stream)> packagesToInstall = new List<(string name, Stream stream)>();

            //Get package from stream.
            using PackageArchiveReader reader = new PackageArchiveReader(stream);
            NuspecReader nuspec = reader.NuspecReader;

            //Get the name of the package we'll be installing. 
            string currentPackegeId = nuspec.GetId();

            //If the package is already present skip it.
            if (existingPackages.Contains(currentPackegeId))
                return packagesToInstall;

            //Else get the best suited tfm .dll.
            string bestFrameworkVersionPath = reader.GetFiles()
                .Where(v => v.StartsWith("lib", StringComparison.OrdinalIgnoreCase))
                .Where(v => v.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => GetTfmPriority(v))
                .FirstOrDefault("");

            //If no .dlls are found inside the lib directory skip the package.
            if (string.IsNullOrEmpty(bestFrameworkVersionPath))
                return packagesToInstall;


            //Get the .dll from the package as a stream.
            var dllStream = reader.GetStream(bestFrameworkVersionPath);

            Stream seekableStream = new MemoryStream();
            dllStream.CopyTo(seekableStream);
            seekableStream.Position = 0;  // Reset position to the beginning

            //
            packagesToInstall.Add((currentPackegeId, seekableStream));
            //yield return reader.GetStream(sortedVersions.First());



            //Get just the name without the full path.
            string bestFrameworkVersion = bestFrameworkVersionPath.Split("/")[1];

            //Get .dll dependencies for the best/selected tfm. 
            IEnumerable<PackageDependencyGroup> values = nuspec.GetDependencyGroups().Where(d => d.TargetFramework.GetShortFolderName() == bestFrameworkVersion);

            //If no compatible dependencies are found, return an empty list.
            if (values.Count() == 0)
                return new List<(string name, Stream stream)>();

            //Select the the first and most appropriate dependency group.
            var selectedDependencyGroup = values.First();
            foreach (var dependency in selectedDependencyGroup.Packages)
            {
                //Get the package dependency version string.
                string dependencyVersionString = dependency.VersionRange.ToString();
                dependencyVersionString = dependencyVersionString[1..(dependencyVersionString.Length - 1)].Replace(" ", "").Split(",").First();

                //Download the information of the package dependency.
                var packageData = await QueryPackageDetails(dependency.Id);
                var version = packageData.Versions.Where(x => x.Version == dependencyVersionString).First();
                string dependencyUrl = version.Id;

                //Recursively get the dependencies of the dependencies.
                packagesToInstall.AddRange(await GetPackageAndDependencies(dependencyUrl, existingPackages));
            }

#if DEBUG
            //Print out the dependencies.
            foreach (var dependencyGroup in nuspec.GetDependencyGroups())
            {
                Console.WriteLine($" - {dependencyGroup.TargetFramework.GetShortFolderName()}");
                foreach (var dependency in dependencyGroup.Packages)
                {
                    Console.WriteLine($"   > {dependency.Id} {dependency.VersionRange}");
                }
            }
#endif

            return packagesToInstall;
        }

        private static float GetTfmPriority(string pathInPackage)
        {
            string tfm = pathInPackage.Split("/")[1];
            string[] multipleTfms = tfm.Split("+");

            if (multipleTfms.Count() > 1)
            {
                tfm = multipleTfms.OrderByDescending(v => local_GetTfmPriority(v)).First();

                return local_GetTfmPriority(tfm);
            }
            else
            {
                return local_GetTfmPriority(tfm);
            }



            static float local_GetTfmPriority(string tfm)
            {
                //Set priority based on the version type.
                string tfmVersion;
                if (tfm.Length < 3)
                    tfmVersion = tfm[^2..];
                else
                    tfmVersion = tfm[^3..];

                if (Regex.IsMatch(tfm, @"net\d+\.\d+")) //net
                    tfmVersion = "3" + tfmVersion;
                else if (Regex.IsMatch(tfm, @"netcoreapp\d+\.\d+")) //netcoreapp
                    tfmVersion = "2" + tfmVersion;
                else if (Regex.IsMatch(tfm, @"netstandard\d+\.\d+")) //netstandard
                    tfmVersion = "1" + tfmVersion;
                else //all other unsupported tfms
                    return -1;

                return float.Parse(tfmVersion);
            }
        }

        #endregion //////////////////////////////////////////////////////////////////////////////////////
    }
}
