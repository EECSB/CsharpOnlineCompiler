using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CsharpOnlineCompiler.Code.NugetLoader.ResponseModels
{
    public class Context
    {
        [JsonPropertyName("@vocab")]
        public string Vocab { get; set; }
    }

    public class AutocompleteResponseModel
    {
        [JsonPropertyName("@context")]
        public Context Context { get; set; }

        [JsonPropertyName("totalHits")]
        public int TotalHits { get; set; }

        [JsonPropertyName("data")]
        public string[] Data { get; set; }
    }

}
