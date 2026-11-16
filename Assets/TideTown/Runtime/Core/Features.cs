// WP-07 预留开关：扫描默认关闭；调试界面默认关闭。WP-09：-scan 命令行可以临时打开扫描站。
// -debugui 命令行打开调试界面（F3 碰撞线框等）；发布包里玩家碰不到。
public static class Features
{
    public static bool scan = HasArg("-scan");
    public static bool debugUi = HasArg("-debugui");
    // 主线自动审计（剧情 / 路线 / 水位 / 地图 / 扫描）：走经典路线，渡口木箱全部出现。创新审计 -innovaudit 不算，它要测真人玩法
    public static bool classicAudit = HasArg("-storyaudit") ||
        System.Array.Exists(System.Environment.GetCommandLineArgs(), GameBootstrap.IsRouteAuditFlag)
        || HasArg("-wateraudit") || HasArg("-mapaudit") || HasArg("-scanaudit") || HasArg("-shot");
    // 渡口缺口（创新方向的多解过海）：-nogap 可以关掉，退回"木箱自动铺满"
    public static bool crossingGap = !HasArg("-nogap");
    // v3 的透视拓印 / 光影声音旧版入口（重做前默认关）
    public static bool oldModes = HasArg("-oldmodes");
    // 2026-11-16 自由漫游（用户议题 2）：主角不再被可走区多边形白名单挡住，只看真实地形高低、碰撞、水和少量禁区。
    // -whitelist 退回旧的白名单规则（出问题时的后路）
    public static bool freeRoam = !HasArg("-whitelist");

    static bool HasArg(string key)
    {
        foreach (var a in System.Environment.GetCommandLineArgs()) if (a == key) return true;
        return false;
    }
}
