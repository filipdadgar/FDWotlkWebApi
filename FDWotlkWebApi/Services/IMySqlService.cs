using FDWotlkWebApi.Models;

namespace FDWotlkWebApi.Services
{
    public interface IMySqlService
    {
        Task<List<Player>> GetPlayersAsync(CancellationToken cancellationToken = default);
        // Task<(bool Success, string? ErrorMessage, long? InsertedId)> CreatePlayerAsync(CreatePlayerRequest request, CancellationToken cancellationToken = default);
    }
}
