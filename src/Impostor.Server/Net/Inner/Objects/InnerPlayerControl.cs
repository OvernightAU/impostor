using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Impostor.Api;
using Impostor.Api.Events.Managers;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Messages;
using Impostor.Server.Events.Player;
using Impostor.Server.Net.Inner.Objects.Components;
using Impostor.Server.Net.Manager;
using Impostor.Server.Net.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using static Impostor.Server.Net.Inner.Objects.InnerPlayerControl;

namespace Impostor.Server.Net.Inner.Objects
{
    internal partial class InnerPlayerControl : InnerNetObject
    {
        private readonly ILogger<InnerPlayerControl> _logger;
        private readonly IEventManager _eventManager;
        private readonly Game _game;

        public enum RpcCalls : byte
        {
            PlayAnimation = 0,
            CompleteTask = 1,
            SyncSettings = 2,
            SetInfected = 3,
            Exiled = 4,
            CheckName = 5,
            SetName = 6,
            CheckColor = 7,
            SetColor = 8,
            SetHat = 9,
            SetSkin = 10,
            ReportDeadBody = 11,
            MurderPlayer = 12,
            SendChat = 13,
            TimesImpostor = 14,
            StartMeeting = 15,
            SetScanner = 16,
            SendChatNote = 17,
            SetPet = 18,
            SetStartCounter = 19,
            SetPlayerScale = 20,
            SetRole = 21,
            StartGame = 22,
            CheckMurder = 23,
            SetCooldown = 24,
            RoleRpc = 25,
            SyncRoleSettings = 26,
            ServerMods = 27,
            SyncRoleOption = 28,
        }

        public InnerPlayerControl(ILogger<InnerPlayerControl> logger, IServiceProvider serviceProvider, IEventManager eventManager, Game game)
        {
            _logger = logger;
            _eventManager = eventManager;
            _game = game;

            Physics = ActivatorUtilities.CreateInstance<InnerPlayerPhysics>(serviceProvider, this, _eventManager, _game);
            NetworkTransform = ActivatorUtilities.CreateInstance<InnerCustomNetworkTransform>(serviceProvider, this, _game);

            Components.Add(this);
            Components.Add(Physics);
            Components.Add(NetworkTransform);

            PlayerId = byte.MaxValue;
        }

        public bool IsNew { get; private set; }

        public byte PlayerId { get; private set; }

        public InnerPlayerPhysics Physics { get; }

        public InnerCustomNetworkTransform NetworkTransform { get; }

        public InnerPlayerInfo PlayerInfo { get; internal set; }

        public override async ValueTask HandleRpc(ClientPlayer sender, ClientPlayer? target, byte call, IMessageReader reader)
        {
            if (!sender.IsOwner(this) && !sender.IsHost)
            {
                throw new ImpostorCheatException($"Client sent {(RpcCalls)call} to unowned {PlayerInfo?.PlayerName} and is not host");
            }

            _logger.LogWarning($"Handling RPC {(RpcCalls)call} ({(int)call}) in {sender.Client.Name}");

            switch ((RpcCalls)call)
            {
                // Play an animation.
                case RpcCalls.PlayAnimation:
                {
                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {(RpcCalls)call} to a specific player instead of broadcast");
                    }

                    var animation = reader.ReadByte();
                    break;
                }

                // Complete a task.
                case RpcCalls.CompleteTask:
                {
                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {(RpcCalls)call} to a specific player instead of broadcast");
                    }

                    var taskId = reader.ReadPackedUInt32();
                    var task = PlayerInfo.Tasks[(int)taskId];
                    if (task == null)
                    {
                        _logger.LogWarning($"Client sent {nameof(RpcCalls.CompleteTask)} with a taskIndex that is not in their {nameof(InnerPlayerInfo)}");
                    }
                    else
                    {
                        task.Complete = true;
                        await _eventManager.CallAsync(new PlayerCompletedTaskEvent(_game, sender, this, task));
                    }

                    break;
                }

                // Update GameOptions.
                case RpcCalls.SyncSettings:
                {
                    if (!sender.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {(RpcCalls)call} but was not a host");
                    }

                    _game.Options.Deserialize(reader.ReadBytesAndSize());

                    break;
                }

                // Set Impostors. But Unused
                case RpcCalls.SetInfected:
                {
                    break;
                }

                case RpcCalls.SetRole:
                {
                        if (!sender.IsHost)
                        {
                            throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetRole)} but was not a host");
                        }

                        var roleName = reader.ReadString();
                        _logger.LogInformation($"{PlayerInfo.PlayerName} role was set to {roleName}");
                        PlayerInfo.RoleName = roleName;

