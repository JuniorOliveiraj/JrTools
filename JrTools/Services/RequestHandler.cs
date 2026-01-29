using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class RequestHandler
    {
        private static readonly HttpClient _sharedClient = new HttpClient();
        
        private readonly string _url;
        private readonly string _method;
        private readonly string _loginType;

        public RequestHandler(string url, string method = "GET", string loginType = null)
        {
            _url = url;
            _method = method.ToUpper();
            _loginType = loginType; 
        }

        private async Task<string> GetAsync()
        {
            try
            {
                // In a real scenario, consider using _sharedClient directly without authentication if not needed,
                // or managing headers appropriately.
                // For this refactor, we are just reusing the client.
                
                HttpResponseMessage response = await _sharedClient.GetAsync(_url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (HttpRequestException)
            {
                // Removed Console.WriteLine as per requirements. 
                // In production, use a proper logger.
                return null;
            }
        }

        public async Task<string> SendRequestAsync()
        {
            return _method switch
            {
                "GET" => await GetAsync(),
                _ => null // Removed Console.WriteLine
            };
        }
     
        public static async Task<string> RequestApiJrLogin(string login, string senha)
        {
            // Note: Sending credentials in URL params is unsafe.
            // Keeping original logic structure but warning about security.
            string url = $"https://api.juniorbelem.com/login?email={Uri.EscapeDataString(login)}&password={Uri.EscapeDataString(senha)}";
            RequestHandler requestHandler = new RequestHandler(url, "GET", "basic");
            return await requestHandler.SendRequestAsync();
        }
    }
}
