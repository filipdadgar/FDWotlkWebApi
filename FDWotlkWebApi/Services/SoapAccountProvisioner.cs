using System.ServiceModel;
using System.ServiceModel.Channels;

namespace FDWotlkWebApi.Services
{
    public class SoapServerOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 7878;
    }
    public class SoapAccountProvisioner : IAccountProvisioner
    {
        private readonly string _host;
        private readonly int _port;

        public SoapAccountProvisioner(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public async Task<(bool Success, string? ErrorMessage, long? ExternalId)> ProvisionAccountAsync(string username, string password, CancellationToken cancellationToken)
        {
            try
            {
                var soapUrl = $"http://{_host}:{_port}/";
                var binding = new BasicHttpBinding();
                var endpoint = new EndpointAddress(soapUrl);
                var factory = new ChannelFactory<ISoapService>(binding, endpoint);

                var client = factory.CreateChannel();

                var command = $"account create {username} {password}";
                var result = await client.ExecuteCommandAsync(command);

                if (result.Contains("success", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, null, null);
                }

                return (false, "SOAP command failed.", null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }
    }

    [ServiceContract]
    public interface ISoapService
    {
        [OperationContract]
        Task<string> ExecuteCommandAsync(string command);
    }
}
