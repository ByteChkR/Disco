using Disco.Core.Tasks;
using Disco.Core.Worker;
using Newtonsoft.Json.Linq;

namespace Disco.Core.Queue;

public interface IDiscoTaskQueue
{
    Task<bool> IsEmpty();
    Task<DiscoTask?> TryWaitForTask(DiscoWorkerCapabilities capabilities, CancellationToken cancellationToken);
    Task<Guid> Enqueue(Guid id, string taskRunnerName, int priority, JToken data, params string[] additionalCapabilities);
    Task SubmitResult(DiscoResult result);
    Task<DiscoResult?> TryGetResult(Guid taskId);
}


public static class DiscoTaskQueueExtensions
{
    public static Task<Guid> Enqueue(this IDiscoTaskQueue queue,
                                     string taskRunnerName,
                                     int priority,
                                     JToken data,
                                     params string[] additionalCapabilities)
    {
        return queue.Enqueue(Guid.NewGuid(), taskRunnerName, priority, data, additionalCapabilities);
    }
}