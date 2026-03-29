using BmsTelemetry.Components;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.AddAppSettings(builder.Configuration, builder.Environment);

// Logging
builder.AddAppLogging();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCertificateSource(builder.Environment);
builder.Services.AddIotDevice(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<IBmsHandlerFactory>();
builder.Services.AddSingleton<IBmsHandlerRegistry, BmsHandlerRegistry>();
builder.Services.AddSingleton<CertificateProvider>();
builder.Services.AddSingleton<KeyvaultService>();
builder.Services.AddSingleton<DpsService>();
builder.Services.AddSingleton<UptimeService>();
builder.Services.AddSingleton<DbReader>();

builder.Services.AddAppDatabase("Data/database.db");

// Workers
builder.Services.AddHostedService<BmsSupervisor>();
builder.Services.AddHostedService<TelemetryWorker>();

var app = builder.Build();

// Ensure DB exists
app.EnsureAppDatabaseCreated();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
