using System;
using System.Text.Json;

namespace JrTools.Services
{
    // Executado quando o processo é lançado com --bserver <servidor> <diretorioBinarios>.
    // Carrega as DLLs Benner, consulta o BServer, imprime JSON no stdout e sai.
    // O processo pai (JrTools UI) lê o resultado — as DLLs nunca ficam travadas no processo principal.
    internal static class BServerWorker
    {
        internal static void Executar(string servidor, string diretorioBinarios)
        {
            try
            {
                var svc = new BServerConnectionService(diretorioBinarios);
                var resultado = svc.TestarConexaoAsync(servidor).GetAwaiter().GetResult();

                if (resultado.IsSuccess)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        success = true,
                        systems = resultado.AvailableSystems
                    }));
                }
                else
                {
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = resultado.ErrorMessage
                    }));
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = ex.Message
                }));
                Environment.Exit(1);
            }
        }
    }
}
