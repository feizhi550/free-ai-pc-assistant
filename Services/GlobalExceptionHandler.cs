using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace AIPCAssistant.Services
{
    public class GlobalExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public void Initialize()
        {
            Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            _logger.LogInformation("Global exception handler initialized");
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            HandleException(e.Exception, "Unhandled UI thread exception");
            e.Handled = true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            if (exception != null)
            {
                HandleException(exception, "Unhandled non-UI thread exception");
            }
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            HandleException(e.Exception, "Unobserved task exception");
            e.SetObserved();
        }

        private void HandleException(Exception exception, string context)
        {
            try
            {
                _logger.LogError(exception, "{Context}: {Message}", context, exception.Message);
                string errorMessage = $"An error occurred in the application:\n{exception.Message}\n\nError details have been recorded in the log file.";

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        errorMessage,
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling exception");
            }
        }

        public bool HandleSpecificException(Exception exception)
        {
            try
            {
                if (exception is System.Net.Http.HttpRequestException httpEx)
                {
                    _logger.LogError(httpEx, "HTTP request exception: {Message}", httpEx.Message);
                    ShowErrorMessage("Network Connection Error", "Unable to connect to Ollama service. Please ensure Ollama is started and running.");
                    return true;
                }
                else if (exception is System.IO.IOException ioEx)
                {
                    _logger.LogError(ioEx, "IO exception: {Message}", ioEx.Message);
                    ShowErrorMessage("File Operation Error", "File operation failed. Please check permissions or if the file is in use.");
                    return true;
                }
                else if (exception is System.UnauthorizedAccessException accessEx)
                {
                    _logger.LogError(accessEx, "Access exception: {Message}", accessEx.Message);
                    ShowErrorMessage("Insufficient Permissions", "This operation requires administrator privileges. Please run the application as administrator.");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling specific exception");
                return false;
            }
        }

        private void ShowErrorMessage(string title, string message)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        message,
                        title,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing error message");
            }
        }
    }
}