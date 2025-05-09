using Disco.Core.Tasks;

using ewu.trace;

using Newtonsoft.Json.Linq;

namespace Disco.Test.TaskTypes;

public class AddTask : DiscoTaskRunner
{
    public const string NAME = nameof(AddTask);

    public AddTask() : base(NAME) { }

    public override async Task<JToken> ExecuteAsync(DiscoTask task, DiscoContext context)
    {
        using ITraceScope scope = context.Session.CreateScope([Name]);
        AddArgs? args = task.Data.ToObject<AddArgs>();

        if (args == null)
        {
            throw new InvalidOperationException("Args is null");
        }

        return JToken.FromObject(args.A + args.B);
    }
}