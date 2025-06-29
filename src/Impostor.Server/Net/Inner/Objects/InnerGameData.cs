using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Impostor.Api;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Api.Net.Messages;
using Impostor.Server.Net.Inner.Objects.Components;
using Impostor.Server.Net.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Net.Inner.Objects
{
    internal partial class InnerGameData : InnerNetObject, IInnerGameData
    {
        private readonly ILogger<InnerGameData> _logger;
        private readonly Game _game;
        private readonly ConcurrentDictionary<byte, InnerPlayerInfo> _allPlayers;

        private enum RpcCalls
        {
            SetTasks = 29,
            UpdateGameData = 30
        }

        public InnerGameData(ILogger<InnerGameData> logger, Game game, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _game = game;
            _allPlayers = new ConcurrentDictionary<byte, InnerPlayerInfo>();

            Components.Add(this);
            Components.Add(ActivatorUtilities.CreateInstance<InnerVoteBanSystem>(serviceProvider));
        }

        public int PlayerCount => _allPlayers.Count;

        public IReadOnlyDictionary<byte, InnerPlayerInfo> Players => _allPlayers;

        public InnerPlayerInfo? GetPlayerById(byte id)
        {
            if (id == byte.MaxValue)
            {
                return null;
            }

            return _allPlayers.TryGetValue(id, out var player) ? player : null;
        }

        public override ValueTask HandleRpc(ClientPlayer sender, ClientPlayer? target, byte call, IMessageReader reader)
        {
            switch ((RpcCalls)call)
            {
                case RpcCalls.SetTasks:
                {
                    if (!sender.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetTasks)} but was not a host");
                    }

                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetTasks)} to a specific player instead of broadcast");
                    }

                    var playerId = reader.ReadByte();
                    var taskTypeIds = reader.ReadBytesAndSize();

                    SetTasks(playerId, taskTypeIds);
                    break;
                }

                case RpcCalls.UpdateGameData:
                {
                    if (!sender.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetTasks)} but was not a host");
                    }

                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetTasks)} to a specific player instead of broadcast");
                    }

                    while (reader.Position < reader.Length)
                    {
                        using var message = reader.ReadMessage();
                        var player = GetPlayerById(message.Tag);
                        if (player != null)
                        {
                            player.Deserialize(message);
                        }
                        else
                        {
                            var playerInfo = new InnerPlayerInfo(message.Tag);

                            playerInfo.Deserialize(reader);

                            if (!_allPlayers.TryAdd(playerInfo.PlayerId, playerInfo))
                            {
                                throw new ImpostorException("Failed to add player to InnerGameData.");
                            }
                        }
                    }

                    break;
                }

                default:
                {
                    _logger.LogWarning("{0}: Unknown rpc call {1}", nameof(InnerGameData), call);
                    break;
                }
            }

            return default;
        }

        public override bool Serialize(IMessageWriter writer, bool initialState)
        {
            throw new NotImplementedException();
        }

        public override void Deserialize(IClientPlayer sender, IClientPlayer? target, IMessageReader reader, bool initialState)
        {
            if (!sender.IsHost)
            {
                throw new ImpostorCheatException($"Client attempted to send data for {nameof(InnerGameData)} as non-host");
            }

            if (initialState)
            {
                int num = reader.ReadPackedInt32();
                for (int i = 0; i < num; i++)
                {
                    InnerPlayerInfo playerInfo = new InnerPlayerInfo(reader.ReadByte());
                    playerInfo.Deserialize(reader);
                    try
                    {
                        _allPlayers.GetOrAdd(playerInfo.PlayerId, playerInfo);
                    }
                    catch (Exception ex) { _logger.LogInformation(ex.ToString()); }
                }
            }
            else
            {
                byte b = reader.ReadByte();
                for (int j = 0; j < b; j++)
                {
                    byte b2 = reader.ReadByte();
                    InnerPlayerInfo? playerById = GetPlayerById(b2);
                    if (playerById != null)
                    {
                        playerById.Deserialize(reader);
                        continue;
                    }
                    playerById = new InnerPlayerInfo(b2);
                    playerById.Deserialize(reader);
                    try
                    {
                        _allPlayers.GetOrAdd(playerById.PlayerId, playerById);
                    }
                    catch (Exception ex) { _logger.LogInformation(ex.ToString()); }
                }
            }
        }

        internal void AddPlayer(InnerPlayerControl control)
        {
            var playerId = control.PlayerId;
            var playerInfo = new InnerPlayerInfo(control.PlayerId);

            if (_allPlayers.TryAdd(playerId, playerInfo))
            {
                control.PlayerInfo = playerInfo;
            }
        }

        private void SetTasks(byte playerId, ReadOnlyMemory<byte> taskTypeIds)
        {
            var player = GetPlayerById(playerId);
            if (player == null)
            {
                _logger.LogTrace("Could not set tasks for playerId {0}.", playerId);
                return;
            }

            if (player.Disconnected)
            {
                return;
            }

            player.Tasks = new List<TaskInfo>(taskTypeIds.Length);

            foreach (var taskId in taskTypeIds.ToArray())
            {
                player.Tasks.Add(new TaskInfo
                {
                    Id = taskId,
                });
            }
        }
    }
}
