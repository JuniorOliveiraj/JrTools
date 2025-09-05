using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class RequestHandler
    {
        private readonly string _url;
        private readonly string _method;
        private readonly string _loginType;
        private readonly HttpClient _client;

        public RequestHandler(string url, string method = "GET", string loginType = null)
        {
            _url = url;
            _method = method.ToUpper();
            _loginType = loginType; // Pode ser usado para autenticação
            _client = new HttpClient();
        }

        // Método privado para GET
        private async Task<string> GetAsync()
        {
            try
            {
                HttpResponseMessage response = await _client.GetAsync(_url);
                response.EnsureSuccessStatusCode();
                string content = await response.Content.ReadAsStringAsync();
                return content;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Erro na requisição GET: {e.Message}");
                return null;
            }
        }

        // Método público para enviar requisição baseado no método
        public async Task<string> SendRequestAsync()
        {
            switch (_method)
            {
                case "GET":
                    return await GetAsync();
                default:
                    Console.WriteLine($"Método {_method} não implementado.");
                    return null;
            }
        }

     
        public static async Task<string> RequestApiJrLogin(string login, string senha)
        {
            string url = $"https://api.juniorbelem.com/login?email={login}&password={senha}";
            RequestHandler requestHandler = new RequestHandler(url, "GET", "basic");
            string response = await requestHandler.SendRequestAsync();
            return response;
        }
    }
}
