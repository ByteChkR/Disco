using Disco.Core.Tasks;

using ewu.trace;

using Newtonsoft.Json.Linq;

namespace Disco.Test.TaskTypes;

public class WaitForTask : IDiscoTaskRunner
{
    public async Task<JToken> ExecuteAsync(DiscoTask task, DiscoContext context)
    {
        using ITraceScope scope = context.Session.CreateScope([task.TaskRunnerName]);
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