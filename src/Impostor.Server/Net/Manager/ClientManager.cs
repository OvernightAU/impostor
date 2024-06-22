using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Messages.S2C;
using Impostor.Hazel;
using Impostor.Hazel.Abstractions;
using Impostor.Server.Config;
using Impostor.Server.Net.Factories;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Net.Manager
{
    internal partial class ClientManager
    {
        public static HashSet<int> SupportedVersions { get; } = new HashSet<int>
        {
            GameVersion.GetVersion(2024, 04, 02), // 2024.4.2
            GameVersion.GetVersion(2024, 04, 06), // 2024.4.6
            GameVersion.GetVersion(2024, 04, 09), // 2024.4.9
            GameVersion.GetVersion(2024, 04, 14), // 2024.4.14
            GameVersion.GetVersion(2024, 04, 28), // 2024.4.28
            GameVersion.GetVersion(2024, 05, 26), // 2024.5.26
            GameVersion.GetVersion(2024, 05, 28), // 2024.5.28
            GameVersion.GetVersion(2024, 05, 29), // 2024.5.29
            GameVersion.GetVersion(2024, 05, 30), // 2024.5.30
            GameVersion.GetVersion(2024, 05, 31), // 2024.5.31
            GameVersion.GetVersion(2024, 06, 4),  // 2024.6.4
            GameVersion.GetVersion(2024, 06, 22), // 2024.6.22
        };

        private readonly ILogger<ClientManager> _logger;
        private readonly ConcurrentDictionary<int, ClientBase> _clients;
        private readonly IClientFactory _clientFactory;
        private int _idLast;

        public ClientManager(ILogger<ClientManager> logger, IClientFactory clientFactory)
        {
            _logger = logger;
            _clientFactory = clientFactory;
            _clients = new ConcurrentDictionary<int, ClientBase>();
        }

        public IEnumerable<ClientBase> Clients => _clients.Values;

        public int NextId()
        {
            var clientId = Interlocked.Increment(ref _idLast);

            if (clientId < 1)
            {
                // Super rare but reset the _idLast because of overflow.
                _idLast = 0;

                // And get a new id.
                clientId = Interlocked.Increment(ref _idLast);
            }

            return clientId;
        }

        public IClient? GetClientById(int clientId)
        {
            return Clients.FirstOrDefault(c => c.Id == clientId);
        }

        public async ValueTask RegisterConnectionAsync(IHazelConnection connection, string name, int clientVersion)
        {
            if (name.Length > 12)
            {
                using var packet = MessageWriter.Get(MessageType.Reliable);
                Message01JoinGameS2C.SerializeError(packet, false, DisconnectReason.Custom, DisconnectMessages.UsernameLength);
                await connection.SendAsync(packet);
                return;
            }

            if (string.IsNullOrWhiteSpace(name) || !name.All(TextBox.IsCharAllowed))
            {
                using var packet = MessageWriter.Get(MessageType.Reliable);
                Message01JoinGameS2C.SerializeError(packet, false, DisconnectReason.Custom, DisconnectMessages.UsernameIllegalCharacters);
                await connection.SendAsync(packet);
                return;
            }

            var client = _clientFactory.Create(connection, name, clientVersion);
            var id = NextId();

            client.Id = id;
            client.GameVersion = clientVersion;
            _logger.LogTrace("Client connected.");
            _clients.TryAdd(id, client);
        }

        public void Remove(IClient client)
        {
            _logger.LogTrace("Client disconnected.");
            _clients.TryRemove(client.Id, out _);
        }

        public bool Validate(IClient client)
        {
            return client.Id != 0
                   && _clients.TryGetValue(client.Id, out var registeredClient)
                   && ReferenceEquals(client, registeredClient);
        }
    }
}
