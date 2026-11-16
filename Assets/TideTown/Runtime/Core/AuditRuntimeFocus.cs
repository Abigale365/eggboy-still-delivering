using UnityEngine;

// 自动化工具返回桌面后会切走焦点；只解除操作系统暂停，不改变时间倍率、状态或任何审计断言。
internal static class AuditRuntimeFocus
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void KeepRunning()
    {
        if (FilmingStageJumper.AuditArguments()) Application.runInBackground = true;
    }
}
