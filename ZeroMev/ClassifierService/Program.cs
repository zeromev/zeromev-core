using ZeroMev.ClassifierService;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Classifier>();
    })
    .Build();

await host.RunAsync();
