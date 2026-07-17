using JrTools.Enums;
using JrTools.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class BServerConnectionService
    {
        private const string DllName = "Benner.Tecnologia.BServer.Clients.dll";
        private const string ClientTypeName = "Benner.Tecnologia.BServer.Clients.BConnectionClient";

        public bool IsDllAvailable { get; private set; }
        public string DllStatusMessage { get; private set; } = string.Empty;

        private readonly string _dllPath;

        public BServerConnectionService(string diretorioBinarios)
        {
            _dllPath = string.IsNullOrWhiteSpace(diretorioBinarios)
                ? string.Empty
                : Path.Combine(diretorioBinarios, "delphi", DllName);
            VerificarDll();
        }

        private void VerificarDll()
        {
            if (string.IsNullOrWhiteSpace(_dllPath))
            {
                IsDllAvailable = false;
                DllStatusMessage = "Diretório de binários não configurado";
                return;
            }

            if (!File.Exists(_dllPath))
            {
                IsDllAvailable = false;
                DllStatusMessage = $"DLL não encontrada em: {_dllPath}";
                return;
            }

            try
            {
                Assembly.LoadFrom(_dllPath);
                IsDllAvailable = true;
                DllStatusMessage = "DLL disponível";
            }
            catch (Exception ex)
            {
                IsDllAvailable = false;
                DllStatusMessage = $"Erro ao carregar DLL: {ex.Message}";
            }
        }

        public Task<ConnectionResult> TestarConexaoAsync(string servidor)
            => Task.Run(() => ExecutarConexao(servidor));

        private ConnectionResult ExecutarConexao(string servidor)
        {
            if (!IsDllAvailable)
                return new ConnectionResult
                {
                    IsSuccess = false,
                    ErrorType = ConnectionErrorType.DllNotFound,
                    ErrorMessage = DllStatusMessage
                };

            if (string.IsNullOrWhiteSpace(servidor))
                return new ConnectionResult
                {
                    IsSuccess = false,
                    ErrorType = ConnectionErrorType.ValidationError,
                    ErrorMessage = "Endereço do servidor não informado"
                };

            // Garante que o encoding 1252 (usado internamente pela DLL) está disponível no .NET 8+
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Diagnóstico TCP antes de invocar a DLL — porta 2000 e 5337
            var diagTcp = DiagnosticarTcp(servidor, 2000, 5337);

            try
            {
                var assembly = Assembly.LoadFrom(_dllPath);
                var type = assembly.GetType(ClientTypeName)
                    ?? throw new InvalidOperationException($"Tipo {ClientTypeName} não encontrado na DLL");

                var connectMethod = type.GetMethod("Connect", new[] { typeof(string) })
                    ?? throw new InvalidOperationException("Método Connect não encontrado na DLL");

                var getSystemNamesMethod = type.GetMethod("GetSystemNames", new[] { typeof(ArrayList) })
                    ?? throw new InvalidOperationException("Método GetSystemNames não encontrado na DLL");

                var client = Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException("Falha ao instanciar BConnectionClient");

                // Lê a porta padrão que a DLL definiu no construtor
                var portaAtual = (int?)type.GetProperty("Port")?.GetValue(client) ?? -1;

                // Desativa farm (evita leitura de farms.xml do diretório do app)
                type.GetProperty("UseFarm")?.SetValue(client, false);

                try
                {
                    var inicio = DateTime.UtcNow;
                    connectMethod.Invoke(client, new object[] { servidor });

                    var sistemas = new ArrayList();
                    getSystemNamesMethod.Invoke(client, new object[] { sistemas });

                    var nomes = new string[sistemas.Count];
                    sistemas.CopyTo(nomes);
                    Array.Sort(nomes, StringComparer.OrdinalIgnoreCase);

                    return new ConnectionResult
                    {
                        IsSuccess = true,
                        AvailableSystems = nomes,
                        ConnectionTime = DateTime.UtcNow - inicio,
                        ErrorMessage = diagTcp   // reutiliza campo para info de diagnóstico no sucesso
                    };
                }
                finally
                {
                    if (client is IDisposable d) d.Dispose();
                }
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                return new ConnectionResult
                {
                    IsSuccess = false,
                    ErrorType = ConnectionErrorType.ServerUnreachable,
                    ErrorMessage = $"{MontarMensagemErro(tie.InnerException)}\n{diagTcp}"
                };
            }
            catch (FileNotFoundException fnf)
            {
                return new ConnectionResult
                {
                    IsSuccess = false,
                    ErrorType = ConnectionErrorType.DllDependencyMissing,
                    ErrorMessage = $"Dependência não encontrada: {fnf.FileName}\n{diagTcp}"
                };
            }
            catch (Exception ex)
            {
                return new ConnectionResult
                {
                    IsSuccess = false,
                    ErrorType = ConnectionErrorType.InvalidResponse,
                    ErrorMessage = $"{MontarMensagemErro(ex)}\n{diagTcp}"
                };
            }
        }

        private static string DiagnosticarTcp(string host, params int[] portas)
        {
            var resultados = new List<string>();
            foreach (var porta in portas)
            {
                try
                {
                    using var tcp = new TcpClient();
                    tcp.Connect(host, porta);
                    resultados.Add($"TCP {porta}: OK");
                }
                catch (Exception ex)
                {
                    resultados.Add($"TCP {porta}: FALHOU ({ex.Message})");
                }
            }
            return string.Join(" | ", resultados);
        }

        private static string MontarMensagemErro(Exception ex)
        {
            var partes = new List<string>();
            var atual = ex;
            while (atual != null)
            {
                if (!string.IsNullOrWhiteSpace(atual.Message))
                    partes.Add(atual.Message.Trim());
                atual = atual.InnerException;
            }
            return string.Join(" → ", partes);
        }
    }
}
