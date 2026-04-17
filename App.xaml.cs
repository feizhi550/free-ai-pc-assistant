using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using AIPCAssistant.Services;
using AIPCAssistant.Tools;
using AIPCAssistant.ViewModels;
using AIPCAssistant.Models;

namespace AIPCAssistant
{
    public partial class App : Application
    {
        private readonly IHost _host;

        public App()
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(services);
                })
                .UseSerilog((context, configuration) =>
                {
                    configuration
                        .MinimumLevel.Information()
                        .WriteTo.File("logs/log.txt", rollingInterval: Serilog.RollingInterval.Day);
                })
                .Build();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<AppSettings>();
            services.AddHttpClient<OllamaService>();
            services.AddSingleton<SystemMonitorService>();
            services.AddSingleton<RollbackManager>();
            services.AddSingleton<GlobalExceptionHandler>();
            services.AddSingleton<AIDecisionEngine>();
            services.AddHostedService(provider => provider.GetRequiredService<AIDecisionEngine>());

            services.AddSingleton<ISystemTool, CacheCleanerTool>();
            services.AddSingleton<ISystemTool, UninstallTool>();
            services.AddSingleton<ISystemTool, SystemOptimizerTool>();

            services.AddSingleton<MainViewModel>();
            services.AddTransient<MainWindow>();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            await _host.StartAsync();

            var exceptionHandler = _host.Services.GetRequiredService<GlobalExceptionHandler>();
            exceptionHandler.Initialize();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();
            base.OnExit(e);
        }
    }
}