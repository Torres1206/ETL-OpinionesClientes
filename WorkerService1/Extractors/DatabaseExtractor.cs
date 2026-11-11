
using Microsoft.Data.SqlClient;
using System.Text;
using WorkerService1.Interfaces;

namespace WorkerService1.Extractors
{
    public class DatabaseExtractor : IExtractor
    {
        private readonly ILogger<DatabaseExtractor> _logger;
        private readonly IConfiguration _configuration;

        public DatabaseExtractor(
            ILogger<DatabaseExtractor> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task ExtractAsync(
            CancellationToken cancellationToken)
        {
            string conexion = _configuration
                .GetConnectionString("SQLServer")
                ?? throw new InvalidOperationException(
                    "No se configuró la conexión SQLServer.");

            string rutaSalida = Path.Combine(
                AppContext.BaseDirectory,
                "Staging",
                "resenas_extraidas.csv");

            Directory.CreateDirectory(
                Path.GetDirectoryName(rutaSalida)!);

            await using var connection =
                new SqlConnection(conexion);

            await connection.OpenAsync(cancellationToken);

            const string consulta = """
                SELECT IdResena, IdCliente,
                       Calificacion, Comentario, FechaResena
                FROM dbo.ResenasClientes
                ORDER BY IdResena
                """;

            await using var command =
                new SqlCommand(consulta, connection);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            await using var writer = new StreamWriter(
                rutaSalida, false, new UTF8Encoding(true));

            await writer.WriteLineAsync(
                "IdResena,IdCliente,Calificacion,Comentario,FechaResena");

            int total = 0;

            while (await reader.ReadAsync(cancellationToken))
            {
                string comentario = reader.GetString(3)
                    .Replace("\"", "\"\"");

                string linea =
                    $"{reader.GetInt32(0)}," +
                    $"{reader.GetInt32(1)}," +
                    $"{reader.GetInt32(2)}," +
                    $"\"{comentario}\"," +
                    $"{reader.GetDateTime(4):yyyy-MM-dd}";

                await writer.WriteLineAsync(linea);
                total++;
            }

            _logger.LogInformation(
                "Extracción SQL completada. Registros: {Total}",
                total);
        }
    }
}
