namespace Xemele.Streaming;

internal sealed partial class XmlStreamingPump
{
    internal void SetAsyncResources(bool enabled)
    {
        Enter();
        try
        {
            CheckUsable();
            if (terminal)
            {
                throw new InvalidOperationException("Pump is terminal.");
            }

            core.SetAsyncResources(enabled);
            asyncResources = enabled;
        }
        finally { Exit(); }
    }
}
