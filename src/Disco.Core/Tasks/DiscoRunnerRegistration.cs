namespace Disco.Core.Tasks;

public class DiscoRunnerRegistration
{
    public Func<IDiscoTaskRunner>? DiscoTaskRunnerFactory { get; set; }
    public Type DiscoTaskType { get; set; }
}