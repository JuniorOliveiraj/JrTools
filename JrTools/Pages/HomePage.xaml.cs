using JrTools.Dto;
using JrTools.Services;
using JrTools.Services.Db;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace JrTools.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage : Page
    {


        public HomePage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            this.Loaded += RenderizarPages_Loaded;
            BuscarBancoDeHorasAsync();
            

        }
        private void RenderizarPages_Loaded(object sender, RoutedEventArgs e)
        {
            DefinirHorarioPrevistoAsync();
            InnerFrame.Navigate(typeof(JrTools.Pages.FecharProcessos));
            CarregarLancamentosDoDiaAsync();
        }

        private async Task DefinirHorarioPrevistoAsync()
        {
            var dados = await PerfilPessoalHelper.LerConfiguracoesAsync();

            var buscarApi = new ApiBennerService(dados);

            string resultadoApiString = await buscarApi.GetAsync("/api/V1/RH/Ponto/Colaborador/PontoMovel");
            DateTime hoje = DateTime.Today;

            // Horário padrão
            DateTime entrada1 = hoje.AddHours(8);
            DateTime saida1 = hoje.AddHours(12);
            DateTime entrada2 = hoje.AddHours(13);
            DateTime saida2 = hoje.AddHours(17).AddMinutes(30);

            // Variável para saída prevista
            DateTime saidaPrevista = hoje.AddHours(17).AddMinutes(30); // default 17:30

            // Parse do JSON
            using var doc = JsonDocument.Parse(resultadoApiString);
            var dias = doc.RootElement.GetProperty("Dias");

            JsonElement? diaAtual = null;

            // Encontra o dia de hoje
            foreach (var dia in dias.EnumerateArray())
            {
                var dataApuracao = dia.GetProperty("DataApuracao").GetString();
                if (DateTime.Parse(dataApuracao).Date == hoje)
                {
                    diaAtual = dia;
                    break;
                }
            }

            // Lista de batidas do dia atual
            List<DateTime> marcacoes = new List<DateTime>();
            if (diaAtual.HasValue)
            {
                foreach (var m in diaAtual.Value.GetProperty("Marcacoes").EnumerateArray())
                {
                    var hora = DateTime.Parse(m.GetProperty("HoraMarcacao").GetString());
                    marcacoes.Add(hora);
                }

                marcacoes = marcacoes.OrderBy(x => x).ToList();

                if (marcacoes.Count >= 1)
                {
                    entrada1 = marcacoes[0];

                    if (marcacoes.Count >= 2)
                    {
                        saida1 = marcacoes[1];

                        var horasManha = saida1 - entrada1;

                        // Calcula a saída prevista com base na jornada de 8h30
                        var jornadaTotal = TimeSpan.FromHours(8.5);
                        var restante = jornadaTotal - horasManha;

                        // Se já tem entrada da tarde, calcula a saída prevista
                        if (marcacoes.Count >= 3)
                        {
                            entrada2 = marcacoes[2];
                            saidaPrevista = entrada2 + restante;
                        }
                        else
                        {
                            entrada2 = saida1.AddHours(1); // almoço mínimo 1h
                            saidaPrevista = entrada2 + restante;
                        }

                        // Se a saída final já foi batida, podemos ignorar para a variável prevista
                        // saida2 = marcacoes.Count >= 4 ? marcacoes[3] : saida2;
                    }
                    else
                    {
                        // Apenas 1 batida
                        saida1 = entrada1.AddHours(4);
                        entrada2 = saida1.AddHours(1);
                        saidaPrevista = entrada2.AddHours(4.5);
                    }
                }
            }

            // Atualiza os TimePickers (com os valores reais)
            Entrada1TimePicker.Time = entrada1.TimeOfDay;
            Saida1TimePicker.Time = saida1.TimeOfDay;
            Entrada2TimePicker.Time = entrada2.TimeOfDay;
            Saida2TimePicker.Time = saida2.TimeOfDay;
            var horasaida = saidaPrevista.TimeOfDay;

            HorariosaidaPrevistaText.Text = $"{horasaida.Hours:D2}:{horasaida.Minutes:D2}";

        }




        private async Task BuscarBancoDeHorasAsync()
        {
            var dados = await PerfilPessoalHelper.LerConfiguracoesAsync();

            var buscarApi = new ApiBennerService(dados);

            string resultado = await buscarApi.GetAsync(
                "/api/v1/RH/Ponto/Colaborador/BancoHoras"
            );

            // Faz o parse do JSON
            using var doc = JsonDocument.Parse(resultado);

            // Pega o valor de "SaldoFinal"
            string saldoFinal = doc.RootElement.GetProperty("SaldoFinal").GetString();

            // Formata como você quer
            string saldoFormatado = saldoFinal.Insert(2, "h ") + "m";
            string saldoFormatado2 = saldoFormatado.Replace(":", "");

            BancoDeHorasText.Text = saldoFormatado2;
            // var obj = JsonSerializer.Deserialize<SeuDto>(resultado);


        }
        private void CalcularHorasButton_Click(object sender, RoutedEventArgs e)
        {
            CalculadoraInfoBar.IsOpen = false;

            try
            {
                // Pega os valores dos TimePickers
                TimeSpan entrada1 = Entrada1TimePicker.Time;
                TimeSpan saida1 = Saida1TimePicker.Time;
                TimeSpan entrada2 = Entrada2TimePicker.Time;
                TimeSpan saida2 = Saida2TimePicker.Time;

                // Validação simples
                if (saida1 < entrada1 || saida2 < entrada2)
                {
                    CalculadoraInfoBar.Title = "Erro de Lógica";
                    CalculadoraInfoBar.Message = "O horário de saída não pode ser anterior ao de entrada.";
                    CalculadoraInfoBar.IsOpen = true;
                    TotalHorasTextBlock.Text = "Erro";
                    return;
                }

                // Calcula a duração de cada período
                TimeSpan periodo1 = saida1.Subtract(entrada1);
                TimeSpan periodo2 = saida2.Subtract(entrada2);

                // Soma os períodos para obter o total
                TimeSpan totalHoras = periodo1.Add(periodo2);

                // Formata o resultado e exibe na tela
                TotalHorasTextBlock.Text = $"{totalHoras.Hours:D2}:{totalHoras.Minutes:D2}";
            }
            catch (Exception ex)
            {
                // Captura qualquer outro erro inesperado
                CalculadoraInfoBar.Title = "Erro Inesperado";
                CalculadoraInfoBar.Message = $"Ocorreu um erro: {ex.Message}";
                CalculadoraInfoBar.IsOpen = true;
                TotalHorasTextBlock.Text = "Erro";
            }
        }



        private async Task CarregarLancamentosDoDiaAsync()
        {
            try
            {
                var perfil = await PerfilPessoalHelper.LerConfiguracoesAsync();
                string token = perfil.ApiToggl;
                if (string.IsNullOrWhiteSpace(token)) return;

                var toggl = new TogglClient(token);
                var entries = await toggl.GetTodayTimeEntriesAsync(DateTime.Today);

                double totalHoras = 0; 

                foreach (var entry in entries.EnumerateArray())
                {
                    double durHoras = 0;

                    if (entry.TryGetProperty("duration", out var durProp))
                    {
                        var dur = durProp.GetDouble();
                        if (dur > 0) durHoras = dur / 3600.0; 
                    }

                    totalHoras += durHoras; 
                }

                string formato = "H ";
                TotalHorasLancadas.Text = formato +=  totalHoras.ToString("0.##"); // duas casas decimais
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar lançamentos: {ex.Message}");
            }
        }

    }
}
