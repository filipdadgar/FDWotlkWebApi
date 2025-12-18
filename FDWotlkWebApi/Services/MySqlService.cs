using MySqlConnector;
using FDWotlkWebApi.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;

namespace FDWotlkWebApi.Services
{
    public class MySqlOptions
    {
        public string Mangos { get; set; } = string.Empty;
    }

    public class MySqlService : IMySqlService
    {
        private readonly string _connectionString;
        private readonly ILogger<MySqlService> _logger;

        public MySqlService(IOptions<MySqlOptions> options, ILogger<MySqlService> logger, IConfiguration configuration)
        {
            _logger = logger;

            // Try options first
            var configured = options?.Value?.Mangos;
            string source = "none";

            if (!string.IsNullOrEmpty(configured))
            {
                source = "IOptions";
            }
            else
            {
                // Fallback to IConfiguration connection string or environment variable
                configured = configuration.GetConnectionString("Mangos")
                             ?? configuration["ConnectionStrings:Mangos"]
                             ?? Environment.GetEnvironmentVariable("ConnectionStrings__Mangos");

                if (!string.IsNullOrEmpty(configuration.GetConnectionString("Mangos")) ) source = "Configuration.GetConnectionString";
                else if (!string.IsNullOrEmpty(configuration["ConnectionStrings:Mangos"])) source = "Configuration[ConnectionStrings:Mangos]";
                else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ConnectionStrings__Mangos"))) source = "Environment.ConnectionStrings__Mangos";
            }

            if (string.IsNullOrEmpty(configured))
            {
                _logger.LogError("Connection string 'Mangos' not configured. Sources checked: IOptions, Configuration.GetConnectionString('Mangos'), Configuration['ConnectionStrings:Mangos'], Environment variable ConnectionStrings__Mangos");
                throw new ArgumentException("Connection string 'Mangos' not configured.");
            }

            // Mask connection string when logging: keep protocol/host part if present but hide password
            string masked = MaskConnectionString(configured);
            _logger.LogInformation("Using MySQL connection string from {Source}: {ConnPreview}", source, masked);

            _connectionString = configured;

            // Ensure connector options to handle MySQL 'zero' timestamps or conversion behavior.
            if (!_connectionString.Contains("AllowZeroDateTime", StringComparison.OrdinalIgnoreCase))
                _connectionString += ";AllowZeroDateTime=True";
            if (!_connectionString.Contains("ConvertZeroDateTime", StringComparison.OrdinalIgnoreCase))
                _connectionString += ";ConvertZeroDateTime=True";
        }

        private static string MaskConnectionString(string cs)
        {
            try
            {
                // Very simple masking: replace password=...; or Password=...; occurrences
                var parts = cs.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    var p = parts[i];
                    if (p.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var idx = p.IndexOf('=');
                        if (idx >= 0)
                            parts[i] = p.Substring(0, idx + 1) + "****";
                    }
                }
                return string.Join(';', parts);
            }
            catch
            {
                return "[masked]";
            }
        }

        // Helper to resolve which IP column exists in the account table
        private static async Task<string?> ResolveIpColumnAsync(MySqlConnection conn, CancellationToken cancellationToken)
        {
            var existing = await GetExistingAccountColumnsAsync(conn, cancellationToken);

            if (existing.Contains("lockedIp") && existing.Contains("last_ip"))
                return "COALESCE(lockedIp, last_ip)";
            if (existing.Contains("lockedIp"))
                return "lockedIp";
            if (existing.Contains("last_ip"))
                return "last_ip";
            return null;
        }

        // Return a set of existing columns for the `account` table by performing a schema-only SELECT
        // This avoids relying on information_schema and works even when that access is restricted.
        private static async Task<HashSet<string>> GetExistingAccountColumnsAsync(MySqlConnection conn, CancellationToken cancellationToken)
        {
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Perform a query that returns no rows but exposes column metadata
            const string schemaSql = "SELECT * FROM account LIMIT 0";
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = schemaSql;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var cols = reader.GetColumnSchema();
            foreach (var col in cols)
            {
                if (!string.IsNullOrEmpty(col.ColumnName))
                    existing.Add(col.ColumnName);
            }
            return existing;
        }
        
