using Microsoft.Data.SqlClient;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

// CONFIGURACIÓN GENERAL

string connectionString =
    "Server=localhost;Database=AnalisisOpinionesClientes;Trusted_Connection=True;TrustServerCertificate=True;";

string carpetaData = Path.Combine(
    AppContext.BaseDirectory, "Data");

Console.WriteLine("=================================================");
Console.WriteLine("     ETL - ANALISIS DE OPINIONES DE CLIENTES");
Console.WriteLine("=================================================");

try
{
    using SqlConnection connection =
        new SqlConnection(connectionString);

    connection.Open();

    Console.WriteLine("Conexion con SQL Server: OK");
    Console.WriteLine();

    // EJECUCION DEL ETL

    ResultadoCarga clientes =
        CargarClientes(connection, carpetaData);

    ResultadoCarga categorias =
        CargarCategorias(connection, carpetaData);

    ResultadoCarga productos =
        CargarProductos(connection, carpetaData);

    ResultadoCarga fuentes =
        CargarFuentes(connection, carpetaData);

    ResultadoCarga reviews =
        CargarWebReviews(connection, carpetaData);

    ResultadoCarga sociales =
        CargarComentariosSociales(connection, carpetaData);

    ResultadoCarga encuestas =
        CargarEncuestas(connection, carpetaData);

    // RESUMEN GENERAL

    Console.WriteLine();
    Console.WriteLine("=================================================");
    Console.WriteLine("              RESUMEN GENERAL ETL");
    Console.WriteLine("=================================================");

    MostrarResultado("CLIENTES", clientes);
    MostrarResultado("CATEGORIAS", categorias);
    MostrarResultado("PRODUCTOS", productos);
    MostrarResultado("FUENTES", fuentes);
    MostrarResultado("RESENAS WEB", reviews);
    MostrarResultado("COMENTARIOS SOCIALES", sociales);
    MostrarResultado("ENCUESTAS", encuestas);

    ResultadoCarga[] resultados =
    {
        clientes,
        categorias,
        productos,
        fuentes,
        reviews,
        sociales,
        encuestas
    };

    Console.WriteLine("----------------------------------------");

    Console.WriteLine(
        $"TOTAL PROCESADOS: {resultados.Sum(r => r.Procesados)}");

    Console.WriteLine(
        $"TOTAL INSERTADOS: {resultados.Sum(r => r.Insertados)}");

    Console.WriteLine(
        $"TOTAL DUPLICADOS: {resultados.Sum(r => r.Duplicados)}");

    Console.WriteLine(
        $"TOTAL RECHAZADOS: {resultados.Sum(r => r.Rechazados)}");

    Console.WriteLine("=================================================");
    Console.WriteLine("Carga de datos de origen finalizada.");

    // NUEVA ETAPA: CARGA DE DIMENSIONES

    DimensionLoader.CargarDimensiones(connection);

    Console.WriteLine();
    Console.WriteLine("=================================================");
    Console.WriteLine("        PROCESO ETL FINALIZADO CORRECTAMENTE");
    Console.WriteLine("=================================================");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("ERROR GENERAL DEL ETL:");
    Console.WriteLine(ex.Message);

    Environment.ExitCode = 1;
}

// ============================================================
// CLIENTES
// ============================================================

static ResultadoCarga CargarClientes(
    SqlConnection connection,
    string carpetaData)
{
    Console.WriteLine("Procesando clients.csv...");

    return ProcesarCsv(
        carpetaData,
        "clients.csv",
        (csv, resultado) =>
        {
            string idCliente = Campo(csv, "IdCliente");
            string nombre = Campo(csv, "Nombre");
            string email = Campo(csv, "Email");

            if (Vacio(idCliente, nombre))
            {
                resultado.Rechazados++;
                return;
            }

            if (ExisteRegistro(
                connection, "Cliente", "IdCliente", idCliente))
            {
                resultado.Duplicados++;
                return;
            }

            Ejecutar(
                connection,
                @"INSERT INTO Cliente
                  (IdCliente, Nombre, Email)
                  VALUES
                  (@IdCliente, @Nombre, @Email)",
                ("@IdCliente", idCliente),
                ("@Nombre", nombre),
                ("@Email", Vacio(email) ? DBNull.Value : email)
            );

            resultado.Insertados++;
        });
}

// ============================================================
// CATEGORIAS
// ============================================================

