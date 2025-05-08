namespace Disco.Core.Worker;

public struct DiscoWorkerCapabilities
{
    public DiscoWorkerCapabilities(DiscoWorkerInfo workerInfo, string[] capabilities)
    {
        WorkerInfo = workerInfo;
        Capabilities = capabilities;
    }

    public DiscoWorkerInfo WorkerInfo { get; set; }
    public string[] Capabilities { get; set; }
}