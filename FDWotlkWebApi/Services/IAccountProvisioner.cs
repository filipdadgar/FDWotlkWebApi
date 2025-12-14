namespace FDWotlkWebApi.Services
{
    public interface IAccountProvisioner
    {
        Task<(bool Success, string? ErrorMessage, long? ExternalId)> ProvisionAccountAsync(string username, string password, CancellationToken cancellationToken = default);
    }
}
