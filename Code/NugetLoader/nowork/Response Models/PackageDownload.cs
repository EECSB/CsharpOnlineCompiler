using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CsharpOnlineCompiler.Code.NugetLoader.Response_Models
{
    public class PackageModel
    {
        [JsonPropertyName("@id")]
        public string Id { get; set; }

        [JsonPropertyName("@type")]
        public List<string> Type { get; set; }

        [JsonPropertyName("catalogEntry")]
        public string CatalogEntry { get; set; }

        [JsonPropertyName("listed")]
        public bool Listed { get; set; }

        [JsonPropertyName("packageContent")]
        public string PackageContent { get; set; }

        [JsonPropertyName("published")]
        public DateTime Published { get; set; }

        [JsonPropertyName("registration")]
        public string Registration { get; set; }

        [JsonPropertyName("@context")]
        public ContextModel Context { get; set; }
    }

    public class ContextModel
    {
        [JsonPropertyName("@vocab")]
        public string Vocab { get; set; }

        [JsonPropertyName("xsd")]
        public string Xsd { get; set; }

        [JsonPropertyName("catalogEntry")]
        public TypeDefinition CatalogEntry { get; set; }

        [JsonPropertyName("registration")]
        public TypeDefinition Registration { get; set; }

        [JsonPropertyName("packageContent")]
        public TypeDefinition PackageContent { get; set; }

        [JsonPropertyName("published")]
        public TypeDefinition Published { get; set; }
    }

    public class TypeDefinition
    {
        [JsonPropertyName("@type")]
        public string Type { get; set; }
    }

}
