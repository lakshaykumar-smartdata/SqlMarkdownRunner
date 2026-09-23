using SqlMarkdownRunner;
using SqlMarkdownRunner.Components;

if (args.Contains("--selftest")) { SelfTest.Run(); return; }

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<ConnectionStore>();
builder.Services.AddSingleton<HistoryStore>();
builder.Services.AddSingleton<ObjectCache>();
builder.Services.AddSingleton<SavedRuns>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
