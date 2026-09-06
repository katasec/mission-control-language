using ForgeMission.Presentation;
using ForgeMission.Application.Transport;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped<IApplicationChannel>(services =>
{
    var baseUri = new Uri(services.GetRequiredService<NavigationManager>().BaseUri);
    return new HttpApplicationChannel(baseUri, request => request.SetBrowserResponseStreamingEnabled(true));
});

await builder.Build().RunAsync();
