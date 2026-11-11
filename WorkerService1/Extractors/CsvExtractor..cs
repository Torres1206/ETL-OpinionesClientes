
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using WorkerService1.Interfaces;

namespace WorkerService1.Extractors
{
    public class CsvExtractor : IExtractor
    {
        private readonly ILogger<CsvExtractor> _logger;

        public CsvExtractor(ILogger<CsvExtractor> logger)
        {
            _logger = logger;
        }

        public async Task ExtractAsync(
            CancellationToken cancellationToken)
        {
            string rutaEntrada = Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "encuestas.csv");

            string rutaSalida = Path.Combine(
                AppContext.BaseDirectory,
                "Staging",
                "encuestas_extraidas.csv");

            if (!File.Exists(rutaEntrada))
            {
                _logger.LogWarning(
                    "No se encontró el archivo CSV: {Ruta}",
                    rutaEntrada);
                return;
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(rutaSalida)!);

            var configuracion = new CsvConfiguration(
                CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            };

            using var reader = new StreamReader(rutaEntrada);
            using var csv = new CsvHelper.CsvReader(
                reader, configuracion);

            using var writer = new StreamWriter(
                rutaSalida, false);

            using var csvWriter = new CsvHelper.CsvWriter(
                writer, configuracion);

            var registros = csv.GetRecords<dynamic>();

            await csvWriter.WriteRecordsAsync(
                registros, cancellationToken);

            _logger.LogInformation(
                "Extracción CSV completada: {Ruta}",
                rutaSalida);
        }
    }
}
