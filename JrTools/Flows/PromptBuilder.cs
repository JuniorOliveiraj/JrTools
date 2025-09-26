using JrTools.Services;
using JrTools.Services.Db;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JrTools.Flows
{
    public static class PromptBuilder
    {
        public static async Task<string> ConstruirPromptAsync( string commitId,string analiseNegocio) {
            var progresso = (IProgress<string>)new Progress<string>();

            return await Task.Run(async () =>
            {
                var config = await ConfigHelper.LerConfiguracoesAsync();
                var perfil = await PerfilPessoalHelper.LerConfiguracoesAsync();

                if (config == null || perfil == null || string.IsNullOrWhiteSpace(perfil.ApiGemini))
                {
                    throw new InvalidOperationException("❌ Configuração da API Gemini não encontrada.");
                }

                string workingDir = config.DiretorioProducao;
                string caminhoAnalise = Path.Combine(workingDir, "analise_sms.txt");

                progresso?.Report("📂 Lendo Prompt base...");
                var promptBase = RetornarPrimpt();

                progresso?.Report("🔍 Executando git show...");
                var gitHandler = new GitRhProdHandler(new GitService(), string.Empty);
                string alteracoes = await gitHandler.ObterAlteracoesDeCommitAsync(progresso, workingDir, commitId);

                // Salva o diff em arquivo sem travar UI
                string caminhoAlteracao = Path.Combine(workingDir, "alteracao.txt");
                await File.WriteAllTextAsync(caminhoAlteracao, alteracoes);
                progresso?.Report($"📝 Arquivo alteracao.txt salvo: {caminhoAlteracao}");

                progresso?.Report("📖 Lendo análise de negócio (se existir)...");
                if (File.Exists(caminhoAnalise))
                {
                    analiseNegocio = await File.ReadAllTextAsync(caminhoAnalise);
                }

                string analizeFinal = string.IsNullOrEmpty(analiseNegocio)
                    ? "Nesse caso sem analise de negócio"
                    : analiseNegocio;

                return $@"
                    {promptBase}

                    --- ALTERAÇÕES DO COMMIT ---
                    {alteracoes}

                    --- ANÁLISE DE NEGÓCIO ---
                    {analizeFinal}
                    ";
            });


        }

        public static string RetornarPrimpt()
        {
            return @"✅ PROMPT CALIBRADO PARA GERAÇÃO DE DOCUMENTAÇÃO FUNCIONAL
Você será um documentador de sistemas responsável por transformar descrições técnicas (como resultados de git show)
e documentos de análise de negócio (.doc ou .pdf) em uma documentação clara, objetiva e funcional.
Essa documentação será voltada ao time de qualidade e usuários finais (não técnicos).

🔧 Produza sempre dois blocos:
1. Campo de Solução (Resumo da Alteração)
Texto objetivo, direto, em linguagem funcional.

Evite termos técnicos ou nomes de tabelas.

Exemplo de estilo:
""Foi implementado um novo wizard para confirmação da transferência de unidade de colaboradores, com preenchimento automático de dados e integração ao processo de benefícios. Além disso, foi feita uma atualização estrutural na tabela de grupos de benefícios.""

2. Documento Detalhado da Alteração
Estruture com os seguintes blocos (sempre com linguagem acessível):

[Título funcional da alteração]
Resumo explicativo do que foi feito e qual a utilidade para o usuário.

[Descrição técnica simplificada]
Explique os ajustes feitos com linguagem acessível. Substitua nomes de tabelas por legendas compreensíveis.

[Localização dos campos nas visões]
Descreva em que telas ou visões do sistema os campos foram adicionados ou removidos, conforme os documentos de análise enviados.

[Impacto no sistema]

Banco de Dados: descrever mudanças em dados ou campos de forma não técnica.

Funcionalidade: descrever o impacto prático da funcionalidade.

Segurança: se houver, destaque regras de acesso ou papéis envolvidos.

[Procedimentos para Testes]

Liste o que deve ser validado funcionalmente no sistema.

[Observações Finais]
Informe a demanda associada (ex: SMS-1234567) e o objetivo funcional da alteração.

📌 Regras obrigatórias:
Aplique correção ortográfica em todos os textos recebidos e gerados.

Não inclua nomes técnicos de tabelas. Use apenas as legendas visíveis para o usuário.

Use linguagem funcional e acessível (evite termos técnicos e descrições de código).

Leia e utilize informações contidas em documentos de análise (.doc ou .pdf) enviados.

Sempre que receber um git show, analise completamente o conteúdo.

Inclua a localização dos campos nas visões/telas descritas na análise.

Gere automaticamente o arquivo .docx com o conteúdo da documentação ao final.

📝 Fluxo sugerido:
Envie o arquivo .txt com o resultado de git show.

Envie a análise de negócio em .doc ou .pdf.

Solicite a geração da documentação completa.";
        }
    }
}
