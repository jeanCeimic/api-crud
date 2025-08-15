using Npgsql;

public class TestConnection
{
    public static async Task TestDatabaseConnection()
    {
        var connectionString = "Host=aws-0-us-east-2.pooler.supabase.com;Port=6543;Database=postgres;Username=postgres.pkkskwlsvhduwreelhgs;Password=Test123";

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            Console.WriteLine("✅ Conexión exitosa a PostgreSQL");
            
            using var command = new NpgsqlCommand("SELECT version();", connection);
            var result = await command.ExecuteScalarAsync();
            Console.WriteLine($"Versión de PostgreSQL: {result}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error de conexión: {ex.Message}");
        }
    }
}