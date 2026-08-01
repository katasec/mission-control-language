using ForgeMission.ClientRuntime.Presentation;
using ForgeMission.ClientRuntime.Transport;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped<IClientRuntimeChannel>(services =>
    new HttpClientRuntimeChannel(new Uri(services.GetRequiredService<NavigationManager>().BaseUri)));

await builder.Build().RunAsync();
