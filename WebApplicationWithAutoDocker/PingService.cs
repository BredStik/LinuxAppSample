using Azure.Data.Tables;

namespace WebApplicationWithAutoDocker;

public class PingService: BackgroundService
{
    private readonly VersionProvider _vp;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PingService> _logger;

    public PingService(VersionProvider vp, IConfiguration configuration ,ILogger<PingService> logger)
    {
        _vp = vp;
        _configuration = configuration;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tableClient = new TableClient(_configuration.GetConnectionString("PingTable"), "Ping");
        var currentVersion = _vp.GetVersion();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tableEntity = new TableEntity(currentVersion.Id.ToString(), DateTime.UtcNow.ToString("s"));
                
                await tableClient.AddEntityAsync(tableEntity, stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not write ping to azure storage table");
            }
            
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}