                        break;
                }

                case RpcCalls.StartGame:
                {
                        if (!sender.IsHost)
                        {
                            throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.StartGame)} but was not a host");
                        }

                        _logger.LogInformation($"Role cutscene started for {_game.Code.Code}");

                        if (_game.GameState == GameStates.Starting)
                        {
                            await _game.StartedAsync();
                        }

                        break;
                }

                case RpcCalls.SyncRoleSettings:
                {
                        if (!sender.IsHost)
                        {
                            throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SyncRoleSettings)} but was not a host");
                        }

                        break;
                }

                case RpcCalls.SetPlayerScale:
                {
                        if (!sender.IsHost)
                        {
                            throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetPlayerScale)} but was not a host");
                        }

                        break;
                }

                case RpcCalls.RoleRpc:
                {
                        var rpcId = reader.ReadInt32();
                        var filteredRoleName = Regex.Replace(PlayerInfo.RoleName, "Role", string.Empty);

                        _logger.LogInformation($"{PlayerInfo.PlayerName} Sent {filteredRoleName}RPC ({rpcId})");

                        break;
                }

                case RpcCalls.ServerMods:
                {
                        var modsJson = reader.ReadString();

                        PlayerInfo.EnabledMods = JsonConvert.DeserializeObject<List<string>>(modsJson) ?? new List<string> { "nomods" };

                        // This rpc used to be handled by server, but its better to be handled by host
                        break;
                }

                case RpcCalls.SyncRoleOption:
                {
                        if (!sender.IsHost)
                        {
                            throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SyncRoleSettings)} but was not a host");
                        }

                        break;
                }

                // Player was voted out.
                case RpcCalls.Exiled:
                {
                    if (!sender.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.Exiled)} but was not a host");
                    }

                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.Exiled)} to a specific player instead of broadcast");
                    }

                    // TODO: Not hit?
                    Die(DeathReason.Exile);

                    await _eventManager.CallAsync(new PlayerExileEvent(_game, sender, this));
                    break;
                }

                // Validates the player name at the host.
                case RpcCalls.CheckName:
                {
                    if (target == null || !target.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.CheckName)} to the wrong player");
                    }

                    var name = reader.ReadString();
                    break;
                }

                // Update the name of a player.
                case RpcCalls.SetName:
                {
                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetName)} to a specific player instead of broadcast");
                    }

                    PlayerInfo.PlayerName = reader.ReadString();
                    break;
                }

                // Validates the color at the host.
                case RpcCalls.CheckColor:
                {
                    if (target == null || !target.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.CheckColor)} to the wrong player");
                    }

                    var color = reader.ReadByte();
                    break;
                }

                // Update the color of a player.
                case RpcCalls.SetColor:
                {
                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {(RpcCalls)call} to a specific player instead of broadcast");
                    }

                    var colorId = reader.ReadByte();

                    PlayerInfo.ColorId = colorId;

                    break;
                }

                // Update the hat of a player.
                case RpcCalls.SetHat:
                {
                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetHat)} to a specific player instead of broadcast");
                    }

                    PlayerInfo.HatId = reader.ReadString();
                    break;
                }

                case RpcCalls.SetSkin:
                {
                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {(RpcCalls)call} to a specific player instead of broadcast");
                    }

                    PlayerInfo.SkinId = reader.ReadString();
                    break;
                }

                // TODO: (ANTICHEAT) Location check?
                // only called by a non-host player on to start meeting
                case RpcCalls.ReportDeadBody:
                {
                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {(RpcCalls)call} to a specific player instead of broadcast");
                    }


                    var deadBodyPlayerId = reader.ReadByte();
                    // deadBodyPlayerId == byte.MaxValue -- means emergency call by button

                    break;
                }

                // TODO: (ANTICHEAT) Cooldown check?
                case RpcCalls.MurderPlayer:
                {
                    if (!sender.IsHost && _game.Host.Client.VersionSupported)
                    {
                        throw new ImpostorCheatException($"Client sent {(RpcCalls)call} but is not host");
                    }

                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {(RpcCalls)call} to a specific player instead of broadcast");
                    }

                    var player = reader.ReadNetObject<InnerPlayerControl>(_game);
                    if (!player.PlayerInfo.IsDead)
                    {
                        player.Die(DeathReason.Kill);
                        _logger.LogInformation("{0} was murdered by {1}", player.PlayerInfo.PlayerName, sender.Client.Name);
                        await _eventManager.CallAsync(new PlayerMurderEvent(_game, sender, this, player));
                    }

                    break;
                }

                // TODO: (ANTICHEAT) If Reason is hacking, ban player?
                case RpcCalls.CheckMurder:
                {
                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {(RpcCalls)call} to a specific player instead of broadcast");
                    }

                    break;
                }

                case RpcCalls.SetCooldown:
                {
                    if (!sender.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {(RpcCalls)call} while not being host");
                    }

                    break;
                }

                case RpcCalls.SendChat:
                {
                    // SendChat should still have ownership checks for obvious reasons
                    if (!sender.IsOwner(this))
                    {
                        throw new ImpostorCheatException($"Client sent {(RpcCalls)call} to an unowned {nameof(InnerPlayerControl)}");
                    }

                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {(RpcCalls)call} to a specific player instead of broadcast");
                    }

                    var chat = reader.ReadString();

                    _logger.LogInformation("{0} Sent: {1}", sender.Client.Name, chat);

                    await _eventManager.CallAsync(new PlayerChatEvent(_game, sender, this, chat));
                    break;
                }

                case RpcCalls.StartMeeting:
                {
                    if (!sender.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.StartMeeting)} but was not a host");
                    }

                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.StartMeeting)} to a specific player instead of broadcast");
                    }

                    // deadBodyPlayerId == byte.MaxValue -- means emergency call by button
                    var deadBodyPlayerId = reader.ReadByte();
                    var deadPlayer = deadBodyPlayerId != byte.MaxValue
                        ? _game.GameNet.GameData.GetPlayerById(deadBodyPlayerId)?.Controller
                        : null;

                    await _eventManager.CallAsync(new PlayerStartMeetingEvent(_game, _game.GetClientPlayer(this.OwnerId), this, deadPlayer));
                    break;
                }

                case RpcCalls.SetScanner:
                {
                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetScanner)} to a specific player instead of broadcast");
                    }

                    /*
                    if (_game.GameState != GameStates.Started)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetScanner)} while game is not started");
                    }
                    */

                    var on = reader.ReadBoolean();
                    var count = reader.ReadByte();
                    break;
                }

                case RpcCalls.SendChatNote:
                {
                    if (!sender.IsHost || !sender.IsOwner(this))
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SendChatNote)} to an unowned {nameof(InnerPlayerControl)}");
                    }

                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SendChatNote)} to a specific player instead of broadcast");
                    }

                    var playerId = reader.ReadByte();
                    var chatNote = (ChatNoteType)reader.ReadByte();

                    if (!Enum.IsDefined(typeof(ChatNoteType), chatNote))
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SendChatNote)} with a invalid chat note type");
                    }

                    break;
                }

                case RpcCalls.SetPet:
                {
                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetPet)} to a specific player instead of broadcast");
                    }

                    PlayerInfo.PetId = reader.ReadString();
                    break;
                }

                case (RpcCalls)81:
                {
                    if (!sender.IsHost)
                    {
                        throw new ImpostorCheatException($"Client tried to spawn a map without authorization.");
                    }

                    break;
                }

                case RpcCalls.SetStartCounter:
                {
                    /*
                    if (!sender.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetStartCounter)} but is not host");
                    }
                    */

                    if (target != null)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetStartCounter)} to a specific player instead of broadcast");
                    }

                    // Used to compare with LastStartCounter.
                    var startCounter = reader.ReadPackedUInt32();

                    // Is either start countdown or byte.MaxValue
                    var secondsLeft = reader.ReadByte();
                    if (secondsLeft < byte.MaxValue)
                    {
                        await _eventManager.CallAsync(new PlayerSetStartCounterEvent(_game, sender, this, secondsLeft));
                    }

                    break;
                }

                default:
                {
                    _logger.LogWarning("{0}: Unknown rpc call {1}", nameof(InnerPlayerControl), call);
                    break;
                }
            }
        }

        public override bool Serialize(IMessageWriter writer, bool initialState)
        {
            throw new NotImplementedException();
        }

        public override void Deserialize(IClientPlayer sender, IClientPlayer? target, IMessageReader reader, bool initialState)
        {
            if (!sender.IsHost)
            {
                throw new ImpostorCheatException($"Client attempted to send data for {nameof(InnerPlayerControl)} as non-host");
            }

            if (initialState)
            {
                IsNew = reader.ReadBoolean();
            }

            PlayerId = reader.ReadByte();
        }

        internal void Die(DeathReason reason)
        {
            PlayerInfo.IsDead = true;
            PlayerInfo.LastDeathReason = reason;
        }
    }
}
