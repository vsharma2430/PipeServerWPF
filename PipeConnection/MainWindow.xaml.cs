using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PipeConnection
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private PipeServerManager _pipeServerManager;
        private const string PIPE_NAME = "STAAD_HELPER_PIPE";
        public MainWindow()
        {
            InitializeComponent();
            _pipeServerManager = new PipeServerManager();
            _pipeServerManager.LogMessageReceived += OnLogMessageReceived;
        }

        private async void StartServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StartServerButton.IsEnabled = false;
                StatusLabel.Content = "Starting server...";

                bool success = await _pipeServerManager.StartServerAsync(PIPE_NAME);

                if (success)
                {
                    StatusLabel.Content = $"Server started on pipe: {PIPE_NAME}";
                    StopServerButton.IsEnabled = true;
                }
                else
                {
                    StatusLabel.Content = "Failed to start server";
                    StartServerButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting server: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StartServerButton.IsEnabled = true;
            }
        }

        private void StopServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _pipeServerManager.StopServer();
                StatusLabel.Content = "Server stopped";
                StartServerButton.IsEnabled = true;
                StopServerButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping server: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnLogMessageReceived(object sender, string message)
        {
            // Ensure UI updates happen on the UI thread
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LogTextBox.AppendText($"{DateTime.Now:HH:mm:ss} - {message}\n");
                LogTextBox.ScrollToEnd();
            }));
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            _pipeServerManager?.StopServer();
            base.OnClosing(e);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                StartServerButton.IsEnabled = false;
                StatusLabel.Content = "Starting server...";

                bool success = await _pipeServerManager.StartServerAsync(PIPE_NAME);

                if (success)
                {
                    StatusLabel.Content = $"Server started on pipe: {PIPE_NAME}";
                    StopServerButton.IsEnabled = true;
                }
                else
                {
                    StatusLabel.Content = "Failed to start server";
                    StartServerButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting server: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StartServerButton.IsEnabled = true;
            }
        }

        private async void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _pipeServerManager.StopServer();
                StatusLabel.Content = "Server stopped";
                StartServerButton.IsEnabled = true;
                StopServerButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping server: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
