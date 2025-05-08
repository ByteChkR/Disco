using Newtonsoft.Json.Linq;

namespace Disco.Core.Tasks;

public abstract class DiscoTaskRunner
{
    protected DiscoTaskRunner(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public abstract Task<JToken> ExecuteAsync(DiscoTask task, DiscoContext context);
}