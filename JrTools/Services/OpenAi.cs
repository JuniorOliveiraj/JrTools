using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Net.Http;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class OpenAi
    {
        private readonly ChatClient _chatClient;

        public OpenAi(string apiKey, string model = "gpt-4o")
        {
            _chatClient = new ChatClient(
                model: model,
                credential: new ApiKeyCredential(apiKey)
            );
        }

        public async Task<string> EnviarPromptAsync(string prompt, IProgress<string>? progresso = null)
        {
            string respostaFinal = "";

            try
            {
                await foreach (StreamingChatCompletionUpdate update in _chatClient.CompleteChatStreamingAsync(prompt))
                {
                    if (update.ContentUpdate.Count > 0)
                    {
                        string delta = update.ContentUpdate[0].Text;
                        respostaFinal += delta;
                        progresso?.Report(delta);
                    }
                }
            }
            catch (HttpRequestException httpEx)
            {
                progresso?.Report("\n❌ Erro de conexão com a API da OpenAI.");
                progresso?.Report($"\nDetalhes: {httpEx.Message}");
                return "[Erro de conexão com a API]";
            }
            catch (ClientResultException apiEx) // exceção da lib para status de erro HTTP
            {
                if (apiEx.Status == 401 || apiEx.Status == 403)
                {
                    progresso?.Report("\n🔑 Erro de autenticação: verifique sua API Key.");
                    progresso?.Report("\nSe você está usando uma conta gratuita, pode ser que ela não tenha créditos.");
                    progresso?.Report("\n");
                    progresso?.Report("\n");
                    progresso?.Report("\n");


                    progresso?.Report($"{apiEx.Message}");
                    return "[API Key inválida ou sem permissão]";
                }

                if (apiEx.Status == 429)
                {
                    progresso?.Report("\n⚠️ Limite de uso atingido (insufficient_quota).");
                    progresso?.Report("Verifique seus créditos na OpenAI ou aguarde a renovação do plano free.");
                    return "[Quota insuficiente]";
                }

                progresso?.Report($"\n⚠️ Erro da API ({apiEx.Status}): {apiEx.Message}");

                progresso?.Report($"{apiEx.Message}" );


                return $"[Erro da API: {apiEx.Status}]";
            }
            catch (Exception ex)
            {
                progresso?.Report($"\n💥 Erro inesperado: {ex.Message}");
                return "[Erro interno]";
            }

            return respostaFinal;
        }
    }
}
