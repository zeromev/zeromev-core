using Microsoft.AspNetCore.ResponseCompression;
using System.Collections;
using ZeroMev.Shared;
using ZeroMev.SharedServer;

ConfigBuilder.Build();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

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

app.MapGet("/zmblock/{id}", (long id) =>
{
    ZMBlock zb = DB.GetZMBlock(id);
    if (zb == null)
        zb = new ZMBlock(id, null);
    BitArray bundles = DB.ReadFlashbotsBundles(id);
    zb.Bundles = bundles;
    return Results.Json(zb, ZMSerializeOptions.Default);
});

app.Run();