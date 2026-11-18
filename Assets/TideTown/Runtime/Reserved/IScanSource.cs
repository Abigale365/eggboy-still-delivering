using System;

public interface IScanSource
{
	void Capture(Action<byte[]> done, Action<string> fail);
}
