// WP-07 读信、地图、过场共用的锁。只释放自己拿到的锁。
public sealed class ControlGate
{
    object holder;

    public object Acquire(string who)
    {
        if (holder != null) return null;
        holder = new object();
        return holder;
    }

    public void Release(object token)
    {
        if (token != null && token == holder) holder = null;
    }

    public bool Locked => holder != null;
}
