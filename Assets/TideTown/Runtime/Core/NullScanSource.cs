using System;

public sealed class NullScanSource : IScanSource
{
	public void Capture(Action<byte[]> done, Action<string> fail)
	{
		fail?.Invoke("暂未开放");
	}
}