        // Get list of players
        public async Task<List<Player>> GetPlayersAsync(CancellationToken cancellationToken = default)
        {
            var players = new List<Player>();

            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                // Discover existing account columns
                var existingCols = await GetExistingAccountColumnsAsync(conn, cancellationToken);

                // Base required columns
                var selectParts = new List<string>
                {
                    "id",
                    "username",
                    "gmlevel",
                    "email",
                    "joindate"
                };

                // last_ip: prefer lockedIp or last_ip; if neither present, alias empty string
                if (existingCols.Contains("lockedIp") && existingCols.Contains("last_ip"))
                    selectParts.Add("COALESCE(lockedIp, last_ip) AS last_ip");
                else if (existingCols.Contains("lockedIp"))
                    selectParts.Add("lockedIp AS last_ip");
                else if (existingCols.Contains("last_ip"))
                    selectParts.Add("last_ip AS last_ip");
                else
                    selectParts.Add("'' AS last_ip");

                // failed_logins and locked
                selectParts.Add(existingCols.Contains("failed_logins") ? "failed_logins" : "0 AS failed_logins");
                selectParts.Add(existingCols.Contains("locked") ? "locked" : "0 AS locked");

                // last_login
                selectParts.Add(existingCols.Contains("last_login") ? "last_login" : "NULL AS last_login");

                // active_realm_id and expansion
                selectParts.Add(existingCols.Contains("active_realm_id") ? "active_realm_id" : "0 AS active_realm_id");
                selectParts.Add(existingCols.Contains("expansion") ? "expansion" : "0 AS expansion");

                var proj = string.Join(", ", selectParts);
                var sql = $"SELECT {proj} FROM account LIMIT 100;";

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var p = new Player
                    {
                        Id = reader.IsDBNull("id") ? 0 : reader.GetInt32("id"),
                        Username = reader.IsDBNull("username") ? string.Empty : reader.GetString("username"),
                        GmLevel = reader.GetByteSafe("gmlevel"),
                        Email = reader.IsDBNull("email") ? null : reader.GetString("email"),
                        JoinDate = reader.IsDBNull("joindate") ? DateTime.MinValue : reader.GetDateTime("joindate"),
                        LastIp = reader.IsDBNull("last_ip") ? string.Empty : reader.GetString("last_ip"),
                        FailedLogins = reader.IsDBNull("failed_logins") ? 0 : reader.GetInt32("failed_logins"),
                        Locked = reader.GetByteSafe("locked") != 0,
                        LastLogin = reader.IsDBNullOrdinalSafe("last_login") ? DateTime.MinValue : reader.GetDateTime("last_login"),
                        ActiveRealmId = reader.IsDBNull("active_realm_id") ? 0 : reader.GetInt32("active_realm_id"),
                        Expansion = reader.IsDBNull("expansion") ? 0 : reader.GetInt32("expansion")
                    };

                    players.Add(p);
                }

