// WP-07 预留开关：扫描默认关闭；调试界面默认关闭。WP-09：-scan 命令行可以临时打开扫描站。
public static class Features
{
    public static bool scan = HasArg("-scan");
    public static bool debugUi = false;

    static bool HasArg(string key)
    {
        foreach (var a in System.Environment.GetCommandLineArgs()) if (a == key) return true;
        return false;
    }
}
