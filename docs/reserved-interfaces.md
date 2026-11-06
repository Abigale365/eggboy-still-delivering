# 预留接口（WP-09）

工程书 6.2 的接口已按最小实现写好。默认什么都不接：不联网、不读摄像头、不生成模型。

## 现在的状态

| 东西 | 文件 | 默认行为 |
| --- | --- | --- |
| `IModelProvider` / `BuiltinModelProvider` | `Assets/TideTown/Runtime/Reserved/IModelProvider.cs` | 先查引用表，再查 `Resources/Items/<itemId>`；找不到就 `fail("没有物品模型: …")` |
| `IScanSource` / `NullScanSource` | `Assets/TideTown/Runtime/Reserved/IScanSource.cs` | 直接 `fail("暂未开放")` |
| `ScanStation` | `Assets/TideTown/Runtime/Reserved/ScanStation.cs` | 邮局门口的小台子；`Features.scan=false` 时渲染器和碰撞体全关，也不注册到 `InteractionService` |
| `Features` | `Assets/TideTown/Runtime/Core/Features.cs` | `scan=false`；命令行加 `-scan` 可临时打开 |

`ScanStation` 已实现 `IInteractable`；`InteractionService` 现在按接口的 `Position` 找最近目标，不写死邮筒。

## 以后怎么接

### 换物品模型来源
1. 写一个 `IModelProvider` 实现（例如从 glTFast 导入、从网络下载到本地缓存）。
2. 在拿物品的地方换成新实现；玩法代码只依赖 `Load(itemId, done, fail)`。
3. 若只是加内置模型：`BuiltinModelProvider.Register("item_id", prefab)`，或把 prefab 放进 `Assets/TideTown/Resources/Items/`。
4. 注意 glTFast 的着色器要进 Always Included Shaders，否则 WebGL 会丢。

### 接真实扫描
1. 写一个 `IScanSource` 实现（选文件 / 摄像头 / 预置样例），`Capture` 成功回调 `byte[]`，失败回调中文原因。
2. 在 `ScanStation` 里替换 `source` 字段。
3. 把 `Features.scan` 打开（或保留 `-scan` 仅作调试）。
4. 扫描结果走 `IModelProvider` 加载模型；`ScanStation` 只负责“拿数据”，不直接调网络。

### 打开/关闭
- 运行：`TideTown.exe -scan`
- 代码：`Features.scan = true`（必须在 `ScanStation.Awake` 之前，静态初始化已读命令行）
- 关闭时：站子看不见、碰不到、不注册，玩家和存档都没有痕迹。

## 明确不做

- 不写中转服务、摄像头、网络请求、打印代码。
- 不花积分、不调 Lux3D。

## 验收

`tools\run-audit.ps1 -Mode scan -OutDir docs\evidence\WP-09`：默认状态不可见/不注册；打开后可见；互动后提示“暂未开放”，不报错。
