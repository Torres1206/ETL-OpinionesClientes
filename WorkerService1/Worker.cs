
using WorkerService1.Extractors;

namespace WorkerService1
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly CsvExtractor _csvExtractor;
        private readonly DatabaseExtractor _databaseExtractor;
        private readonly ApiExtractor _apiExtractor;

        public Worker(
            ILogger<Worker> logger,
            CsvExtractor csvExtractor,
            DatabaseExtractor databaseExtractor,
            ApiExtractor apiExtractor)
        {
            _logger = logger;
            _csvExtractor = csvExtractor;
            _databaseExtractor = databaseExtractor;
            _apiExtractor = apiExtractor;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation(
                    "Iniciando proceso de extracción ETL...");

                await _csvExtractor.ExtractAsync(stoppingToken);

                await _databaseExtractor.ExtractAsync(stoppingToken);

                await _apiExtractor.ExtractAsync(stoppingToken);

                _logger.LogInformation(
                    "Todas las extracciones finalizaron correctamente.");
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Proceso cancelado.");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, "Error durante el proceso ETL.");
            }
        }
    }
}
