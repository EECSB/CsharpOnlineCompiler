using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace CsharpOnlineCompiler
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddBlazoredLocalStorage();
            //Back ILocalStorageService with IndexedDB (larger quota + compression) instead of localStorage.
            //Registered after AddBlazoredLocalStorage so this implementation is the one resolved.
            builder.Services.AddScoped<ILocalStorageService, IndexedDbLocalStorage>();

            await builder.Build().RunAsync();
        }
    }
}
