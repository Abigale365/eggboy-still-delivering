// 可见桌面启动审计；不更改游戏、不注入输入、不改任何断言。
using System;
using System.IO;
using System.Diagnostics;
public static class AuditDesktopLauncher
{
    [STAThread]
    public static int Main()
    {
        var folder = AppDomain.CurrentDomain.BaseDirectory;
        var lines = File.ReadAllLines(Path.Combine(folder, "audit-launch.args"));
        var start = new ProcessStartInfo(lines[0], lines[1]);
        start.UseShellExecute = true;
        start.WorkingDirectory = Path.GetDirectoryName(lines[0]);
        var process = Process.Start(start);
        if (!process.WaitForExit(600000)) { process.Kill(); return 2; }
        File.WriteAllText(Path.Combine(folder, "audit-launch.exit"), process.ExitCode.ToString());
        return process.ExitCode;
    }
}
