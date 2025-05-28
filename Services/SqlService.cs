using Microsoft.Data.SqlClient;
using System.Data;

public class SqlService
{
    private readonly string _connectionString;

    public SqlService(IConfiguration config)
    {
        //"confio que não será nulo"
        _connectionString = config.GetConnectionString("DefaultConnection")!;
    }

    public async Task<T> QuerySingleAsync<T>(string sql, SqlParameter[] parameters, Func<IDataReader, T> map)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(sql, conn)
        {
            CommandTimeout = 30
        };
        cmd.Parameters.AddRange(parameters);

        using var reader = await cmd.ExecuteReaderAsync();
        return reader.Read() ? map(reader) : default!;
    }


    public async Task ExecuteAsync(string sql, SqlParameter[] parameters)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(sql, conn)
        {
            CommandTimeout = 30
        };
        cmd.Parameters.AddRange(parameters);
        await cmd.ExecuteNonQueryAsync();
    }
}