static ResultadoCarga CargarCategorias(
    SqlConnection connection,
    string carpetaData)
{
    Console.WriteLine("Procesando categorias...");

    ResultadoCarga resultado = new();

    string archivo = Path.Combine(
        carpetaData, "products.csv");

    if (!File.Exists(archivo))
    {
        Console.WriteLine("No se encontro products.csv");
        return resultado;
    }

    HashSet<string> categorias =
        new(StringComparer.OrdinalIgnoreCase);

    using (var reader = new StreamReader(archivo))
    using (var csv = CrearCsvReader(reader))
    {
        if (!csv.Read())
            return resultado;

        csv.ReadHeader();

        while (csv.Read())
        {
            string categoria = Campo(csv, "Categoria");

            if (!Vacio(categoria))
                categorias.Add(categoria);
        }
    }

    foreach (string categoria in categorias)
    {
        resultado.Procesados++;

        try
        {
            object? existe = Consultar(
                connection,
                @"SELECT IdCategoria
                  FROM Categoria
                  WHERE NombreCategoria = @Nombre",
                ("@Nombre", categoria)
            );

            if (existe != null && existe != DBNull.Value)
            {
                resultado.Duplicados++;
                continue;
            }

            int nuevoId = Convert.ToInt32(
                Consultar(
                    connection,
                    @"SELECT ISNULL(MAX(IdCategoria), 0) + 1
                      FROM Categoria")
            );

            Ejecutar(
                connection,
                @"INSERT INTO Categoria
                  (IdCategoria, NombreCategoria)
                  VALUES
                  (@IdCategoria, @NombreCategoria)",
                ("@IdCategoria", nuevoId),
                ("@NombreCategoria", categoria)
            );

            resultado.Insertados++;
        }
        catch (Exception ex)
        {
            resultado.Rechazados++;

            Console.WriteLine(
                $"Categoria rechazada: {ex.Message}");
        }
    }

    return resultado;
}

// ============================================================
// PRODUCTOS
// ============================================================

static ResultadoCarga CargarProductos(
    SqlConnection connection,
    string carpetaData)
{
    Console.WriteLine("Procesando products.csv...");

    return ProcesarCsv(
        carpetaData,
        "products.csv",
        (csv, resultado) =>
        {
            string idProducto = Campo(csv, "IdProducto");
            string nombre = Campo(csv, "Nombre");
            string categoria = Campo(csv, "Categoria");

            if (Vacio(idProducto, nombre, categoria))
            {
                resultado.Rechazados++;
                return;
            }

            if (ExisteRegistro(
                connection, "Producto",
                "IdProducto", idProducto))
            {
                resultado.Duplicados++;
                return;
            }

            object? idCategoria = Consultar(
                connection,
                @"SELECT IdCategoria
                  FROM Categoria
                  WHERE NombreCategoria = @Nombre",
                ("@Nombre", categoria)
            );

            if (idCategoria == null ||
                idCategoria == DBNull.Value)
            {
                resultado.Rechazados++;
                return;
            }

            Ejecutar(
                connection,
                @"INSERT INTO Producto
                  (IdProducto, Nombre, IdCategoria)
                  VALUES
                  (@IdProducto, @Nombre, @IdCategoria)",
                ("@IdProducto", idProducto),
                ("@Nombre", nombre),
                ("@IdCategoria", Convert.ToInt32(idCategoria))
            );

            resultado.Insertados++;
        });
}

// ============================================================
// FUENTES DE DATOS
// ============================================================

static ResultadoCarga CargarFuentes(
    SqlConnection connection,
    string carpetaData)
{
    Console.WriteLine("Procesando fuente_datos.csv...");

    return ProcesarCsv(
        carpetaData,
        "fuente_datos.csv",
        (csv, resultado) =>
        {
            string idFuente = Campo(csv, "IdFuente");
            string tipoFuente = Campo(csv, "TipoFuente");
            string fechaTexto = Campo(csv, "FechaCarga");

            if (Vacio(idFuente, tipoFuente))
            {
                resultado.Rechazados++;
                return;
            }

            if (ExisteRegistro(
                connection, "Fuente_Datos",
                "IdFuente", idFuente))
            {
                resultado.Duplicados++;
                return;
            }

            DateTime? fecha = null;

            if (!Vacio(fechaTexto))
            {
                if (!DateTime.TryParse(
                    fechaTexto, out DateTime fechaConvertida))
                {
                    resultado.Rechazados++;
                    return;
                }

                fecha = fechaConvertida;
            }

            Ejecutar(
                connection,
                @"INSERT INTO Fuente_Datos
                  (IdFuente, TipoFuente, FechaCarga)
                  VALUES
                  (@IdFuente, @TipoFuente, @FechaCarga)",
                ("@IdFuente", idFuente),
                ("@TipoFuente", tipoFuente),
                ("@FechaCarga",
                    fecha.HasValue
                    ? fecha.Value
                    : DBNull.Value)
            );

            resultado.Insertados++;
        });
}

// ============================================================
// RESENAS WEB
// ============================================================

static ResultadoCarga CargarWebReviews(
    SqlConnection connection,
    string carpetaData)
{
    Console.WriteLine("Procesando web_reviews.csv...");

    return ProcesarCsv(
        carpetaData,
        "web_reviews.csv",
        (csv, resultado) =>
        {
            string idReview = Campo(csv, "IdReview");

            string idCliente =
                LimpiarId(Campo(csv, "IdCliente"), 'C');

            string idProducto =
                LimpiarId(Campo(csv, "IdProducto"), 'P');

            string fechaTexto = Campo(csv, "Fecha");
            string comentario = Campo(csv, "Comentario");
            string ratingTexto = Campo(csv, "Rating");

            if (Vacio(
                idReview,
                idCliente,
                idProducto,
                fechaTexto,
                comentario))
            {
                resultado.Rechazados++;
                return;
            }

            if (!DateTime.TryParse(
                fechaTexto, out DateTime fecha))
            {
                resultado.Rechazados++;
                return;
            }

            if (!int.TryParse(ratingTexto, out int rating)
                || rating < 1
                || rating > 5)
            {
                resultado.Rechazados++;
                return;
            }

            if (!ExisteRegistro(
                connection, "Cliente",
                "IdCliente", idCliente))
            {
                resultado.Rechazados++;
                return;
            }

            if (!ExisteRegistro(
                connection, "Producto",
                "IdProducto", idProducto))
            {
                resultado.Rechazados++;
                return;
            }

            if (ExisteRegistro(
                connection, "Web_Reviews",
                "IdReview", idReview))
            {
                resultado.Duplicados++;
                return;
            }

            Ejecutar(
                connection,
                @"INSERT INTO Web_Reviews
                  (
                      IdReview,
                      IdCliente,
                      IdProducto,
                      Fecha,
                      Comentario,
                      Rating
                  )
                  VALUES
                  (
                      @IdReview,
                      @IdCliente,
                      @IdProducto,
                      @Fecha,
                      @Comentario,
                      @Rating
                  )",
                ("@IdReview", idReview),
                ("@IdCliente", idCliente),
                ("@IdProducto", idProducto),
                ("@Fecha", fecha),
                ("@Comentario", comentario),
                ("@Rating", rating)
            );

            resultado.Insertados++;
        });
}

// ============================================================
// COMENTARIOS SOCIALES
// ============================================================

static ResultadoCarga CargarComentariosSociales(
    SqlConnection connection,
    string carpetaData)
{
    Console.WriteLine("Procesando social_comments.csv...");

    object? fuenteResultado = Consultar(
        connection,
        @"SELECT TOP 1 IdFuente
          FROM Fuente_Datos
          WHERE TipoFuente = 'Red Social'
          ORDER BY IdFuente"
    );

    if (fuenteResultado == null ||
        fuenteResultado == DBNull.Value)
    {
        Console.WriteLine(
            "No existe una fuente de tipo Red Social.");

        return new ResultadoCarga();
    }

    string idFuente = fuenteResultado.ToString()!;

    return ProcesarCsv(
        carpetaData,
        "social_comments.csv",
        (csv, resultado) =>
        {
            string idComment = Campo(csv, "IdComment");
            string clienteCsv = Campo(csv, "IdCliente");

            string? idCliente =
                Vacio(clienteCsv)
                ? null
                : LimpiarId(clienteCsv, 'C');

            string idProducto =
                LimpiarId(Campo(csv, "IdProducto"), 'P');

            string fuente = Campo(csv, "Fuente");
            string fechaTexto = Campo(csv, "Fecha");
            string comentario = Campo(csv, "Comentario");

            if (Vacio(
                idComment,
                idProducto,
                fuente,
                fechaTexto,
                comentario))
            {
                resultado.Rechazados++;
                return;
            }

            if (!DateTime.TryParse(
                fechaTexto, out DateTime fecha))
            {
                resultado.Rechazados++;
                return;
            }

            if (idCliente != null &&
                !ExisteRegistro(
                    connection,
                    "Cliente",
                    "IdCliente",
                    idCliente))
            {
                resultado.Rechazados++;
                return;
            }

            if (!ExisteRegistro(
                connection,
                "Producto",
                "IdProducto",
                idProducto))
            {
                resultado.Rechazados++;
                return;
            }

            if (ExisteRegistro(
                connection,
                "Social_Comments",
                "IdComment",
                idComment))
            {
                resultado.Duplicados++;
                return;
            }

            Ejecutar(
                connection,
                @"INSERT INTO Social_Comments
                  (
                      IdComment,
                      IdCliente,
                      IdProducto,
                      IdFuente,
                      Fuente,
                      Fecha,
                      Comentario
                  )
                  VALUES
                  (
                      @IdComment,
                      @IdCliente,
                      @IdProducto,
                      @IdFuente,
                      @Fuente,
                      @Fecha,
                      @Comentario
                  )",
                ("@IdComment", idComment),
                ("@IdCliente",
                    idCliente == null
                    ? DBNull.Value
                    : idCliente),
                ("@IdProducto", idProducto),
                ("@IdFuente", idFuente),
                ("@Fuente", fuente),
                ("@Fecha", fecha),
                ("@Comentario", comentario)
            );

            resultado.Insertados++;
        });
}

// ============================================================
// ENCUESTAS
// ============================================================

static ResultadoCarga CargarEncuestas(
    SqlConnection connection,
    string carpetaData)
{
    Console.WriteLine("Procesando surveys_part1.csv...");

    object? fuenteResultado = Consultar(
        connection,
        @"SELECT TOP 1 IdFuente
          FROM Fuente_Datos
          WHERE TipoFuente = 'CSV'
          ORDER BY IdFuente"
    );

    if (fuenteResultado == null ||
        fuenteResultado == DBNull.Value)
    {
        Console.WriteLine(
            "No existe una fuente de tipo CSV.");

        return new ResultadoCarga();
    }

    string idFuente = fuenteResultado.ToString()!;

    return ProcesarCsv(
        carpetaData,
        "surveys_part1.csv",
        (csv, resultado) =>
        {
            string idOpinion = Campo(csv, "IdOpinion");
            string idCliente = Campo(csv, "IdCliente");
            string idProducto = Campo(csv, "IdProducto");
            string fechaTexto = Campo(csv, "Fecha");
            string comentario = Campo(csv, "Comentario");

            string clasificacion =
                Campo(csv, "Clasificación");

            string puntajeTexto =
                Campo(csv, "PuntajeSatisfacción");

            if (Vacio(
                idOpinion,
                idCliente,
                idProducto,
                fechaTexto,
                comentario,
                clasificacion,
                puntajeTexto))
            {
                resultado.Rechazados++;
                return;
            }

            if (!DateTime.TryParse(
                fechaTexto, out DateTime fecha))
            {
                resultado.Rechazados++;
                return;
            }

            if (!int.TryParse(
                puntajeTexto, out int puntaje)
                || puntaje < 1
                || puntaje > 5)
            {
                resultado.Rechazados++;
                return;
            }

            if (!ExisteRegistro(
                connection,
                "Cliente",
                "IdCliente",
                idCliente))
            {
                resultado.Rechazados++;
                return;
            }

            if (!ExisteRegistro(
                connection,
                "Producto",
                "IdProducto",
                idProducto))
            {
                resultado.Rechazados++;
                return;
            }

            object? idClasificacion = Consultar(
                connection,
                @"SELECT IdClasificacion
                  FROM Clasificacion
                  WHERE NombreClasificacion = @Nombre",
                ("@Nombre", clasificacion)
            );

            if (idClasificacion == null ||
                idClasificacion == DBNull.Value)
            {
                resultado.Rechazados++;
                return;
            }

            if (ExisteRegistro(
                connection,
                "Surveys",
                "IdOpinion",
                idOpinion))
            {
                resultado.Duplicados++;
                return;
            }

            Ejecutar(
                connection,
                @"INSERT INTO Surveys
                  (
                      IdOpinion,
                      IdCliente,
                      IdProducto,
                      IdClasificacion,
                      IdFuente,
                      Fecha,
                      Comentario,
                      PuntajeSatisfaccion
                  )
                  VALUES
                  (
                      @IdOpinion,
                      @IdCliente,
                      @IdProducto,
                      @IdClasificacion,
                      @IdFuente,
                      @Fecha,
                      @Comentario,
                      @Puntaje
                  )",
                ("@IdOpinion", idOpinion),
                ("@IdCliente", idCliente),
                ("@IdProducto", idProducto),
                ("@IdClasificacion",
                    Convert.ToInt32(idClasificacion)),
                ("@IdFuente", idFuente),
                ("@Fecha", fecha),
                ("@Comentario", comentario),
                ("@Puntaje", puntaje)
            );

            resultado.Insertados++;
        });
}

// ============================================================
// FUNCIONES AUXILIARES
// ============================================================

static ResultadoCarga ProcesarCsv(
    string carpetaData,
    string nombreArchivo,
    Action<CsvReader, ResultadoCarga> procesar)
{
    ResultadoCarga resultado = new();

    string archivo =
        Path.Combine(carpetaData, nombreArchivo);

    if (!File.Exists(archivo))
    {
        Console.WriteLine(
            $"No se encontro {nombreArchivo}");

        return resultado;
    }

    using var reader = new StreamReader(archivo);

    using var csv = CrearCsvReader(reader);

    if (!csv.Read())
        return resultado;

    csv.ReadHeader();

    while (csv.Read())
    {
        resultado.Procesados++;

        try
        {
            procesar(csv, resultado);
        }
        catch (Exception ex)
        {
            resultado.Rechazados++;

            Console.WriteLine(
                $"{nombreArchivo} - Registro " +
                $"{resultado.Procesados} rechazado: " +
                ex.Message);
        }
    }

    return resultado;
}

static CsvReader CrearCsvReader(
    StreamReader reader)
{
    CsvConfiguration configuracion =
        new(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            TrimOptions = TrimOptions.Trim
        };

    return new CsvReader(
        reader, configuracion);
}

static string Campo(
    CsvReader csv,
    string nombre)
{
    return csv.GetField(nombre)?.Trim() ?? "";
}

static bool Vacio(params string?[] valores)
{
    return valores.Any(
        string.IsNullOrWhiteSpace);
}

static object? Consultar(
    SqlConnection connection,
    string sql,
    params (string Nombre, object Valor)[] parametros)
{
    using SqlCommand command =
        new(sql, connection);

    command.CommandTimeout = 120;

    foreach (var parametro in parametros)
    {
        command.Parameters.AddWithValue(
            parametro.Nombre,
            parametro.Valor ?? DBNull.Value);
    }

    return command.ExecuteScalar();
}

static int Ejecutar(
    SqlConnection connection,
    string sql,
    params (string Nombre, object Valor)[] parametros)
{
    using SqlCommand command =
        new(sql, connection);

    command.CommandTimeout = 120;

    foreach (var parametro in parametros)
    {
        command.Parameters.AddWithValue(
            parametro.Nombre,
            parametro.Valor ?? DBNull.Value);
    }

    return command.ExecuteNonQuery();
}

static bool ExisteRegistro(
    SqlConnection connection,
    string tabla,
    string columna,
    string valor)
{
    string sql =
        $"SELECT COUNT(*) FROM {tabla} " +
        $"WHERE {columna} = @Valor";

    int cantidad = Convert.ToInt32(
        Consultar(
            connection,
            sql,
            ("@Valor", valor))
    );

    return cantidad > 0;
}

static string LimpiarId(
    string valor,
    char prefijo)
{
    valor = valor.Trim();

    if (string.IsNullOrWhiteSpace(valor))
        return "";

    if (valor.StartsWith(
        prefijo.ToString(),
        StringComparison.OrdinalIgnoreCase))
    {
        valor = valor.Substring(1);
    }

    if (string.IsNullOrWhiteSpace(valor))
        return "";

    valor = valor.TrimStart('0');

    if (string.IsNullOrWhiteSpace(valor))
        valor = "0";

    return valor;
}

static void MostrarResultado(
    string nombre,
    ResultadoCarga resultado)
{
    Console.WriteLine();
    Console.WriteLine($"[{nombre}]");

    Console.WriteLine(
        $"Procesados : {resultado.Procesados}");

    Console.WriteLine(
        $"Insertados : {resultado.Insertados}");

    Console.WriteLine(
        $"Duplicados : {resultado.Duplicados}");

    Console.WriteLine(
        $"Rechazados : {resultado.Rechazados}");
}

// ============================================================
// CLASE PARA GUARDAR RESULTADOS
// ============================================================

class ResultadoCarga
{
    public int Procesados { get; set; }

    public int Insertados { get; set; }

    public int Duplicados { get; set; }

    public int Rechazados { get; set; }
}