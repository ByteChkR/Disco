using Disco.Core.Tasks;
using Disco.Core.Worker;

namespace Disco.Core.Queue;

public class DiscoNode
{
    private readonly List<DiscoWorker> _workers = new();
    private readonly CancellationTokenSource _cts = new();
    public string Name { get; }
    public int WorkerCount { get; }
    public IDiscoTaskQueue Queue { get; }
    public string[] Capabilities { get; }
    private readonly List<DiscoTaskRunner> _runners = new();
    public DiscoNode(string name, int workerCount, IDiscoTaskQueue queue, params string[] additionalCapabilities)
    {
        Name = name;
        WorkerCount = workerCount;
        Queue = queue;
        Capabilities = additionalCapabilities;
    }
    
    public DiscoNode AddRunner(DiscoTaskRunner runner)
    {
        _runners.Add(runner);
        return this;
    }
    public DiscoNode AddRunner<T>() where T : DiscoTaskRunner, new()
    {
        var runner = new T();
        _runners.Add(runner);
        return this;
    }

    public void Run()
    {
        for (int i = 0; i < WorkerCount; i++)
        {
            var workerInfo = new DiscoWorkerInfo(Guid.NewGuid(), Name + "-" + i);
            var worker = new DiscoWorker(workerInfo, Queue)
                .AddRunner(_runners);
            _workers.Add(worker);
            _ = worker.Run(_cts.Token);
        }
    }

    public void Stop()
    {
        _cts.Cancel();
    }

    public async Task StopOnIdle()
    {
        await WaitForIdle();
        Stop();
    }

    public async Task WaitForIdle()
    {
        while(_workers.Any(x=>!x.IsIdle) || !Queue.IsEmpty)await Task.Delay(100);
    }

    public void GracefulStop()
    {
        // check all workers and remove only those that are idle
        foreach (var worker in _workers)
        {
            if(!worker.IsStopping)
            {
                worker.GracefulStop();
            }
        }
    }
}