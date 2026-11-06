# 给接手这个工程的 agent

先读 `docs/ENGINEERING_BOOK.md` 的第 0 节，再读 `docs/progress.md`，然后按工作循环继续。

三条最重要的规矩：

1. 首要目标是把游戏做出来。扫描和打印只留接口。
2. 没有用户这一次的明确同意，不调用会花积分的 Lux3D 接口。
3. 不要调用 `ExpandedTownBuilder`，它会覆盖场景。
