using Microsoft.AspNetCore.ResponseCompression;
using System.Text;
using System.Text.Json;
using System.Collections;
using ZeroMev.Shared;
using ZeroMev.SharedServer;
using ZeroMev.Server;

ConfigBuilder.Build();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddHostedService<CacheService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();


app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.MapGet("/zmhealth", () => "ok");

app.MapGet("/zmsummary", () => DB.MEVLiteCacheJson);

app.MapGet("/zmblock/{id}", async (long id) =>
{
    return Results.Text(await DB.GetZmBlockJson(id));
});

app.Run();