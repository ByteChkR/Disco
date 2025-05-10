using Disco.Core.Queue;
using Disco.Core.Tasks;
using Newtonsoft.Json.Linq;

namespace Disco.Core;

public static class DiscoExtensions
{
    public static Task<DiscoResult> EnqueueAndWait(this IDiscoTaskQueue queue, int waitTimeout, CancellationToken cancellationToken, string taskRunnerName,
                                                         int priority, JToken data, params string[] additionalCapabilities)
    {
        return queue.EnqueueAndWait(waitTimeout, Guid.NewGuid(), cancellationToken, taskRunnerName, priority, data, additionalCapabilities);
    }
    public static async Task<DiscoResult> EnqueueAndWait(this IDiscoTaskQueue queue, int waitTimeout, Guid id, CancellationToken cancellationToken, string taskRunnerName,
                                                         int priority, JToken data, params string[] additionalCapabilities)
    {
        var taskId = await queue.Enqueue(id, taskRunnerName, priority, data, additionalCapabilities).ConfigureAwait(false);
        
        DiscoResult? result = null;
        while (result == null && !cancellationToken.IsCancellationRequested)
        {
            result = await queue.TryGetResult(taskId).ConfigureAwait(false);
            if (result != null)
                return result;
            await Task.Delay(waitTimeout, cancellationToken).ConfigureAwait(false);
        }
        
        throw new OperationCanceledException("Task was cancelled", cancellationToken);
    }
}