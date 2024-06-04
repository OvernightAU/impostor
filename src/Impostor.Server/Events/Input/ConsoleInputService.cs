using System;
using System.CommandLine.Rendering;
using System.Threading;
using System.Threading.Tasks;
using Impostor.Api.Events.Managers;
using Impostor.Api.Net.Manager;
using Impostor.Server.Config;
using Impostor.Server.Net.Manager;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impostor.Server.Input
{
    public class ConsoleInputService : BackgroundService
    {
        private readonly ILogger<ConsoleInputService> _logger;
        private readonly ConsoleInputConfig _config;
        private readonly IEventManager _eventManager;
        private readonly IClientManager _clientManager;

        private SusLine? _susLine;

        public ConsoleInputService(ILogger<ConsoleInputService> logger, IOptions<ConsoleInputConfig> config, IEventManager eventManager, IClientManager clientManager)
        {
            _logger = logger;
            _config = config.Value;
            _eventManager = eventManager;
            _clientManager = clientManager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_config.SusLine && !ConsoleFormatInfo.CurrentInfo.SupportsAnsiCodes)
            {
                if (VirtualTerminalMode.TryEnable().IsEnabled)
                {
                    _logger.LogWarning("Enabled experimental windows ANSI mode");
                }
                else
                {
                    _logger.LogWarning("Your terminal doesn't support ANSI, falling back to System.Console input");
                    _config.SusLine = false;
                }
            }

            if (_config.SusLine)
            {
                _susLine = new SusLine();
            }

            await Task.Yield();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    string? line;

                    if (_config.SusLine)
                    {
                        line = await _susLine!.ReadLineAsync(stoppingToken);
                    }
                    else
                    {
                        var task = Task.Run(Console.ReadLine, stoppingToken);
                        await Task.WhenAny(task, Task.Delay(Timeout.Infinite, stoppingToken));

                        line = task.IsCompleted ? task.GetAwaiter().GetResult() : null;
                    }

                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    Console.WriteLine("> " + line);

                    _logger.LogTrace("Console input received: {line}", line);
                    await _eventManager.CallAsync(new ConsoleInputEvent(line, _clientManager));
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception caught handling console input");
                }

                _susLine?.Update();
            }

            _susLine?.Dispose();
        }
    }
}
