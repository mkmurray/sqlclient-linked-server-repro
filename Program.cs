using Microsoft.Data.SqlClient;

var server = Required("SQL_SERVER");
var database = Required("SQL_DATABASE");
var linkedServer = Required("SQL_LINKED_SERVER");
var remoteDatabase = Required("SQL_REMOTE_DATABASE");
var remoteSchema = Environment.GetEnvironmentVariable("SQL_REMOTE_SCHEMA") ?? "dbo";
var remoteTable = Required("SQL_REMOTE_TABLE");
var remoteColumn = Environment.GetEnvironmentVariable("SQL_REMOTE_COLUMN") ?? "MasterPersonID";

var connectionString = new SqlConnectionStringBuilder
{
    DataSource = server,
    InitialCatalog = database,
    IntegratedSecurity = true,
    Encrypt = true,
    TrustServerCertificate = true,
    ConnectTimeout = 30,
    ApplicationName = "SqlClientLinkedServerRepro"
}.ConnectionString;

try
{
    await using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    Console.WriteLine($"SqlClient: {typeof(SqlConnection).Assembly.GetName().Version}");
    Console.WriteLine("First-hop identity:");
    await PrintRows(connection, """
        SELECT @@SERVERNAME AS server_name,
               DB_NAME() AS database_name,
               CONNECTIONPROPERTY('auth_scheme') AS auth_scheme,
               SYSTEM_USER AS login_name,
               ORIGINAL_LOGIN() AS original_login;
        """);

    var remoteSql = $"SELECT @@SERVERNAME AS server_name, SYSTEM_USER AS login_name, ORIGINAL_LOGIN() AS original_login, HAS_DBACCESS(N'{EscapeLiteral(remoteDatabase)}') AS has_database_access;";
    var openQuery = $"SELECT * FROM OPENQUERY({QuoteIdentifier(linkedServer)}, '{EscapeLiteral(remoteSql)}');";
    Console.WriteLine("Linked-server execution context:");
    await PrintRows(connection, openQuery);

    var objectQuery = $"SELECT TOP (0) {QuoteIdentifier(remoteColumn)} FROM {QuoteIdentifier(linkedServer)}.{QuoteIdentifier(remoteDatabase)}.{QuoteIdentifier(remoteSchema)}.{QuoteIdentifier(remoteTable)};";
    Console.WriteLine("Four-part-name object probe:");
    await PrintRows(connection, objectQuery);
    Console.WriteLine("All checks completed.");
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
}

static async Task PrintRows(SqlConnection connection, string sql)
{
    await using var command = new SqlCommand(sql, connection);
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var values = Enumerable.Range(0, reader.FieldCount)
            .Select(index => $"{reader.GetName(index)}={reader.GetValue(index)}");
        Console.WriteLine("  " + string.Join(" | ", values));
    }
}

static string Required(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"Set the {name} environment variable.");

static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";

static string EscapeLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
