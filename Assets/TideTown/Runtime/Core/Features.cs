// WP-07 预留开关：扫描默认关闭；调试界面默认关闭。WP-09：-scan 命令行可以临时打开扫描站。
// -debugui 命令行打开调试界面（F3 碰撞线框等）；发布包里玩家碰不到。
public static class Features
{
    public static bool scan = HasArg("-scan");
    public static bool debugUi = HasArg("-debugui");
    // 主线自动审计（剧情 / 路线 / 水位 / 地图 / 扫描）：走经典路线，渡口木箱全部出现。创新审计 -innovaudit 不算，它要测真人玩法
    public static bool classicAudit = HasArg("-storyaudit") || HasArg("-routeaudit2") || HasArg("-routeaudit")
        || HasArg("-wateraudit") || HasArg("-mapaudit") || HasArg("-scanaudit") || HasArg("-shot");
    // 渡口缺口（创新方向的多解过海）：-nogap 可以关掉，退回"木箱自动铺满"
    public static bool crossingGap = !HasArg("-nogap");

    static bool HasArg(string key)
    {
        foreach (var a in System.Environment.GetCommandLineArgs()) if (a == key) return true;
        return false;
    }
}
