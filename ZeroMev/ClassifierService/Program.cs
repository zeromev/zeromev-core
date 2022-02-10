using ZeroMev.ClassifierService;
using ZeroMev.SharedServer;

ConfigBuilder.Build();

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Classifier>();
    })
    .Build();

await host.RunAsync();
