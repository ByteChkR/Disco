using Disco.Core.Queue;
using Disco.Core.Tasks;
using Newtonsoft.Json.Linq;

namespace Disco.Core;

public static class DiscoExtensions
{
    public static async Task<DiscoResult> EnqueueAndWait(this IDiscoTaskQueue queue, CancellationToken cancellationToken, string taskRunnerName,
                                                         int priority, JToken data, params string[] additionalCapabilities)
    {
        var taskId = await queue.Enqueue(taskRunnerName, priority, data, additionalCapabilities).ConfigureAwait(false);
        return await queue.GetResult(taskId, cancellationToken).ConfigureAwait(false);
    }
    public static async Task<DiscoResult> EnqueueAndWait(this IDiscoTaskQueue queue, Guid id, CancellationToken cancellationToken, string taskRunnerName,
                                                         int priority, JToken data, params string[] additionalCapabilities)
    {
        var taskId = await queue.Enqueue(id, taskRunnerName, priority, data, additionalCapabilities).ConfigureAwait(false);
        return await queue.GetResult(taskId, cancellationToken).ConfigureAwait(false);
    }
}