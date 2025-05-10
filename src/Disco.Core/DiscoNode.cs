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
    private readonly Func<DiscoWorkerInfo, IDiscoTaskQueue , string[], DiscoWorker> _workerFactory;
    public readonly List<DiscoWorker> Workers = new();
    private readonly CancellationTokenSource _cts = new();
    private bool _initialized = false;
    public string Name { get; }
    public int WorkerCount { get; }
    public Func<int, IDiscoTaskQueue> QueueFactory { get; }
    public string[] Capabilities { get; }
    private readonly List<DiscoRunnerRegistration> _runners = new();
    public IDiscoTaskQueue Queue { get; }

    private DiscoNodeConfig? _config;
    public static DiscoNode FromConfig(DiscoNodeConfig config, Func<int, IDiscoTaskQueue>? queue, Func<DiscoWorkerInfo, IDiscoTaskQueue, string[], DiscoWorker>? workerFactory)
    {
        DiscoNode node = new DiscoNode(config.Name, config.Workers.Sum(x => x.Replicas), queue, workerFactory, config.Capabilities);
        node._config = config;
        return node;
    }

    public static DiscoNode FromJson(string json, Func<int, IDiscoTaskQueue>? queue, Func<DiscoWorkerInfo, IDiscoTaskQueue, string[], DiscoWorker>? workerFactory)
    {
        return FromConfig(JsonConvert.DeserializeObject<DiscoNodeConfig>(json)!, queue, workerFactory);
    }

    public static DiscoNode FromFile(string path, Func<int, IDiscoTaskQueue>? queue, Func<DiscoWorkerInfo, IDiscoTaskQueue, string[], DiscoWorker>? workerFactory)
    {
        return FromJson(File.ReadAllText(path), queue, workerFactory);
    }
    
    public DiscoNode(string name, int workerCount, Func<int, IDiscoTaskQueue>? queueFactory, Func<DiscoWorkerInfo, IDiscoTaskQueue, string[], DiscoWorker>? workerFactory, params string[] additionalCapabilities)
    {
        Name = name;
        WorkerCount = workerCount;
        QueueFactory = queueFactory ?? (i => new DiscoLocalTaskQueue());
        _workerFactory = workerFactory ?? ((info, queue, capabilities) => new DiscoWorker(info, queue, capabilities));
        Capabilities = additionalCapabilities;
        Queue = QueueFactory(-1);
    }
    
    public DiscoNode AddRunner(DiscoRunnerRegistration runner)
    {
        _runners.Add(runner);
        return this;
    }

    public DiscoNode AddRunner<T>(Func<IDiscoTaskRunner>? factory = null) =>
        AddRunner(new DiscoRunnerRegistration { DiscoTaskRunnerFactory = factory, DiscoTaskType = typeof(T) });

    public void InitNode()
    {
        if (_initialized)
            return;
        _initialized = true;
        if (_config != null)
        {
            foreach (var workerConfig in _config.Workers)
            {
                for (int i = 0; i < workerConfig.Replicas; i++)
                {
                    var workerInfo = new DiscoWorkerInfo(Guid.NewGuid(), Name + "-" + workerConfig.Name + +i);
                    var worker = _workerFactory(workerInfo, QueueFactory(i), workerConfig.Capabilities.Concat(Capabilities).ToArray())
                        .AddRunner(_runners);
                    Workers.Add(worker);
                }
            }
        }
        else
        {
            for (int i = 0; i < WorkerCount; i++)
            {
                var workerInfo = new DiscoWorkerInfo(Guid.NewGuid(), Name + "-" + i);
                var worker = _workerFactory(workerInfo, QueueFactory(i), Capabilities)
                    .AddRunner(_runners);
                Workers.Add(worker);
            }
        }
    }

    public async Task Run()
    {
        List<Task> tasks = new();
        
        InitNode();

        foreach (var worker in Workers)
        {
            Task t = Task.Run(async delegate { await worker.Run(_cts.Token).ConfigureAwait(false); });
            tasks.Add(t);
        }
        
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public void Stop()
    {
        _cts.Cancel();
    }

    public async Task StopOnIdle()
    {
        await WaitForIdle().ConfigureAwait(false);
        Stop();
    }

    public async Task WaitForIdle()
    {
        while(Workers.Any(x=>!x.IsIdle) || !await Queue.IsEmpty().ConfigureAwait(false))
            await Task.Delay(100).ConfigureAwait(false);
    }

    public void GracefulStop()
    {
        // check all workers and remove only those that are idle
        foreach (var worker in Workers)
        {
            if(!worker.IsStopping)
            {
                worker.GracefulStop();
            }
        }
    }
}