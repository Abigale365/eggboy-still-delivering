public sealed class ControlGate
{
	private object holder;

	public bool Locked => holder != null;

	public object Acquire(string who)
	{
		if (holder != null)
		{
			return null;
		}
		holder = new object();
		return holder;
	}

	public void Release(object token)
	{
		if (token != null && token == holder)
		{
			holder = null;
		}
	}

	public void ReleaseForFilming()
	{
		holder = null;
	}
}
