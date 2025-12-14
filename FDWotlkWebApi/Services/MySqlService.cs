using MySqlConnector;
using FDWotlkWebApi.Models;
using System.Security.Cryptography;
using System.Text;

namespace FDWotlkWebApi.Services
{
    public class MySqlService : IMySqlService
    {
        private readonly string _connectionString;
        private readonly ILogger<MySqlService> _logger;
        private readonly IAccountProvisioner _accountProvisioner;

        public MySqlService(IConfiguration configuration, ILogger<MySqlService> logger, IAccountProvisioner accountProvisioner)
        {
            var cs = configuration.GetConnectionString("Mangos");
            if (string.IsNullOrWhiteSpace(cs))
                throw new ArgumentException("Connection string 'Mangos' not configured.");

            // Ensure connector options to handle MySQL 'zero' timestamps or conversion behavior.
            if (!cs.Contains("AllowZeroDateTime", StringComparison.OrdinalIgnoreCase))
                cs += ";AllowZeroDateTime=True";
            if (!cs.Contains("ConvertZeroDateTime", StringComparison.OrdinalIgnoreCase))
                cs += ";ConvertZeroDateTime=True";

            _connectionString = cs;
            _logger = logger;
            _accountProvisioner = accountProvisioner;
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

        // Create an account with username and password only using SOAP
        public async Task<(bool Success, string? ErrorMessage, long? InsertedId)> CreatePlayerAsync(CreatePlayerRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var (success, errorMessage, externalId) = await _accountProvisioner.ProvisionAccountAsync(request.Username, request.Password, cancellationToken);
                if (!success)
                {
                    _logger.LogError("Failed to provision account via SOAP: {ErrorMessage}", errorMessage);
                    return (false, errorMessage, null);
                }

                _logger.LogInformation("Account provisioned successfully via SOAP for username={Username}", request.Username);
                return (true, null, externalId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while provisioning account via SOAP for username={Username}", request.Username);
                return (false, ex.Message, null);
            }
        }
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
