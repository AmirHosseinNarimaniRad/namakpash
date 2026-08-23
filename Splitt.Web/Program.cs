using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Splitt.Web;
using Splitt.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<Store>();

// The store is opened by App.razor on its first render, not here: JS interop is not usable
// before the host runs, and awaiting a module import at this point never returns.
await builder.Build().RunAsync();
