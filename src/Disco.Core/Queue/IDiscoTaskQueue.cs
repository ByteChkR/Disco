using Disco.Core.Tasks;
using Disco.Core.Worker;
using Newtonsoft.Json.Linq;

namespace Disco.Core.Queue;

public interface IDiscoTaskQueue
{
    bool IsEmpty { get; }
    Task<DiscoTask> WaitForTask(DiscoWorkerCapabilities capabilities, CancellationToken cancellationToken);
    Task Enqueue(string taskRunnerName, int priority, JToken data, params string[] additionalCapabilities);
    Task SubmitResult(DiscoTask task, DiscoResult result);
    Task<DiscoResult?> TryGetResult(Guid taskId);
    Task<DiscoResult> GetResult(Guid taskId, CancellationToken token);
}