using Npgsql;

namespace YukiAssistantDemo.Data;

public sealed class NpgsqlConnectionFactory(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("YukiDemo")
        ?? configuration["ConnectionStrings__YukiDemo"]
        ?? "Host=localhost;Port=5432;Database=yuki_demo;Username=yuki;Password=yuki_local_only";

    public async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
