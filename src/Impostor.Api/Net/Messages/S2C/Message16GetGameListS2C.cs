using System.Collections.Generic;
using Newtonsoft.Json;
using Impostor.Api.Games;
using Impostor.Shared.Innersloth;

namespace Impostor.Api.Net.Messages.S2C
{
    public class Message16GetGameListS2C
    {
        public static void Serialize(IMessageWriter writer, IEnumerable<IGame> games)
        {
            writer.StartMessage(MessageFlags.GetGameListV2);


            List<GameListing> gameListings = new List<GameListing>();
            foreach (var game in games)
            {
                GameListing gameInfo = new GameListing();
                gameInfo.HostName = game.Host.Client.Name ?? "Unknown host";
                gameInfo.GameId = game.Code;
                gameInfo.PlayerCountAndMax = $"{game.PlayerCount}/{game.Options.MaxPlayers ?? 10}";
                gameInfo.ImpostorCount = game.Options.NumImpostors ?? 1;
                gameListings.Add(gameInfo);
            }
            writer.Write(JsonConvert.SerializeObject(gameListings, Formatting.Indented));

            writer.EndMessage();
        }
    }
}
