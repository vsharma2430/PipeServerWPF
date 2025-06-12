using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PipeConnection
{
    public class PipeServer : IDisposable
    {
        private readonly string _pipeName;
        private readonly int _maxNumberOfServerInstances;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _isRunning;
        private bool _disposed;

        // Events for WPF data binding and UI updates
        public event EventHandler<string> MessageReceived;
        public event EventHandler<string> ClientConnected;
        public event EventHandler<string> ClientDisconnected;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<string> StatusChanged;

        public bool IsRunning => _isRunning;
        public string PipeName => _pipeName;

        public PipeServer(string pipeName, int maxNumberOfServerInstances = 1)
        {
            if (string.IsNullOrWhiteSpace(pipeName))
                throw new ArgumentException("Pipe name cannot be null or empty", nameof(pipeName));

            _pipeName = pipeName;
            _maxNumberOfServerInstances = Math.Max(1, maxNumberOfServerInstances);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            if (_isRunning)
            {
                OnStatusChanged("Server is already running");
                return;
            }

            try
            {
                _isRunning = true;
                OnStatusChanged("Starting pipe server...");

                // Start listening for connections
                await ListenForConnectionsAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                OnStatusChanged("Server stopped");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to start server: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            OnStatusChanged("Stopping pipe server...");
            _cancellationTokenSource?.Cancel();
            _isRunning = false;
        }

        private async Task ListenForConnectionsAsync(CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            try
            {
                // Create multiple server instances if needed
                for (int i = 0; i < _maxNumberOfServerInstances; i++)
                {
                    tasks.Add(HandleClientConnectionAsync(cancellationToken));
                }

                // Wait for all tasks to complete
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Error in connection listener: {ex.Message}");
            }
        }

        private async Task HandleClientConnectionAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                NamedPipeServerStream pipeServer = null;

                try
                {
                    // Create a new pipe server instance
                    pipeServer = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.InOut,
                        _maxNumberOfServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    OnStatusChanged("Waiting for client connection...");

                    // Wait for client connection
                    await pipeServer.WaitForConnectionAsync(cancellationToken);

                    OnClientConnected($"Client connected to pipe: {_pipeName}");

                    // Handle the connected client
                    await ProcessClientAsync(pipeServer, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping the server
                    break;
                }
                catch (IOException ex)
                {
                    OnErrorOccurred($"IO Error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"Unexpected error: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        if (pipeServer?.IsConnected == true)
                        {
                            pipeServer.Disconnect();
                            OnClientDisconnected("Client disconnected");
                        }
                    }
                    catch (Exception ex)
                    {
                        OnErrorOccurred($"Error disconnecting client: {ex.Message}");
                    }

                    pipeServer?.Dispose();
                }

                // Small delay before accepting next connection
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }
        }

        private async Task ProcessClientAsync(NamedPipeServerStream pipeServer, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096]; // Increased buffer size

            try
            {
                while (pipeServer.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    // Read message from client
                    int bytesRead = await pipeServer.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    if (bytesRead == 0)
                    {
                        // Client closed connection
                        break;
                    }

                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    OnMessageReceived(receivedMessage);

                    // Send response back to client
                    string response = await ProcessMessageAsync(receivedMessage);
                    await SendResponseAsync(pipeServer, response, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (IOException ex) when (ex.Message.Contains("pipe has been ended"))
            {
                // Client disconnected normally
                OnClientDisconnected("Client disconnected normally");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Error processing client: {ex.Message}");
            }
        }

        private async Task SendResponseAsync(NamedPipeServerStream pipeServer, string message, CancellationToken cancellationToken)
        {
            try
            {
                byte[] responseBytes = Encoding.UTF8.GetBytes(message);
                await pipeServer.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);
                await pipeServer.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Error sending response: {ex.Message}");
            }
        }

        // Virtual method that can be overridden to customize message processing
        protected virtual async Task<string> ProcessMessageAsync(string message)
        {
            // Default implementation - can be overridden in derived classes
            await Task.Delay(1); // Simulate async processing
            return $"Echo: {message} (Received at {DateTime.Now:HH:mm:ss})";
        }

        // Public method to send a message to all connected clients
        public async Task BroadcastMessageAsync(string message)
        {
            // This would require keeping track of connected clients
            // Implementation depends on specific requirements
            await Task.Run(() => {
                // Placeholder for broadcast functionality
                OnStatusChanged($"Broadcasting: {message}");
            });
        }

        // Event handlers
        protected virtual void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(this, message);
        }

        protected virtual void OnClientConnected(string info)
        {
            ClientConnected?.Invoke(this, info);
        }

        protected virtual void OnClientDisconnected(string info)
        {
            ClientDisconnected?.Invoke(this, info);
        }

        protected virtual void OnErrorOccurred(string error)
        {
            ErrorOccurred?.Invoke(this, error);
        }

        protected virtual void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                Stop();
                _cancellationTokenSource?.Dispose();
                _disposed = true;
            }
        }

        ~PipeServer()
        {
            Dispose(false);
        }

        #endregion
    }

    // Usage example class for WPF integration
    public class PipeServerManager
    {
        private PipeServer _pipeServer;

        public event EventHandler<string> LogMessageReceived;

        public async Task<bool> StartServerAsync(string pipeName)
        {
            try
            {
                _pipeServer = new PipeServer(pipeName);

                // Subscribe to events
                _pipeServer.MessageReceived += OnMessageReceived;
                _pipeServer.ClientConnected += OnClientConnected;
                _pipeServer.ClientDisconnected += OnClientDisconnected;
                _pipeServer.ErrorOccurred += OnErrorOccurred;
                _pipeServer.StatusChanged += OnStatusChanged;

                // Start the server in a background task
                _ = Task.Run(async () => await _pipeServer.StartAsync());

                return true;
            }
            catch (Exception ex)
            {
                LogMessageReceived?.Invoke(this, $"Failed to start server: {ex.Message}");
                return false;
            }
        }

        public void StopServer()
        {
            _pipeServer?.Stop();
            _pipeServer?.Dispose();
            _pipeServer = null;
        }

        private void OnMessageReceived(object sender, string message)
        {
            LogMessageReceived?.Invoke(this, $"[MESSAGE] {message}");
        }

        private void OnClientConnected(object sender, string info)
        {
            LogMessageReceived?.Invoke(this, $"[CONNECTED] {info}");
        }

        private void OnClientDisconnected(object sender, string info)
        {
            LogMessageReceived?.Invoke(this, $"[DISCONNECTED] {info}");
        }

        private void OnErrorOccurred(object sender, string error)
        {
            LogMessageReceived?.Invoke(this, $"[ERROR] {error}");
        }

        private void OnStatusChanged(object sender, string status)
        {
            LogMessageReceived?.Invoke(this, $"[STATUS] {status}");
        }
    }
}