using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace JrTools.Services
{
    public class WebAppLinkService
    {
        private const string PROD_DEFAULT_PATH = @"D:\Benner\fontes\rh\prod";

        // Evita refazer o full scan/relink de WES\WebApp a cada clique de botão dentro da
        // mesma sessão do app — o vínculo já é idempotente, então uma vez por sessão basta.
        private static readonly HashSet<string> _verificadosNestaSessao = new(StringComparer.OrdinalIgnoreCase);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        [DllImport("kernel32.dll", EntryPoint = "CreateSymbolicLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CreateSymbolicLink([In] string lpSymlinkFileName, [In] string lpTargetFileName, uint dwFlags);

        private const uint SYMBOLIC_LINK_FLAG_DIRECTORY = 0x1;
        private const uint SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE = 0x2;

        /// <summary>
        /// Garante que a pasta WES\WebApp do projeto de destino (específico)
        /// possua os links/arquivos essenciais da pasta WES\WebApp do produto (prod),
        /// utilizando NTFS Hard Links e Directory Junctions de forma ultra-rápida (praticamente instantânea).
        /// </summary>
        public async Task GarantirLinkWebAppProdAsync(string caminhoProjetoTarget, string? caminhoProd = null, IProgress<string>? progresso = null, bool forcar = false)
        {
            await Task.Run(() =>
            {
                string prodRoot = string.IsNullOrWhiteSpace(caminhoProd) ? PROD_DEFAULT_PATH : caminhoProd;
                string prodWebApp = Path.Combine(prodRoot, @"WES\WebApp");
                string targetWebApp = Path.Combine(caminhoProjetoTarget, @"WES\WebApp");
                string targetWebAppNormalizado = Path.GetFullPath(targetWebApp).TrimEnd('\\');

                if (!forcar && _verificadosNestaSessao.Contains(targetWebAppNormalizado))
                {
                    progresso?.Report("[LINK WEBAPP] Já verificado nesta sessão, pulando.");
                    return;
                }

                if (!Directory.Exists(prodRoot))
                {
                    progresso?.Report($"[AVISO WEBAPP] Diretório de produção não encontrado: {prodRoot}");
                    return;
                }

                if (string.Equals(Path.GetFullPath(prodWebApp), Path.GetFullPath(targetWebApp), StringComparison.OrdinalIgnoreCase))
                {
                    progresso?.Report("[LINK WEBAPP] O projeto selecionado já é o produto (prod). Nenhum vínculo necessário.");
                    return;
                }

                if (!Directory.Exists(prodWebApp))
                {
                    progresso?.Report($"[AVISO WEBAPP] Pasta de origem WES\\WebApp do prod não encontrada em: {prodWebApp}");
                    return;
                }

                if (!Directory.Exists(targetWebApp))
                {
                    Directory.CreateDirectory(targetWebApp);
                    progresso?.Report($"[LINK WEBAPP] Criada pasta de destino: {targetWebApp}");
                }

                progresso?.Report($"[LINK WEBAPP] Sincronizando WebApp de modo ultra-rápido: {prodWebApp} -> {targetWebApp}");

                // 1. Processa Subdiretórios (Bin, Views, Content, Scripts, App_Data, App_Themes, etc.)
                var subDirs = Directory.GetDirectories(prodWebApp);
                foreach (var dirProd in subDirs)
                {
                    string dirName = Path.GetFileName(dirProd);
                    string dirTarget = Path.Combine(targetWebApp, dirName);

                    if (!Directory.Exists(dirTarget))
                    {
                        // Se a subpasta não existe no específico, cria Junction Link instantâneo (0ms)
                        CriarJunctionRapida(dirTarget, dirProd, progresso);
                    }
                    else
                    {
                        // Se a subpasta já existe (ex: Bin com DLLs compiladas específicas),
                        // vincula os arquivos ausentes via NTFS Hard Links (0ms por arquivo, 0 bytes copiados)
                        VincularArquivosInstantaneo(dirProd, dirTarget, progresso);
                    }
                }

                // 2. Processa Arquivos Raiz (Global.asax, web.config, etc.)
                var rootFiles = Directory.GetFiles(prodWebApp);
                foreach (var fileProd in rootFiles)
                {
                    string fileName = Path.GetFileName(fileProd);
                    string fileTarget = Path.Combine(targetWebApp, fileName);

                    if (!File.Exists(fileTarget))
                    {
                        VincularOuCopiarArquivo(fileProd, fileTarget, progresso);
                    }
                }

                _verificadosNestaSessao.Add(targetWebAppNormalizado);
                progresso?.Report("[LINK WEBAPP] Vínculo instantâneo concluído com sucesso.");
            });
        }

        private void CriarJunctionRapida(string destino, string origem, IProgress<string>? progresso)
        {
            try
            {
                // Tenta Win32 API CreateSymbolicLink (instantâneo, sem overhead de processo)
                if (CreateSymbolicLink(destino, origem, SYMBOLIC_LINK_FLAG_DIRECTORY | SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE))
                {
                    progresso?.Report($"[LINK RÁPIDO] Junction criado: {Path.GetFileName(destino)} -> {origem}");
                    return;
                }
            }
            catch { }

            // Fallback via cmd.exe mklink /J
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{destino}\" \"{origem}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                proc.WaitForExit();
                string dirName = Path.GetFileName(destino);
                if (proc.ExitCode == 0)
                {
                    progresso?.Report($"[LINK WEBAPP] Junction criado via cmd: {dirName}");
                }
                else
                {
                    progresso?.Report($"[LINK WARN] Falha ao criar junction para {dirName}. Vinculando arquivos...");
                    VincularArquivosInstantaneo(origem, destino, progresso);
                }
            }
        }

        private void VincularArquivosInstantaneo(string origemDir, string destinoDir, IProgress<string>? progresso)
        {
            try
            {
                if (!Directory.Exists(destinoDir))
                    Directory.CreateDirectory(destinoDir);

                // Varredura apenas no diretório superior (TopDirectoryOnly) para evitar trava de IO em subpastas grandes
                var arquivosOrigem = Directory.GetFiles(origemDir, "*.*", SearchOption.TopDirectoryOnly);
                int vinculados = 0;

                foreach (var arqOrigem in arquivosOrigem)
                {
                    string arqNome = Path.GetFileName(arqOrigem);
                    string arqDestino = Path.Combine(destinoDir, arqNome);

                    if (!File.Exists(arqDestino))
                    {
                        VincularOuCopiarArquivo(arqOrigem, arqDestino, null);
                        vinculados++;
                    }
                }

                if (vinculados > 0)
                {
                    progresso?.Report($"[LINK RÁPIDO] Vinculados {vinculados} arquivo(s) ausente(s) em {Path.GetFileName(destinoDir)}");
                }
            }
            catch (Exception ex)
            {
                progresso?.Report($"[WARN WEBAPP] Erro ao sincronizar subpasta {Path.GetFileName(origemDir)}: {ex.Message}");
            }
        }

        private void VincularOuCopiarArquivo(string origem, string destino, IProgress<string>? progresso)
        {
            try
            {
                // NTFS Hard Link: Instantâneo (0ms), apontamento de ponteiro NTFS sem duplicar espaço em disco
                if (CreateHardLink(destino, origem, IntPtr.Zero))
                {
                    progresso?.Report($"[HARDLINK] {Path.GetFileName(destino)} vinculado.");
                    return;
                }
            }
            catch { }

            // Fallback caso Hard Link falhe (ex: partições diferentes)
            try
            {
                File.Copy(origem, destino, overwrite: false);
                progresso?.Report($"[CÓPIA] {Path.GetFileName(destino)} copiado.");
            }
            catch { }
        }
    }
}
