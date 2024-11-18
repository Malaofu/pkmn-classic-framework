namespace PkmnFoundations.GlobalTerminalService;
public class Service1 : BackgroundService
{
    private GTServer4? _server4;
    private GTServer5? _server5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialization logic
        _server4 = new GTServer4();
        _server5 = new GTServer5();

        // Start polling both servers
        _server4.BeginPolling();
        _server5.BeginPolling();

        // Keep the service running until stopped
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken); // Adjust delay as needed
        }
    }

    public override Task StopAsync(CancellationToken stoppingToken)
    {
        // Shutdown logic
        _server4?.EndPolling();
        _server5?.EndPolling();
        return base.StopAsync(stoppingToken);
    }
}
