using System;
using System.IO;
using System.Linq;
using Impostor.Api.Events.Managers;
using Impostor.Api.Games;
using Impostor.Api.Games.Managers;
using Impostor.Api.Net.Manager;
using Impostor.Api.Net.Messages;
using Impostor.Hazel.Extensions;
using Impostor.Server.Config;
using Impostor.Server.Events;
using Impostor.Server.Input;
using Impostor.Server.Net;
using Impostor.Server.Net.Factories;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.Messages;
using Impostor.Server.Net.Redirector;
using Impostor.Server.Plugins;
using Impostor.Server.Recorder;
using Impostor.Server.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.ObjectPool;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Hosting;
using Impostor.Api.Plugins;
using Microsoft.AspNetCore.Builder;
using System.Net;
using Impostor.Api.Config;

namespace Impostor.Server
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
#if DEBUG
            var logLevel = LogEventLevel.Debug;
#else
            var logLevel = LogEventLevel.Information;
#endif

            if (args.Contains("--verbose"))
            {
                logLevel = LogEventLevel.Verbose;
            }
            else if (args.Contains("--errors-only"))
            {
                logLevel = LogEventLevel.Error;
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
#if DEBUG
                .MinimumLevel.Override("Microsoft", LogEventLevel.Debug)
#else
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
#endif
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                Log.Information("Starting Impostor v{0}", DotnetUtils.GetVersion());
                CreateHostBuilder(args).Build().Run();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Impostor terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IConfiguration CreateConfiguration(string[] args)
        {
            var configurationBuilder = new ConfigurationBuilder();

            configurationBuilder.AddJsonFile("config.json", true);
            configurationBuilder.AddJsonFile("config.Development.json", true);
            configurationBuilder.AddEnvironmentVariables(prefix: "IMPOSTOR_");
            configurationBuilder.AddCommandLine(args);

            return configurationBuilder.Build();
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            var configuration = CreateConfiguration(args);
            var pluginConfig = configuration.GetSection("PluginLoader")
                .Get<PluginConfig>() ?? new PluginConfig();
            var httpConfig = configuration.GetSection(HttpServerConfig.Section)
                .Get<HttpServerConfig>() ?? new HttpServerConfig();
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .UseContentRoot(Directory.GetCurrentDirectory())
#if DEBUG
                .UseEnvironment(Environment.GetEnvironmentVariable("IMPOSTOR_ENV") ?? "Development")
#else
                .UseEnvironment("Production")
#endif
                .ConfigureAppConfiguration(builder =>
                {
                    builder.AddConfiguration(configuration);
                })
                .ConfigureServices((host, services) =>
                {
                    var consoleInput = host.Configuration
                        .GetSection(ConsoleInputConfig.Section)
                        .Get<ConsoleInputConfig>() ?? new ConsoleInputConfig();

                    var debug = host.Configuration
                        .GetSection(DebugConfig.Section)
                        .Get<DebugConfig>() ?? new DebugConfig();

                    var redirector = host.Configuration
                        .GetSection(ServerRedirectorConfig.Section)
                        .Get<ServerRedirectorConfig>() ?? new ServerRedirectorConfig();

                    services.Configure<DebugConfig>(host.Configuration.GetSection(DebugConfig.Section));
                    services.Configure<AntiCheatConfig>(host.Configuration.GetSection(AntiCheatConfig.Section));
                    services.Configure<ServerConfig>(host.Configuration.GetSection(ServerConfig.Section));
                    services.Configure<ServerRedirectorConfig>(host.Configuration.GetSection(ServerRedirectorConfig.Section));
                    services.Configure<HttpServerConfig>(host.Configuration.GetSection(HttpServerConfig.Section));
                    services.AddHostedService<ConsoleInputService>();

                    if (redirector.Enabled)
                    {
                        if (!string.IsNullOrEmpty(redirector.Locator.Redis))
                        {
                            // When joining a game, it retrieves the game server ip from redis.
                            // When a game has been created on this node, it stores the game code with its ip in redis.
                            services.AddSingleton<INodeLocator, NodeLocatorRedis>();

                            // Dependency for the NodeLocatorRedis.
                            services.AddStackExchangeRedisCache(options =>
                            {
                                options.Configuration = redirector.Locator.Redis;
                                options.InstanceName = "ImpostorRedis";
                            });
                        }
                        else if (!string.IsNullOrEmpty(redirector.Locator.UdpMasterEndpoint))
                        {
                            services.AddSingleton<INodeLocator, NodeLocatorUdp>();

                            if (redirector.Master)
                            {
                                services.AddHostedService<NodeLocatorUdpService>();
                            }
                        }
                        else
                        {
                            throw new Exception("Missing a valid NodeLocator config.");
                        }

                        // Use the configuration as source for the list of nodes to provide
                        // when creating a game.
                        services.AddSingleton<INodeProvider, NodeProviderConfig>();
                    }
                    else
                    {
                        // Redirector is not enabled but the dependency is still required.
                        // So we provide one that ignores all calls.
                        services.AddSingleton<INodeLocator, NodeLocatorNoOp>();
                    }


                    services.AddSingleton<ClientManager>();
                    services.AddSingleton<IClientManager>(p => p.GetRequiredService<ClientManager>());


                    if (redirector.Enabled && redirector.Master)
                    {
                        services.AddSingleton<IClientFactory, ClientFactory<ClientRedirector>>();

                        // For a master server, we don't need a GameManager.
                    }
                    else
                    {
                        if (debug.GameRecorderEnabled)
                        {
                            services.AddSingleton<ObjectPoolProvider>(new DefaultObjectPoolProvider());
                            services.AddSingleton<ObjectPool<PacketSerializationContext>>(serviceProvider =>
                            {
                                var provider = serviceProvider.GetRequiredService<ObjectPoolProvider>();
                                var policy = new PacketSerializationContextPooledObjectPolicy();
                                return provider.Create(policy);
                            });

                            services.AddSingleton<PacketRecorder>();
                            services.AddHostedService(sp => sp.GetRequiredService<PacketRecorder>());
                            services.AddSingleton<IClientFactory, ClientFactory<ClientRecorder>>();
                        }
                        else
                        {
                            services.AddSingleton<IClientFactory, ClientFactory<Client>>();
                        }

                        services.AddSingleton<GameManager>();
                        services.AddSingleton<IGameManager>(p => p.GetRequiredService<GameManager>());
                    }

                    services.AddHazel();
                    services.AddSingleton<IMessageWriterProvider, MessageWriterProvider>();
                    services.AddSingleton<IGameCodeFactory, GameCodeFactory>();
                    services.AddSingleton<IEventManager, EventManager>();
                    services.AddSingleton<Matchmaker>();
                    services.AddHostedService<MatchmakerService>();
                })
                .UseSerilog()
                .UseConsoleLifetime()
                .UsePluginLoader(pluginConfig);

            Log.Information("Use Http: " + httpConfig.Enabled.ToString());
            if (httpConfig.Enabled)
            {
                hostBuilder.ConfigureWebHostDefaults(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.AddControllers();
                    });

                    builder.Configure(app =>
                    {
                        var pluginLoaderService = app.ApplicationServices.GetRequiredService<PluginLoaderService>();
                        foreach (var pluginInformation in pluginLoaderService.Plugins)
                        {
                            if (pluginInformation.Startup is IPluginHttpStartup httpStartup)
                            {
                                httpStartup.ConfigureWebApplication(app);
                            }
                        }

                        app.UseRouting();

                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllers();
                        });
                    });

                    builder.ConfigureKestrel(serverOptions =>
                    {
                        serverOptions.Listen(IPAddress.Parse(httpConfig.ListenIp), httpConfig.ListenPort);
                    });
                });
            }

            return hostBuilder;
        }
    }
}
