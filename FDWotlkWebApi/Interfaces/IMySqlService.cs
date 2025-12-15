using FDWotlkWebApi.Models;

namespace FDWotlkWebApi.Services
{
    public interface IMySqlService
    {
        Task<List<Player>> GetPlayersAsync(CancellationToken cancellationToken = default);
        Task UpdateAccountExpansionAsync(string username, int expansion, CancellationToken cancellationToken = default);
        Task<int> GetAccountCountByIpAsync(string ip, TimeSpan? window = null, CancellationToken cancellationToken = default);
        Task UpdateAccountLastIpAsync(string username, string ip, CancellationToken cancellationToken = default);
    }
}
