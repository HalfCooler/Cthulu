using Cthulu.Application;
using Cthulu.Application.Rooms;
using Cthulu.Application.Views;
using Cthulu.Domain.Cards;
using Cthulu.Web.Components;
using Cthulu.Web.Hubs;
using Cthulu.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
    {
        options.DetailedErrors = true;
    });
}

builder.Services.AddSignalR();

builder.Services.Configure<GameOptions>(builder.Configuration.GetSection(GameOptions.SectionName));

// Development: enable fixed-seed debug unless explicitly disabled in config.
if (builder.Environment.IsDevelopment())
{
    builder.Services.PostConfigure<GameOptions>(opts =>
    {
        var configured = builder.Configuration.GetSection(GameOptions.SectionName)["AllowDebugSeed"];
        if (string.IsNullOrEmpty(configured))
            opts.AllowDebugSeed = true;
    });
}

// Apply arcana enable list once at startup (empty = all).
var gameSection = builder.Configuration.GetSection(GameOptions.SectionName);
var enabledNames = gameSection.GetSection("EnabledArcana").Get<string[]>() ?? [];
var parsed = CardCatalog.ParseArcanaNames(enabledNames);
CardCatalog.ConfigureEnabledArcana(parsed.Count == 0 ? null : parsed);

builder.Services.AddSingleton<IGameRoomStore, InMemoryGameRoomStore>();
builder.Services.AddSingleton<ViewProjector>();
builder.Services.AddSingleton<RoomService>();
builder.Services.AddScoped<GameClientService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<GameHub>("/hubs/game");

app.Run();