                _logger.LogInformation("Fetched {Count} players", players.Count);
                 return players;
            }
            catch (MySqlException mex)
            {
                _logger.LogError(mex, "MySql error while fetching players");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while fetching players");
                throw;
            }
        }

        // Update account expansion
        public async Task UpdateAccountExpansionAsync(string username, int expansion, CancellationToken cancellationToken = default)
        {
            const string sql = @"UPDATE account SET expansion = @expansion WHERE username = @username;";

            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@expansion", expansion);
                cmd.Parameters.AddWithValue("@username", username);

                var rowsAffected = await cmd.ExecuteNonQueryAsync(cancellationToken);
                if (rowsAffected > 0)
                {
                    _logger.LogInformation("Updated expansion for user {Username} to {Expansion}", username, expansion);
                }
                else
                {
                    _logger.LogWarning("No rows updated for user {Username}", username);
                }
            }
            catch (MySqlException mex)
            {
                _logger.LogError(mex, "MySql error while updating account expansion for user {Username}", username);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating account expansion for user {Username}", username);
                throw;
            }
        }

        // Count accounts created from a specific IP. Optionally limit to a recent time window.
        public async Task<int> GetAccountCountByIpAsync(string ip, TimeSpan? window = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                var ipCol = await ResolveIpColumnAsync(conn, cancellationToken);
                if (ipCol == null)
                {
                    _logger.LogWarning("No IP column found on account table; returning 0 for account count by IP");
                    return 0;
                }

                var sql = $"SELECT COUNT(*) FROM account WHERE {ipCol} = @ip";
                if (window.HasValue)
                    sql += " AND joindate >= @since";

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@ip", ip);
                if (window.HasValue)
                    cmd.Parameters.AddWithValue("@since", DateTime.UtcNow - window.Value);

                var result = await cmd.ExecuteScalarAsync(cancellationToken);
                if (result == null || result == DBNull.Value) return 0;
                return Convert.ToInt32(result);
            }
            catch (MySqlException mex)
            {
                _logger.LogError(mex, "MySql error while counting accounts for IP {Ip}", ip);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while counting accounts for IP {Ip}", ip);
                throw;
            }
        }

        // Update last_ip for an account
        public async Task UpdateAccountLastIpAsync(string username, string ip, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                var ipCol = await ResolveIpColumnAsync(conn, cancellationToken);
                if (ipCol == null)
                {
                    _logger.LogWarning("No IP column found on account table; cannot update last_ip for user {Username}", username);
                    return;
                }

                var sql = $"UPDATE account SET {ipCol} = @ip WHERE username = @username;";

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@ip", ip);
                cmd.Parameters.AddWithValue("@username", username);

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (MySqlException mex)
            {
                _logger.LogError(mex, "MySql error while updating last_ip for user {Username}", username);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating last_ip for user {Username}", username);
                throw;
            }
        }
    }

    public class DatabaseOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
    }
}

// Local helper extension methods for safe reads
static class MySqlDataReaderExtensions
{
    public static byte GetByteSafe(this MySqlDataReader reader, string name)
    {
        if (reader.IsDBNull(name)) return 0;
        var value = reader.GetFieldValue<byte?>(reader.GetOrdinal(name));
        return value ?? 0;
    }

    public static bool IsDBNull(this MySqlDataReader reader, string name)
    {
        var ord = reader.GetOrdinal(name);
        return reader.IsDBNull(ord);
    }

    // New helper: check by column name but return true if column missing or DBNull
    public static bool IsDBNullOrdinalSafe(this MySqlDataReader reader, string name)
    {
        try
        {
            var ord = reader.GetOrdinal(name);
            return reader.IsDBNull(ord);
        }
        catch (IndexOutOfRangeException)
        {
            // Column does not exist in this resultset
            return true;
        }
    }

    public static int GetInt32(this MySqlDataReader reader, string name)
    {
        var ord = reader.GetOrdinal(name);
        return reader.GetInt32(ord);
    }

    public static string GetString(this MySqlDataReader reader, string name)
    {
        var ord = reader.GetOrdinal(name);
        return reader.GetString(ord);
    }

    public static DateTime GetDateTime(this MySqlDataReader reader, string name)
    {
        var ord = reader.GetOrdinal(name);
        if (reader.IsDBNull(ord)) return DateTime.MinValue;

        try
        {
            return reader.GetDateTime(ord);
        }
        catch (Exception)
        {
            // Fallback: try to read as string and parse. This handles zero-dates or unusual formats.
            try
            {
                var s = reader.GetString(ord);
                if (DateTime.TryParse(s, out var dt))
                    return dt;
            }
            catch { /* ignore and fall through */ }

            return DateTime.MinValue;
        }
    }
}
