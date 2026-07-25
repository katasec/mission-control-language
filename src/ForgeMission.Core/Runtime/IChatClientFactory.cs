using ForgeMission.Core.Manifest;
using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Runtime;

public interface IChatClientFactory
{
    IChatClient Create(ProviderProfile profile);
}
