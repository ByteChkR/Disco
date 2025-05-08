using Disco.Core.Queue;
using Disco.Core.Tasks;
using Newtonsoft.Json.Linq;

namespace Disco.Core;

public static class DiscoExtensions
{
    public static async Task<DiscoResult> EnqueueAndWait(this IDiscoTaskQueue queue, CancellationToken cancellationToken, string taskRunnerName,
        int priority, JToken data, params string[] additionalCapabilities)
    {
        var taskId = await queue.Enqueue(taskRunnerName, priority, data, additionalCapabilities);
        return await queue.GetResult(taskId, cancellationToken);
    }
}