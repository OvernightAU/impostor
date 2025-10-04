using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Impostor.Api.Events.Managers;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Messages.S2C;
using Impostor.Hazel;
using Impostor.Hazel.Abstractions;
using Impostor.Server.Config;
using Impostor.Server.Events.Client;
using Impostor.Server.Net.Factories;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Net.Manager
{
    internal partial class ClientManager
    {
        private static readonly DateTime LastDateTime = new(2025, 6, 21);

        public static bool IsVersionSupported(int version)
        {
            var time = GameVersion.ParseVersion(GameVersion.Version2String(version));

            return time >= LastDateTime;
        }

        private readonly ILogger<ClientManager> _logger;
        private readonly ConcurrentDictionary<int, ClientBase> _clients;
        private readonly IClientFactory _clientFactory;
        private readonly IEventManager _eventManager;
        private int _idLast;

        public ClientManager(ILogger<ClientManager> logger, IClientFactory clientFactory, IEventManager eventManager)
        {
            _logger = logger;
            _clientFactory = clientFactory;
            _clients = new ConcurrentDictionary<int, ClientBase>();
            _eventManager = eventManager;
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

        public async ValueTask RegisterConnectionAsync(IHazelConnection connection, string name, int clientVersion, string deviceId = "")
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
            client.DeviceId = deviceId;
            client.VersionSupported = IsVersionSupported(clientVersion);
            _logger.LogInformation($"Player {client.Name} ({client.DeviceId}) connected.");
            _clients.TryAdd(id, client);

            await _eventManager.CallAsync(new ClientConnectedEvent(connection, client));
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
