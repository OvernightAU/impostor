using Impostor.Api.Games;

namespace Impostor.Server.Net
{
    public class GameCodeFactory : IGameCodeFactory
    {
        public GameCode Create(int num)
        {
            return GameCode.Create(num);
        }
    }
}
