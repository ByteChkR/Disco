using Disco.Core.Tasks;

using ewu.trace;

using Newtonsoft.Json.Linq;

namespace Disco.Test.TaskTypes;

public class WaitForTask : DiscoTaskRunner
{
    public const string NAME = nameof(WaitForTask);

    public WaitForTask() : base(NAME) { }

    public override async Task<JToken> ExecuteAsync(DiscoTask task, DiscoContext context)
    {
        using ITraceScope scope = context.Session.CreateScope([Name]);
        WaitForArgs? args = task.Data.ToObject<WaitForArgs>();

        if (args == null)
        {
            throw new InvalidOperationException("Args is null");
        }

        scope.Info($"Waiting for {args.Delay}ms");
        await Task.Delay(args.Delay);

        return JToken.FromObject(new { task.Id, args.Delay });
    }
}