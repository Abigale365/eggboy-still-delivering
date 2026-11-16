using System;
using System.Text;

internal sealed class EasyInputReportDecoder
{
    static readonly string[] Actions = {
        "s1", "s2", "s3", "s4", "s5", "s6", "s7", "s8",
        "dial-left", "dial-right", "dial-press"
    };
    const string Prefix = "c0ffee00-0000-4000-8000-00000000000";
    bool functionHeld;
    bool functionUsed;

    public void Reset()
    {
        functionHeld = functionUsed = false;
    }

    public string Decode(byte[] report, int count)
    {
        if (report == null || count < 5 || count > report.Length ||
            report[0] != 0x11 || report[2] != 0 || report[3] != 1)
            return null;
        int length = report[4];
        if (length > count - 5) return null;

        if (report[1] == 2 && (length == 3 || length == 9))
        {
            string key = Encoding.ASCII.GetString(report, 6, length - 1);
            if (!key.Equals("fn", StringComparison.OrdinalIgnoreCase) &&
                !key.Equals("function", StringComparison.OrdinalIgnoreCase))
                return null;
            if (report[5] == 1 && !functionHeld)
            {
                functionHeld = true;
                functionUsed = false;
            }
            if (report[5] != 2) return null;
            bool tap = functionHeld && !functionUsed;
            Reset();
            return tap ? "dial-press" : null;
        }

        if (report[1] != 5 || length != 36) return null;
        string uuid = Encoding.ASCII.GetString(report, 5, length);
        if (!uuid.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return null;
        int index = "123456789ab".IndexOf(char.ToLowerInvariant(uuid[35]));
        if (index < 0) return null;
        if (!functionHeld) return Actions[index];
        if (index < 10) functionUsed = true;
        return index < 8 ? "combo-" + Actions[index] : Actions[index];
    }
}
