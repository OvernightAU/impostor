using System;
using System.Linq;
using System.Text;
using Impostor.Api.Events.Input;
using Impostor.Api.Net.Manager;
using Impostor.Server.Net.Manager;
using Serilog;

namespace Impostor.Server.Input
{
    public class ConsoleInputEvent : IConsoleInputEvent
    {
        private readonly ILogger _logger = Log.ForContext<ConsoleInputEvent>();
        private readonly IClientManager _clientManager;

        public ConsoleInputEvent(string input, IClientManager client)
        {
            _clientManager = client;
            Input = input;
            NormalHandle();
        }

        public void NormalHandle()
        {
            var arguments = Input.Split(' ');
            switch (arguments[0])
            {
                case "help":
                    {
                        Console.WriteLine("Welcome to Modding Us, here are some commands:\n/kick {Player Id}");
                    }

                    break;

                case "exit":
                    {
                        Environment.Exit(-9);
                    }

                    break;

                case "ids":
                    {
                        if (_clientManager.Clients.Count() == 0)
                        {
                            Console.WriteLine("Server has no players (RIP)");
                            return;
                        }


                        var clientList = new StringBuilder();
                        clientList.AppendLine("NAME   | ID   | GAME");
                        clientList.AppendLine("-----------------------");

                        foreach (var client in _clientManager.Clients)
                        {
                            var gameCode = client.Player?.Game.Code.Code ?? "Not in game";
                            clientList.AppendLine($"{client.Name,-7} | {client.Id,-4} | {gameCode}");
                        }

                        Console.WriteLine(clientList.ToString());
                        break;
                    }

                case "kick":
                    {
                        if (arguments.Length < 2)
                        {
                            Console.WriteLine("Missing player id");
                            break;
                        }

                        if (!int.TryParse(arguments[1], out int id))
                        {
                            Console.WriteLine("Invalid ID format");
                            break;
                        }

                        var client = _clientManager.GetClientById(id);
                        var reason = string.Join(" ", arguments.Skip(2)); // Concatenate all arguments after the second one

                        if (!string.IsNullOrEmpty(reason))
                        {
                            client?.DisconnectAsync(Api.Innersloth.DisconnectReason.Custom, reason);
                        }
                        else
                        {
                            client?.DisconnectAsync(Api.Innersloth.DisconnectReason.Kicked);
                        }

                        break;
                    }

                case "warning":
                    {
                        if (arguments.Length < 2)
                        {
                            Console.WriteLine("Bro where is the announcement?");
                            break;
                        }

                        var announcement = string.Join(" ", arguments.Skip(1));

                        foreach (var client in _clientManager.Clients)
                        {
                            var player = client.Player;
                            if (player != null)
                            {
                                var clientCharacter = player.Character;
                                clientCharacter?.SendChatToPlayerAsync(announcement, clientCharacter);
                            }
                        }

                        Console.WriteLine("Warning sent succesfully!");

                        break;
                    }

                case "cls":
                    {
                        Console.Clear();
                    }

                    break;

                default:
                    {
                        Console.WriteLine("Command not recognized.");
                    }

                    break;
            }
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = false;
        }

        public string Input { get; }
    }
}
