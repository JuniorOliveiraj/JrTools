using GenerativeAI;
using GenerativeAI.Types;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class GeminiService
    {
        private readonly GenerativeModel _geminiModel;

        public GeminiService(string apiKey, string modelName = "models/gemini-2.0-flash")
        {
            // Inicializa o cliente com a API Key
            var googleAI = new GoogleAi(apiKey);
            _geminiModel = googleAI.CreateGenerativeModel(modelName);
        }

        public async Task<string> EnviarPromptAsync(string prompt, IProgress<string>? progresso = null)
        {
            progresso?.Report("⏳ Processando com Gemini...");
            string respostaFinal = "";

            try
            {
                progresso?.Report("\n entrou no try");
                // Streaming de resposta
                await foreach (var chunk in _geminiModel.StreamContentAsync(prompt))
                {
                    respostaFinal += chunk.Text;
                    progresso?.Report(chunk.Text);
                }
            }
            catch (Exception ex)
            {
                progresso?.Report("erro");

                progresso?.Report($"\n💥 Erro inesperado: {ex.Message}");
                return "[Erro interno]";
            }

            return respostaFinal;
        }



        public async Task<string> EnviarPromptComArquivoAsync( string prompt, List<string>? arquivosLocais = null, List<(string url, string mimeType)>? arquivosRemotos = null)
        {
            try
            {
                var request = new GenerateContentRequest();
                request.AddText(prompt);

                // Adiciona arquivos locais
                if (arquivosLocais != null)
                {
                    foreach (var arquivo in arquivosLocais)
                        request.AddInlineFile(arquivo);
                }

                // Adiciona arquivos remotos
                if (arquivosRemotos != null)
                {
                    foreach (var (url, mimeType) in arquivosRemotos)
                        request.AddRemoteFile(url, mimeType);
                }

                var response = await _geminiModel.GenerateContentAsync(request).ConfigureAwait(false);

                return response?.Text() ?? "[Sem resposta da IA]";
            }
            catch (Exception ex)
            {
                return $"💥 Erro ao processar arquivos: {ex}";
            }
        }




    }
}
