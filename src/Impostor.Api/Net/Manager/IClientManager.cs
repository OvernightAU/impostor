using System.Collections.Generic;
using System.Linq;

namespace Impostor.Api.Net.Manager
{
    public interface IClientManager
    {
        IEnumerable<IClient> Clients { get; }

        public IClient? GetClientById(int clientId)
        {
            return Clients.FirstOrDefault(c => c.Id == clientId);
        }
    }
}
