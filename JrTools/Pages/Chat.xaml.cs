using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
    public sealed partial class Chat : Page
    {
      


        public ObservableCollection<ChatMessage> Messages { get; set; } = new();

        public Chat()
        {
            this.InitializeComponent();

            // Conecta o ItemsRepeater à lista de mensagens
            MessagesRepeater.ItemsSource = Messages;

            // Evento de clique do botão
            SendButton.Click += SendButton_Click;

            // Permite enviar com Enter
            InputBox.KeyDown += InputBox_KeyDown;
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && !string.IsNullOrWhiteSpace(InputBox.Text))
            {
                e.Handled = true; // evita pular linha
                SendMessage();
            }
        }

        private void SendMessage()
        {
            string userText = InputBox.Text.Trim();
            if (string.IsNullOrEmpty(userText)) return;

            // Adiciona mensagem do usuário
            Messages.Add(new ChatMessage
            {
                Text = userText,
                StyleKey = (Style)Resources["UserMessageBubble"]
            });

            InputBox.Text = string.Empty;
            ScrollToBottom();

            // Simula resposta da IA (aqui você vai integrar sua API depois)
            RespondFromBot(userText);
        }

        private async void RespondFromBot(string userText)
        {
            // Simulação de processamento da IA
            await Task.Delay(500);

            string botResponse = $"Você disse: {userText}"; // Aqui você chama a IA real

            Messages.Add(new ChatMessage
            {
                Text = botResponse,
                StyleKey = (Style)Resources["BotMessageBubble"]
            });

            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            // Faz o ScrollViewer rolar até o fim
            ChatScrollViewer.UpdateLayout();
            ChatScrollViewer.ChangeView(null, ChatScrollViewer.ScrollableHeight, null);
        }
    }

    public class ChatMessage
    {
        public string Text { get; set; }
        public Style StyleKey { get; set; }
    }
}

