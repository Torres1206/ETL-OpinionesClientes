using Microsoft.Data.SqlClient;

public static class DimensionLoader
{
    public static void CargarDimensiones(SqlConnection connection)
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("   CARGA DE DIMENSIONES - DATA WAREHOUSE");
        Console.WriteLine("========================================");

        using var transaction = connection.BeginTransaction();

        try
        {
            // DIMENSIÓN CLIENTE
            Ejecutar(connection, transaction, "DimCliente", """
                INSERT INTO DW_OpinionesClientes.dbo.DimCliente
                    (ClienteID, NombreCliente)
                SELECT DISTINCT
                    TRY_CONVERT(INT, c.IdCliente),
                    c.Nombre
                FROM AnalisisOpinionesClientes.dbo.Cliente c
                WHERE TRY_CONVERT(INT, c.IdCliente) IS NOT NULL
                AND NOT EXISTS (
                    SELECT 1
                    FROM DW_OpinionesClientes.dbo.DimCliente d
                    WHERE d.ClienteID =
                        TRY_CONVERT(INT, c.IdCliente)
                );
                """);

            // DIMENSIÓN PRODUCTO
            Ejecutar(connection, transaction, "DimProducto", """
                INSERT INTO DW_OpinionesClientes.dbo.DimProducto
                    (ProductoID, NombreProducto, Categoria)
                SELECT DISTINCT
                    TRY_CONVERT(INT, p.IdProducto),
                    p.Nombre,
                    c.NombreCategoria
                FROM AnalisisOpinionesClientes.dbo.Producto p
                INNER JOIN
                    AnalisisOpinionesClientes.dbo.Categoria c
                    ON p.IdCategoria = c.IdCategoria
                WHERE TRY_CONVERT(INT, p.IdProducto) IS NOT NULL
                AND NOT EXISTS (
                    SELECT 1
                    FROM DW_OpinionesClientes.dbo.DimProducto d
                    WHERE d.ProductoID =
                        TRY_CONVERT(INT, p.IdProducto)
                );
                """);

            // DIMENSIÓN FUENTE
            Ejecutar(connection, transaction, "DimFuente", """
                WITH Fuentes AS (
                    SELECT DISTINCT
                        CASE TipoFuente
                            WHEN 'CSV' THEN 'Encuestas internas'
                            WHEN 'Web' THEN 'Reseñas web'
                            WHEN 'Red Social' THEN 'Redes sociales'
                        END AS NombreFuente,
                        CASE TipoFuente
                            WHEN 'CSV' THEN 'CSV'
                            WHEN 'Web' THEN 'Base de Datos'
                            WHEN 'Red Social' THEN 'API REST'
                        END AS TipoFuente
                    FROM AnalisisOpinionesClientes.dbo.Fuente_Datos
                    WHERE TipoFuente IN ('CSV', 'Web', 'Red Social')
                )
                INSERT INTO DW_OpinionesClientes.dbo.DimFuente
                    (NombreFuente, TipoFuente)
                SELECT f.NombreFuente, f.TipoFuente
                FROM Fuentes f
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM DW_OpinionesClientes.dbo.DimFuente d
                    WHERE d.NombreFuente = f.NombreFuente
                    AND d.TipoFuente = f.TipoFuente
                );
                """);

            // DIMENSIÓN SENTIMIENTO
            Ejecutar(connection, transaction, "DimSentimiento", """
                WITH Sentimientos AS (
                    SELECT DISTINCT
                        CASE NombreClasificacion
                            WHEN 'Positiva' THEN 'Positivo'
                            WHEN 'Negativa' THEN 'Negativo'
                            WHEN 'Neutra' THEN 'Neutro'
                        END AS NombreSentimiento
                    FROM AnalisisOpinionesClientes.dbo.Clasificacion
                )
                INSERT INTO DW_OpinionesClientes.dbo.DimSentimiento
                    (NombreSentimiento)
                SELECT s.NombreSentimiento
                FROM Sentimientos s
                WHERE s.NombreSentimiento IS NOT NULL
                AND NOT EXISTS (
                    SELECT 1
                    FROM DW_OpinionesClientes.dbo.DimSentimiento d
                    WHERE d.NombreSentimiento = s.NombreSentimiento
                );
                """);

            // DIMENSIÓN FECHA
            Ejecutar(connection, transaction, "DimFecha", """
                WITH Fechas AS (
                    SELECT CAST(Fecha AS DATE) AS Fecha
                    FROM AnalisisOpinionesClientes.dbo.Web_Reviews

                    UNION

                    SELECT CAST(Fecha AS DATE)
                    FROM AnalisisOpinionesClientes.dbo.Social_Comments

                    UNION

                    SELECT CAST(Fecha AS DATE)
                    FROM AnalisisOpinionesClientes.dbo.Surveys

                    UNION

                    SELECT FechaResena
                    FROM OpinionesClientesDB.dbo.ResenasClientes
                )
                INSERT INTO DW_OpinionesClientes.dbo.DimFecha
                (
                    FechaKey,
                    Fecha,
                    Dia,
                    Mes,
                    NombreMes,
                    Trimestre,
                    Anio
                )
                SELECT
                    CONVERT(INT, CONVERT(CHAR(8), f.Fecha, 112)),
                    f.Fecha,
                    DAY(f.Fecha),
                    MONTH(f.Fecha),
                    FORMAT(f.Fecha, 'MMMM', 'es-DO'),
                    DATEPART(QUARTER, f.Fecha),
                    YEAR(f.Fecha)
                FROM Fechas f
                WHERE f.Fecha IS NOT NULL
                AND NOT EXISTS (
                    SELECT 1
                    FROM DW_OpinionesClientes.dbo.DimFecha d
                    WHERE d.FechaKey =
                        CONVERT(INT,
                            CONVERT(CHAR(8), f.Fecha, 112))
                );
                """);

            transaction.Commit();

            Console.WriteLine("----------------------------------------");
            Console.WriteLine("Carga de dimensiones completada.");
            Console.WriteLine("========================================");

            MostrarTotales(connection);
        }
        catch
        {
            if (transaction.Connection != null)
                transaction.Rollback();

            Console.WriteLine("Error durante la carga de dimensiones.");
            throw;
        }
    }

    private static void Ejecutar(
        SqlConnection connection,
        SqlTransaction transaction,
        string dimension,
        string sql)
    {
        using var command = new SqlCommand(
            sql, connection, transaction);

        command.CommandTimeout = 120;

        int insertados = command.ExecuteNonQuery();

        Console.WriteLine(
            $"{dimension}: {insertados} registros insertados.");
    }

    private static void MostrarTotales(SqlConnection connection)
    {
        string[] dimensiones =
        {
            "DimCliente",
            "DimProducto",
            "DimFuente",
            "DimSentimiento",
            "DimFecha"
        };

        Console.WriteLine();
        Console.WriteLine("TOTAL DE REGISTROS POR DIMENSIÓN");
        Console.WriteLine("----------------------------------------");

        foreach (string dimension in dimensiones)
        {
            string sql =
                $"SELECT COUNT(*) FROM " +
                $"DW_OpinionesClientes.dbo.{dimension}";

            using var command = new SqlCommand(sql, connection);

            int total = Convert.ToInt32(command.ExecuteScalar());

            Console.WriteLine($"{dimension}: {total} registros");
        }
    }
}