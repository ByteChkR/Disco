using Disco.Core.Tasks;

using ewu.trace;

using Newtonsoft.Json.Linq;

namespace Disco.Test.TaskTypes;

public class AddArgs
{
    public int A { get; set; }
    public int B { get; set; }
}

public class ExecuteGraphArgs
{
    public Guid GraphId { get; set; }
    public JObject[] Inputs { get; set; } = [];
}

public class ExecuteGraphTask : DiscoTaskRunner
{
    public const string NAME = nameof(ExecuteGraphTask);

    public ExecuteGraphTask() : base(NAME) { }

    public override Task<JToken> ExecuteAsync(DiscoTask task, DiscoContext context)
    {
        using ITraceScope scope = context.Session.CreateScope([Name]);
        ExecuteGraphArgs? args = task.Data.ToObject<ExecuteGraphArgs>();

        if (args == null)
        {
            throw new InvalidOperationException("Args is null");
        }

        return Task.FromResult(JToken.FromObject(args.GraphId));
    }
}