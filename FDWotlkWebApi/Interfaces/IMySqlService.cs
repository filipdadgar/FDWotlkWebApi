using FDWotlkWebApi.Models;

namespace FDWotlkWebApi.Services
{
    public interface IMySqlService
    {
        Task<List<Player>> GetPlayersAsync(CancellationToken cancellationToken = default);
        Task UpdateAccountExpansionAsync(string username, int expansion, CancellationToken cancellationToken = default);
    }
}
