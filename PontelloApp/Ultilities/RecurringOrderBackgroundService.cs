namespace PontelloApp.Ultilities
{
    public class RecurringOrderBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _services;

        public RecurringOrderBackgroundService(IServiceProvider services)
        {
            _services = services;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _services.CreateScope();
                var job = scope.ServiceProvider.GetRequiredService<RecurringOrderProcessorJob>();
                await job.RunAsync();

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); 
            }
        }
    }
}
