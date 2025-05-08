using ewu.trace;

namespace Disco.Core.Tasks;

public class DiscoContext
{
    public DiscoContext(ITraceSession session)
    {
        Session = session;
    }

    public ITraceSession Session { get; }
}