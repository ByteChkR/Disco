namespace Disco.Core.Worker;

public readonly struct DiscoWorkerInfo
{
    public DiscoWorkerInfo(Guid id, string? name = null)
    {
        Id = id;
        Name = name ?? $"Worker-{Convert.ToBase64String(id.ToByteArray())}";
    }

    public Guid Id { get; }
    public string Name { get; }
}