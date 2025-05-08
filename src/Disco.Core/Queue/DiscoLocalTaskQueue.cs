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
    public bool IsEmpty => _queue.Count == 0;

    public async Task<DiscoTask> WaitForTask(DiscoWorkerCapabilities capabilities, CancellationToken cancellationToken)
    {
        while(!cancellationToken.IsCancellationRequested)
        {
            await _semaphore.WaitAsync(cancellationToken);
            if (_queue.Count != 0)
            {
                DiscoTask? task = null;
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
                
                if (task != null)
                {
                    _semaphore.Release();
                    return task;
                }
            }
            _semaphore.Release();
            await Task.Delay(100, cancellationToken);
        }
        
        throw new OperationCanceledException("Task queue was cancelled");
    }

    public Task Enqueue(string taskRunnerName, int priority, JToken data, params string[] additionalCapabilities)
    {
        var t = new DiscoTask(Guid.NewGuid(), taskRunnerName, data, priority, additionalCapabilities);
        _queue.Enqueue(t,t);
        return Task.CompletedTask;
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
    
    public Task SubmitResult(DiscoTask task, DiscoResult result)
    {
        if (_tasks.TryAdd(task.Id, result))
        {
            return Task.CompletedTask;
        }
        else
        {
            throw new InvalidOperationException($"Task {task.Id} already exists");
        }
    }
}