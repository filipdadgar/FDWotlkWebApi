using MySqlConnector;
using FDWotlkWebApi.Models;
using Microsoft.Extensions.Options;

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

        public MySqlService(IOptions<MySqlOptions> options, ILogger<MySqlService> logger)
        {
            if (options.Value == null || string.IsNullOrEmpty(options.Value.Mangos))
            {
                throw new ArgumentException("Connection string 'Mangos' not configured.");
            }

            _connectionString = options.Value.Mangos;

            // Ensure connector options to handle MySQL 'zero' timestamps or conversion behavior.
            if (!_connectionString.Contains("AllowZeroDateTime", StringComparison.OrdinalIgnoreCase))
                _connectionString += ";AllowZeroDateTime=True";
            if (!_connectionString.Contains("ConvertZeroDateTime", StringComparison.OrdinalIgnoreCase))
                _connectionString += ";ConvertZeroDateTime=True";

            _logger = logger;
        }
        
        // Get list of players
        public async Task<List<Player>> GetPlayersAsync(CancellationToken cancellationToken = default)
        {
            var players = new List<Player>();

            const string sql = @"SELECT id, username, gmlevel, email, joindate, last_ip, failed_logins, locked, last_login, active_realm_id, expansion FROM account LIMIT 100;";

            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var p = new Player
                    {
                        Id = reader.GetInt32("id"),
                        Username = reader.GetString("username"),
                        GmLevel = reader.GetByteSafe("gmlevel"),
                        Email = reader.IsDBNull("email") ? null : reader.GetString("email"),
                        JoinDate = reader.IsDBNull("joindate") ? DateTime.MinValue : reader.GetDateTime("joindate"),
                        LastIp = reader.IsDBNull("last_ip") ? string.Empty : reader.GetString("last_ip"),
                        FailedLogins = reader.IsDBNull("failed_logins") ? 0 : reader.GetInt32("failed_logins"),
                        Locked = reader.IsDBNull("locked") ? false : (reader.GetInt32("locked") != 0),
                        // last_login is a TIMESTAMP in the DB — map to DateTime
                        LastLogin = reader.IsDBNull("last_login") ? DateTime.MinValue : reader.GetDateTime("last_login"),
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
            var sql = "SELECT COUNT(*) FROM account WHERE last_ip = @ip";
            if (window.HasValue)
                sql += " AND joindate >= @since";

            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

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
            const string sql = @"UPDATE account SET last_ip = @ip WHERE username = @username;";

            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

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
