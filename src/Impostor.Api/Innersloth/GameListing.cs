using System;

namespace Impostor.Shared.Innersloth
{
    [Serializable]
    public struct GameListing
    {
        public string HostName;

        public int GameId;

        public string PlayerCountAndMax;

        public int ImpostorCount;
    }
}