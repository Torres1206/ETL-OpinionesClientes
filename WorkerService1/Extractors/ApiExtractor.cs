
using System.Text.Json;
using WorkerService1.Interfaces;

namespace WorkerService1.Extractors
{
    public class ApiExtractor : IExtractor
    {
        private readonly ILogger<ApiExtractor> _logger;
        private readonly HttpClient _httpClient;

        public ApiExtractor(
            ILogger<ApiExtractor> logger,
            HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task ExtractAsync(
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Iniciando extracción desde API REST...");

            string url =
                "https://jsonplaceholder.typicode.com/comments";

            using var response = await _httpClient.GetAsync(
                url, cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var contenido =
                await response.Content.ReadAsStreamAsync(
                    cancellationToken);

            using var documento =
                await JsonDocument.ParseAsync(
                    contenido,
                    cancellationToken: cancellationToken);

            string rutaSalida = Path.Combine(
                AppContext.BaseDirectory,
                "Staging",
                "comentarios_api.json");

            Directory.CreateDirectory(
                Path.GetDirectoryName(rutaSalida)!);

            await using var archivo = File.Create(rutaSalida);

            await JsonSerializer.SerializeAsync(
                archivo,
                documento.RootElement,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                },
                cancellationToken);

            int total = documento.RootElement.GetArrayLength();

            _logger.LogInformation(
                "Extracción API completada. Registros: {Total}",
                total);
        }
    }
}
