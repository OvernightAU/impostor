using System;
using System.Collections.Generic;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Net.Messages;

namespace Impostor.Server.Net.Inner.Objects
{
    internal partial class InnerPlayerInfo
    {
        public InnerPlayerInfo(byte playerId)
        {
            PlayerId = playerId;
        }

        public InnerPlayerControl Controller { get; internal set; }

        public byte PlayerId { get; }

        public string PlayerName { get; internal set; }

        public byte ColorId { get; internal set; }

        public string HatId { get; internal set; }

        public string PetId { get; internal set; }

        public string SkinId { get; internal set; }

        public bool Disconnected { get; internal set; }

        public string RoleName { get; internal set; }

        public HashSet<string> EnabledMods { get; internal set; }

        public bool IsDead { get; internal set; }

        public DeathReason LastDeathReason { get; internal set; }

        public List<InnerGameData.TaskInfo> Tasks { get; internal set; }

        public DateTimeOffset LastMurder { get; set; }

        public void Serialize(IMessageWriter writer)
        {
            throw new NotImplementedException();
        }

        public void Deserialize(IMessageReader reader)
        {
            try
            {
                PlayerName = reader.ReadString();
                ColorId = reader.ReadByte();
                HatId = reader.ReadString();
                PetId = reader.ReadString();
                SkinId = reader.ReadString();
                var flag = reader.ReadByte();
                Disconnected = (flag & 1) != 0;
                IsDead = (flag & 4) != 0;
                var taskCount = reader.ReadByte();
                for (var i = 0; i < taskCount; i++)
                {
                    Tasks[i] ??= new InnerGameData.TaskInfo();
                    Tasks[i].Deserialize(reader);
                }
            }
            catch
            {
            }
        }
    }
}
