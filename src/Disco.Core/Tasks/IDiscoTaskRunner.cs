using Newtonsoft.Json.Linq;

namespace Disco.Core.Tasks;

public interface IDiscoTaskRunner
{
    Task<JToken> ExecuteAsync(DiscoTask task, DiscoContext context);
}