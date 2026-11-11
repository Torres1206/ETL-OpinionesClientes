
using WorkerService1;
using WorkerService1.Extractors;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();

builder.Services.AddTransient<CsvExtractor>();
builder.Services.AddTransient<DatabaseExtractor>();

builder.Services.AddHttpClient<ApiExtractor>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

var host = builder.Build();
host.Run();
