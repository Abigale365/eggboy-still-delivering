# 潮汐镇功能扩展接口建议

状态：建议方案，尚未实施结构重构。依据 2026-11-06 当前 Unity 工程源码检查，同时参考原 Three.js 原型。本次不改变场景、角色和现有玩法。

目标：让后续地形、多个互动点、三站任务、地图和存档能够独立增加。暂按当前单机原型、Windows 与桌面 WebGL 的已知范围设计；NPC、联网、云端服务等没有被本次方案视为已确认需求。

## 当前需要处理的依赖

| 位置 | 观察到的实现 | 扩展时的影响 |
|---|---|---|
| `Assets/Scripts/PrototypeGame.cs` | 同时创建角色、读取输入、驱动表情、处理取信、更新目标和运行巡检 | 新功能继续集中加入入口；任务依赖具体界面和角色实现 |
| `Assets/Scripts/IEggboyWorld.cs` | 已抽象地面查询，但还包括唯一的 `MailboxPos`，并暴露 `EggboyCollider` 列表 | 地形、角色碰撞与特定剧情点混在一起 |
| `Assets/Scripts/TownWorld.cs` | `GroundAt(x,z)` 从高空向下取命中高度，结果只有浮点高度 | 分层道路、桥下通行、坡度和地面类型不能由这个结果充分表达 |
| `Assets/Scripts/EggboyUI.cs` | 界面内写着固定的信件正文，并自行检测到邮筒的距离 | 扩展新信件或互动对象需要改 UI 代码 |
| `Assets/TideTown/Scripts/TownMap.cs` | 道路和地标坐标手写，直接设置角色 `locked` | 移动建筑后地图可能不同步；以后多个流程锁定角色时可能互相释放 |
| `Assets/TideTown/Scripts/TownAssetInfo.cs` | `assetId` 表示来源模型，同一预制体的实例共享这个资产标识 | 玩家存档还需要单独的场景实例标识 |
| `Assets/TideTown/Editor/ExpandedTownBuilder.cs` | `BuildWindows()` 调用 `Build()`，从旧场景重新生成并保存扩展场景 | 手工编辑地形与布局后，误用此入口可能覆盖修改 |
| `Assets/TideTown/Editor/NativeFaceBuild.cs` | 直接构建指定场景 | 已有纯打包入口，可作为统一构建方式的基础 |

角色的 Skin、Bones、Gait、Pose、Face、AntiClip 已经有职责分工，优先保持现有效果和调用顺序。`IEggboyWorld` 也提供了可逐步迁移的基础。

## 先稳定三条接口

以下名称和签名是设计草案，不是已经加入工程的 API。

### 世界查询 `IWorldQuery`

- 地面查询接收三维起点与查询距离，例如 `TryGetGround(origin, maxDistance, out hit)`。
- `GroundHit` 至少包含命中点、法线、地面类型和所在区域；无命中必须显式返回失败，不能统一退回高度 0。
- 查询需结合角色当前高度与允许迈步高度，避免把桥面识别成桥下角色脚下的地面。
- 通行与碰撞仍由世界实现提供，第一轮可适配已有碰撞代码，保持移动手感。
- 出生点、邮筒、地标归场景配置与交互注册，不再写在通用地形接口中。

### 互动对象 `IInteractable`

- 每个场景实例有稳定 `EntityId`，提供提示文案、可用条件和互动入口。
- 建议约定 `CanInteract(context)`、`TryInteract(context)`，结果区分拒绝、已开始与已完成。
- 一次动作或对话结束后才发布 `InteractionCompleted(entityId, actionId)`；任务系统据此推进，避免把“按下 E”直接当成完成任务。
- 对已完成的一次性互动，再次执行应保持同一结果，不重复发奖励或推进任务。
- 邮筒、烤炉、狗碗、灯塔开关各自实现互动；UI 只显示注册的提示和内容。

### 存档 `ISaveStore`

