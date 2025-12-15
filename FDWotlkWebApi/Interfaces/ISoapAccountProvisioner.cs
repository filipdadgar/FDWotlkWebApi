namespace FDWotlkWebApi.Services
{
    public interface ISoapAccountProvisioner
    {
        Task<(bool Success, string? ErrorMessage, long? ExternalId)> ProvisionAccountAsync(string username, string password, CancellationToken cancellationToken = default);
        Task<string> GetServerInfoAsync();
    }
}
