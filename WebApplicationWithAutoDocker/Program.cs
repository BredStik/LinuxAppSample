using System.Text.Json;
using Azure.Data.Tables;
using WebApplicationWithAutoDocker;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<VersionProvider>(_ => new VersionProvider());
builder.Services.AddHostedService<PingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/currentversion", (VersionProvider vp) =>
    {
        var version = vp.GetVersion();
        return $"{version.Id} started on {version.StartedOn}";
    })
    .WithName("currentversion")
    .WithOpenApi();

app.MapGet("/ping", async (HttpContext context,  VersionProvider vp, IConfiguration configuration) =>
{
    context.Response.ContentType = "text/html";

    var tableClient = new TableClient(configuration.GetConnectionString("PingTable"), "Ping");

    var entries = tableClient.QueryAsync<TableEntity>(e => e.Timestamp >= DateTimeOffset.UtcNow.AddMinutes(-15), 100);

    var serverDictionary = new Dictionary<Guid, DateTimeOffset>();
    
    await foreach (var page in entries.AsPages())
    {
        serverDictionary = page.Values.GroupBy(x => x.PartitionKey).ToDictionary(x => Guid.Parse(x.Key), x => DateTimeOffset.Parse(x.Max(e => e.RowKey)));
        break;
    }

    return new { ServerId = vp.GetVersion().Id, LatestByServer = serverDictionary};
});

app.MapGet("/pingLive", (HttpContext context,  VersionProvider vp, IConfiguration configuration, CancellationToken ct) =>
{
    var tableClient = new TableClient(configuration.GetConnectionString("PingTable"), "Ping");
    
    async IAsyncEnumerable<KeyValuePair<Guid, DateTimeOffset>> Stream()
    {
        while (!ct.IsCancellationRequested)
        {
            var entries = tableClient.QueryAsync<TableEntity>(e => e.Timestamp >= DateTimeOffset.UtcNow.AddMinutes(-15), 100);

            var serverDictionary = new Dictionary<Guid, DateTimeOffset>();
    
            await foreach (var page in entries.AsPages())
            {
                serverDictionary = page.Values.GroupBy(x => x.PartitionKey).ToDictionary(x => Guid.Parse(x.Key), x => DateTimeOffset.Parse(x.Max(e => e.RowKey)));
                break;
            }

            foreach (var kv in serverDictionary)
            {
                yield return kv;
            }
            
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    return Stream();
});

app.MapGet("/pingSse", async (HttpContext context,  VersionProvider vp, IConfiguration configuration, CancellationToken ct) =>
   {
       var tableClient = new TableClient(configuration.GetConnectionString("PingTable"), "Ping");
       
       context.Response.Headers.Append("Content-Type", "text/event-stream");
    
       while (!ct.IsCancellationRequested)
       {
           var entries = tableClient.QueryAsync<TableEntity>(e => e.Timestamp >= DateTimeOffset.UtcNow.AddMinutes(-15), 100);
   
           var serverDictionary = new Dictionary<Guid, DateTimeOffset>();
       
           await foreach (var page in entries.AsPages())
           {
               serverDictionary = page.Values.GroupBy(x => x.PartitionKey).ToDictionary(x => Guid.Parse(x.Key), x => DateTimeOffset.Parse(x.Max(e => e.RowKey)));
               break;
           }
           
           
           await context.Response.WriteAsync($"data: ");
           await JsonSerializer.SerializeAsync(context.Response.Body, serverDictionary);
           await context.Response.WriteAsync($"\n\n");
           await context.Response.Body.FlushAsync();

           await Task.Delay(TimeSpan.FromSeconds(30));
       }
   });

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}