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
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly List<DiscoTaskRunner> _runners = new List<DiscoTaskRunner>();
    private readonly List<DiscoWorker> _workers = new List<DiscoWorker>();

    private DiscoNodeConfig? _config;

    public DiscoNode(string name,
                     int workerCount,
                     Func<int, IDiscoTaskQueue> queueFactory,
                     params string[] additionalCapabilities)
    {
        Name = name;
        WorkerCount = workerCount;
        QueueFactory = queueFactory;
        Capabilities = additionalCapabilities;
        Queue = QueueFactory(-1);
    }

    public string Name { get; }
    public int WorkerCount { get; }
    public Func<int, IDiscoTaskQueue> QueueFactory { get; }
    public string[] Capabilities { get; }
    public IDiscoTaskQueue Queue { get; }

    public static DiscoNode FromConfig(DiscoNodeConfig config, Func<int, IDiscoTaskQueue> queue)
    {
        DiscoNode node = new DiscoNode(config.Name, config.Workers.Sum(x => x.Replicas), queue, config.Capabilities);
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

    public DiscoNode AddRunner(DiscoTaskRunner runner)
    {
        _runners.Add(runner);

        return this;
    }

    public DiscoNode AddRunner<T>() where T : DiscoTaskRunner, new()
    {
        T runner = new T();
        _runners.Add(runner);

        return this;
    }

    public async Task Run()
    {
        List<Task> tasks = new List<Task>();

        if (_config != null)
        {
            foreach (DiscoWorkerConfig workerConfig in _config.Workers)
            {
                for (int i = 0; i < workerConfig.Replicas; i++)
                {
                    DiscoWorkerInfo workerInfo =
                        new DiscoWorkerInfo(Guid.NewGuid(), Name + "-" + workerConfig.Name + +i);

                    DiscoWorker worker = new DiscoWorker(workerInfo,
                                                         QueueFactory(i),
                                                         workerConfig.Capabilities.Concat(Capabilities)
                                                                     .ToArray()
                                                        )
                        .AddRunner(_runners);
                    _workers.Add(worker);
                    Task t = Task.Run(async () => await worker.Run(_cts.Token));
                    tasks.Add(t);
                }
            }
        }
        else
        {
            for (int i = 0; i < WorkerCount; i++)
            {
                DiscoWorkerInfo workerInfo = new DiscoWorkerInfo(Guid.NewGuid(), Name + "-" + i);

                DiscoWorker worker = new DiscoWorker(workerInfo, QueueFactory(i), Capabilities)
                    .AddRunner(_runners);
                _workers.Add(worker);
                Task t = Task.Run(async () => await worker.Run(_cts.Token));
                tasks.Add(t);
            }
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
        while (_workers.Any(x => !x.IsIdle) || !await Queue.IsEmpty())
        {
            await Task.Delay(100).ConfigureAwait(false);
        }
    }

    public void GracefulStop()
    {
        // check all workers and remove only those that are idle
        foreach (DiscoWorker worker in _workers)
        {
            if (!worker.IsStopping)
            {
                worker.GracefulStop();
            }
        }
    }
}