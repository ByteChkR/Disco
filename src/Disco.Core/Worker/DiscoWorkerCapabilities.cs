namespace Disco.Core.Worker;

public readonly struct DiscoWorkerCapabilities
{
    public DiscoWorkerCapabilities(DiscoWorkerInfo workerInfo, string[] capabilities)
    {
        WorkerInfo = workerInfo;
        Capabilities = capabilities;
    }

    public DiscoWorkerInfo WorkerInfo { get; }
    public string[] Capabilities { get; }
}