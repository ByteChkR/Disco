using Disco.Core.Queue;
using Disco.Core.Tasks;
using Disco.Core.Worker;
using Newtonsoft.Json;

namespace Disco.Core;

public class DiscoWorkerConfig
{
    public string Name { get; set; } = "DefaultWorker";
    public int Replicas { get; set; }
    public string[] Capabilities { get; set; } = [];
}
public class DiscoNodeConfig
{
    public string Name { get; set; } = "DefaultNode";
    public string[] Capabilities { get; set; } = [];
    public DiscoWorkerConfig[] Workers { get; set; } = [];
}



public class DiscoNode
{
    private readonly List<DiscoWorker> _workers = new();
    private readonly CancellationTokenSource _cts = new();
    public string Name { get; }
    public int WorkerCount { get; }
    public Func<int, IDiscoTaskQueue> QueueFactory { get; }
    public string[] Capabilities { get; }
    private readonly List<DiscoTaskRunner> _runners = new();
    public IDiscoTaskQueue Queue { get; }

    private DiscoNodeConfig? _config;
    public static DiscoNode FromConfig(DiscoNodeConfig config, Func<int, IDiscoTaskQueue> queue)
    {
        var node = new DiscoNode(config.Name, config.Workers.Sum(x=>x.Replicas), queue, config.Capabilities);
        node._config = config;
        return node;
    }

    public static DiscoNode FromJson(string json, Func<int, IDiscoTaskQueue> queue)
    {
        return FromConfig(JsonConvert.DeserializeObject<DiscoNodeConfig>(json)!, queue);
    }

    public static DiscoNode FromFile(string path, Func<int, IDiscoTaskQueue> queue)
    {
        return FromJson(File.ReadAllText(path), queue);
    }
    
    public DiscoNode(string name, int workerCount, Func<int, IDiscoTaskQueue> queueFactory, params string[] additionalCapabilities)
    {
        Name = name;
        WorkerCount = workerCount;
        QueueFactory = queueFactory;
        Capabilities = additionalCapabilities;
        Queue = QueueFactory(-1);
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

    public Task Run()
    {
        List<Task> tasks = new();
        if (_config != null)
        {
            foreach (var workerConfig in _config.Workers)
            {
                for (int i = 0; i < workerConfig.Replicas; i++)
                {
                    var workerInfo = new DiscoWorkerInfo(Guid.NewGuid(), Name + "-" + workerConfig.Name + +i);
                    var worker = new DiscoWorker(workerInfo, QueueFactory(i), workerConfig.Capabilities.Concat(Capabilities).ToArray())
                        .AddRunner(_runners);
                    _workers.Add(worker);
                    tasks.Add(worker.Run(_cts.Token));
                }
            }
        }
        else
        {
            for (int i = 0; i < WorkerCount; i++)
            {
                var workerInfo = new DiscoWorkerInfo(Guid.NewGuid(), Name + "-" + i);
                var worker = new DiscoWorker(workerInfo, QueueFactory(i), Capabilities)
                    .AddRunner(_runners);
                _workers.Add(worker);
                tasks.Add(worker.Run(_cts.Token));
            }
        }
        
        return Task.WhenAll(tasks);
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
        while(_workers.Any(x=>!x.IsIdle) || !await Queue.IsEmpty())
            await Task.Delay(100);
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