- 使用 `LoadAsync` / `SaveAsync`，定义找不到存档、版本不兼容和保存失败的返回结果。
- 保存带 `schemaVersion` 的状态数据，包含任务阶段、已取信件、物件状态和角色位置。
- 记录稳定 `EntityId`；不保存 GameObject 引用、实例运行时编号或靠名称匹配。
- Windows 与 WebGL 的存储实现放在接口后面，任务逻辑使用同一份状态格式。
- 存档恢复后，场景物件和 UI 根据状态重建表现。不能依赖关页时才进行一次保存。

Unity 的 `persistentDataPath` 在 Windows 和 WebGL 上对应不同的存储位置，具体落盘策略需要分别验证：<https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Application-persistentDataPath.html>。

## 配合接口拆出的具体模块

| 模块 | 责任 | 初期实现方式 |
|---|---|---|
| `GameBootstrap` | 创建对象、连接引用、选择配置 | 从 `PrototypeGame` 逐步提取；只做初始化装配 |
| `InputRouter` | 把按键和鼠标转换为角色命令，分配 UI 与游戏输入 | 先接现有 Unity 输入；巡检也通过命令驱动 |
| `InteractionService` | 选择附近目标、显示提示、发起互动 | 管理互动对象注册与取消注册 |
| `QuestService` + `GameState` | 推进任务与保存运行状态 | 先实现取信这一条现有流程，不先创建大量未使用的抽象 |
| `CharacterPresentation` | 对外提供表情、动作等角色表现入口 | 内部继续使用现有 Face、Pose 与动作代码 |
| `ControlGate` | 管理读信、地图、对话等流程的控制占用 | 每个调用方取得自己的锁，只释放自己的锁 |
| `WorldDefinition` / 地标注册 | 地图、场景出生点、地标与互动实例的共同数据来源 | 使用稳定标识与明确的坐标约定 |

每帧的移动、步态和相机更新继续直接调用。只在互动完成、任务变更和状态改变等明确节点使用类型化事件；暂时不引入全局通用消息总线或依赖注入框架。

## 区分定义、状态与表现

- 定义：信件正文、任务条件、地标、模型引用、默认配置。
- 状态：已取哪封信、烤炉是否点燃、任务完成到哪一步。
- 表现：显示信纸、播放递信动作、亮起炉火、更新地图图标。

定义可以使用 Unity ScriptableObject 便于编辑；玩家状态另存。ScriptableObject 资产不能直接作为发布版的玩家持久存档：<https://docs.unity3d.com/2022.3/Documentation/Manual/class-ScriptableObject.html>。

场景实例 ID 与资产 ID 分开：同一种路灯模型可出现多次，每盏灯有自己的实例 ID。生成布局时应保留已有 ID，并在编辑器检查重复，避免地形重建后存档对不上物件。

## 建议目录

```text
Assets/TideTown/
  Runtime/
    Bootstrap/
    World/
    Interaction/
    Quests/
    Characters/
    UI/
    Persistence/
  Data/
  Art/
  Prefabs/
  Scenes/
  Editor/
```

目录整理放在职责和接口确定之后。迁移现有 Unity 资源时应保留 `.meta` 和 GUID；当前阶段不需要立即把每个目录都拆成程序集。

## 分阶段落地与验收

1. **统一构建入口**：纯打包不调用场景生成器。验证构建前后场景文件内容未改变。
2. **互动与任务拆分**：先迁移取信流程，界面读取信件定义。验证 E 取信、重复按键、读信锁定、关闭信纸、目标变化与表情恢复保持原行为。
3. **稳定 ID、状态与存档**：让取信状态跨重启保留。验证未知对象、旧版本存档和新增物件的默认状态。
4. **地形接口与地图数据**：增加真实高度、法线与地面类型查询；地图读取同一份地标。验证坡道、台阶、桥上下层、码头和无地面时的处理。
5. **扩展第二个互动**：以烤炉作为验证对象，确认新增玩法只需要新增对象行为和任务定义，不再修改主入口、角色移动或 UI 布局。

未来如果改用 Three.js，可以共用版本化的世界布局、信件、任务定义和状态格式；C# 逻辑、Unity 场景与材质不能直接变成 TypeScript 或 Three.js 实现。跨引擎数据还需约定单位、坐标轴、原点与版本。
