using Disco.Core.Tasks;
using Disco.Core.Worker;

using Newtonsoft.Json.Linq;

namespace Disco.Core.Queue;

public interface IDiscoTaskQueue
{
    Task<bool> IsEmpty();
    Task<DiscoTask> WaitForTask(DiscoWorkerCapabilities capabilities, CancellationToken cancellationToken);
    Task<DiscoTask?> TryWaitForTask(DiscoWorkerCapabilities capabilities, CancellationToken cancellationToken);
    Task<Guid> Enqueue(string taskRunnerName, int priority, JToken data, params string[] additionalCapabilities);
    Task SubmitResult(DiscoResult result);
    Task<DiscoResult?> TryGetResult(Guid taskId);
    Task<DiscoResult> GetResult(Guid taskId, CancellationToken token);
}