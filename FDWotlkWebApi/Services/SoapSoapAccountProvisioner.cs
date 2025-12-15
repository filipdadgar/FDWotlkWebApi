using System.ServiceModel;
using Microsoft.Extensions.Options;

namespace FDWotlkWebApi.Services
{
    public class SoapServerOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 7878;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class SoapSoapAccountProvisioner : ISoapAccountProvisioner
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private HttpClient _httpClient;

        public SoapSoapAccountProvisioner(IOptions<SoapServerOptions> options, HttpClient httpClient)
        {
            var soapOptions = options.Value;
            _host = soapOptions.Host;
            _port = soapOptions.Port;
            _username = soapOptions.Username;
            _password = soapOptions.Password;
            _httpClient = httpClient;
        }

        private async Task<string> SendSoapRequestAsync(string action, string command)
        {
            var soapEnvelope = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                               "<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:ns1=\"urn:MaNGOS\">\n" +
                               "  <SOAP-ENV:Body>\n" +
                               $"    <ns1:{action}>\n" +
                               $"      <command>{command}</command>\n" +
                               "    </ns1:{action}>\n" +
                               "  </SOAP-ENV:Body>\n" +
                               "</SOAP-ENV:Envelope>";

            var request = new HttpRequestMessage(HttpMethod.Post, $"http://{_host}:{_port}/")
            {
                Content = new StringContent(soapEnvelope, System.Text.Encoding.UTF8, "text/xml")
            };

            request.Headers.Add("SOAPAction", action);
            var byteArray = System.Text.Encoding.ASCII.GetBytes($"{_username}:{_password}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<(bool Success, string? ErrorMessage, long? ExternalId)> ProvisionAccountAsync(string username, string password, CancellationToken cancellationToken)
        {
            try
            {
                var command = $"account create {username} {password}";
                var result = await SendSoapRequestAsync("executeCommand", command);

                if (result.Contains("Account created", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, null, null);
                }

                // Extract the error message from the SOAP response
                var errorMessage = ExtractSoapErrorMessage(result);
                return (false, errorMessage, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        private string ExtractSoapErrorMessage(string soapResponse)
        {
            try
            {
                var startTag = "<result>";
                var endTag = "</result>";

                var startIndex = soapResponse.IndexOf(startTag, StringComparison.OrdinalIgnoreCase) + startTag.Length;
                var endIndex = soapResponse.IndexOf(endTag, StringComparison.OrdinalIgnoreCase);

                if (startIndex >= 0 && endIndex > startIndex)
                {
                    return soapResponse[startIndex..endIndex].Trim();
                }
            }
            catch
            {
                // Ignore parsing errors and return the full response as a fallback
            }

            return soapResponse;
        }

        public async Task<string> GetServerInfoAsync()
        {
            try
            {
                Console.WriteLine("Fetching server info via SOAP...");

                var result = await SendSoapRequestAsync("executeCommand", "server info");

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to retrieve server info.", ex);
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
