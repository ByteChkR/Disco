using System.Collections.Concurrent;
using Disco.Core.Tasks;
using Disco.Core.Worker;
using Newtonsoft.Json.Linq;

namespace Disco.Core.Queue;

public class DiscoLocalTaskQueue : IDiscoTaskQueue
{
    private readonly PriorityQueue<DiscoTask, DiscoTask> _queue = new();
    private readonly ConcurrentDictionary<Guid, DiscoResult> _tasks = new();
    private SemaphoreSlim _semaphore = new(1, 1);

    public Task<bool> IsEmpty()
    {
        return Task.FromResult(_queue.Count == 0);
    }

    public async Task<DiscoTask> WaitForTask(DiscoWorkerCapabilities capabilities, CancellationToken cancellationToken)
    {
        while(!cancellationToken.IsCancellationRequested)
        {
            await _semaphore.WaitAsync(cancellationToken);
            var task = await TryWaitForTask(capabilities, cancellationToken);
            if (task != null) return task;
            await Task.Delay(100, cancellationToken);
        }
        
        throw new OperationCanceledException("Task queue was cancelled");
    }

    public async Task<DiscoTask?> TryWaitForTask(DiscoWorkerCapabilities capabilities, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        DiscoTask? task = null;
        if (_queue.Count != 0)
        {
            List<DiscoTask> tasks = new();
            while (_queue.Count != 0)
            {
                var t = _queue.Dequeue();
                if (t.CanRunOn(capabilities))
                {
                    task = t;
                    break;
                }
                    
                tasks.Add(t);
            }
                
            foreach (var t in tasks)
            {
                _queue.Enqueue(t, t);
            }
        }
        _semaphore.Release();
        
        return task;
    }

    public Task<Guid> Enqueue(string taskRunnerName, int priority, JToken data, params string[] additionalCapabilities)
    {
        var t = new DiscoTask(Guid.NewGuid(), taskRunnerName, data, priority, additionalCapabilities);
        _queue.Enqueue(t,t);
        return Task.FromResult(t.Id);
    }

    public Task<DiscoResult?> TryGetResult(Guid taskId)
    {
        return Task.FromResult(_tasks.GetValueOrDefault(taskId));
    }
    
    public async Task<DiscoResult> GetResult(Guid taskId, CancellationToken token)
    {
        while(!token.IsCancellationRequested)
        {
            if (_tasks.TryGetValue(taskId, out var result))
            {
                return result;
            }
            await Task.Delay(100, token);
        }
        
        throw new OperationCanceledException("Task queue was cancelled");
    }
    
    public Task SubmitResult(DiscoResult result)
    {
        if (_tasks.TryAdd(result.TaskId, result))
        {
            return Task.CompletedTask;
        }
        else
        {
            throw new InvalidOperationException($"Task {result.TaskId} already exists");
        }
    }
}