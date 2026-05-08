using System;
using System.Linq;
using System.Threading.Tasks;
using JrTools.Services.Db;
using JrTools.Dto;
using Microsoft.Windows.AppNotifications;
using System.Text.Json;

namespace JrTools.Services
{
    public class NotificationService
    {
        private static NotificationService? _instance;
        public static NotificationService Instance => _instance ??= new NotificationService();

        private bool _notified13h = false;
        private bool _notified17h = false;
        private DateTime _lastCheckDate = DateTime.MinValue;

        private NotificationService() { }

        public async Task CheckAndNotifyTogglHoursAsync()
        {
            var config = await ConfigHelper.LerConfiguracoesAsync();
            if (!config.NotificarHorasToggl) return;

            var now = DateTime.Now;

            // Reset flags if it's a new day
            if (now.Date != _lastCheckDate.Date)
            {
                _notified13h = false;
                _notified17h = false;
                _lastCheckDate = now.Date;
            }

            if (now.Hour == 13 && !_notified13h)
            {
                await CheckHoursAndNotify(4, "Lembrete das 13h");
                _notified13h = true;
            }
            else if (now.Hour == 17 && !_notified17h)
            {
                await CheckHoursAndNotify(8, "Lembrete das 17h");
                _notified17h = true;
            }
        }

        private async Task CheckHoursAndNotify(double threshold, string title)
        {
            try
            {
                var dadosPessoais = await PerfilPessoalHelper.LerConfiguracoesAsync();
                if (string.IsNullOrEmpty(dadosPessoais.ApiToggl)) return;

                var togglClient = new TogglClient(dadosPessoais.ApiToggl);
                var entries = await togglClient.GetTodayTimeEntriesAsync(DateTime.Today);

                double totalSeconds = 0;
                foreach (var entry in entries.EnumerateArray())
                {
                    if (entry.TryGetProperty("duration", out var durationProp))
                    {
                        var duration = durationProp.GetDouble();
                        if (duration > 0)
                            totalSeconds += duration;
                    }
                }

                double totalHours = totalSeconds / 3600.0;

                if (totalHours < threshold)
                {
                    ShowNotification(
                        title,
                        $"Você lançou apenas {totalHours:F1} horas hoje. O esperado era pelo menos {threshold} horas. Não esqueça de lançar no Toggl!"
                    );
                }
            }
            catch (Exception ex)
            {
                // Silently fail or log
                System.Diagnostics.Debug.WriteLine($"Erro ao verificar horas Toggl: {ex.Message}");
            }
        }

        public void ShowNotification(string title, string message)
        {
            var toastXml = $@"
                <toast>
                    <visual>
                        <binding template='ToastGeneric'>
                            <text>{title}</text>
                            <text>{message}</text>
                        </binding>
                    </visual>
                </toast>";

            var notification = new AppNotification(toastXml);
            AppNotificationManager.Default.Show(notification);
        }
    }
}
