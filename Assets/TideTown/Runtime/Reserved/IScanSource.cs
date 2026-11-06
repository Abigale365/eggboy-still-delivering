// WP-09 预留：扫描来源（以后可能是摄像头、选文件或预置样例）。默认什么也不做，只回“暂未开放”。
using System;

public interface IScanSource
{
    void Capture(Action<byte[]> done, Action<string> fail);
}

public sealed class NullScanSource : IScanSource
{
    public void Capture(Action<byte[]> done, Action<string> fail)
    {
        if (fail != null) fail("暂未开放");
    }
}
