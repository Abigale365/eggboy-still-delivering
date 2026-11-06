# 性能基线（优化前）

记录时间：2026-11-07 ｜ 基线提交：`424b00b`（WP-22 全绿）
用途：优化前后对照。优化路线：不减面（用户 2026-11-06 决定维持），走 LOD / 纹理压缩 / 剔除。

## 包体与仓库

| 指标 | 基线值 | 测量方式 |
|---|---|---|
| WebGL 构建体积 | 37 MB（工程书预算 ≤120 MB，达标） | `du -sh Build/MainWebGL` |
| Windows 构建体积 | 139 MB | `du -sh Build/Main` |
| 仓库 .git 体积 | 485 MB（pack 484.66 MiB） | `git count-objects -vH` |
| 证据目录 | 207 MB（已入库，历史不重写，向前优化） | `du -sh docs/evidence` |
| SourceGLB（原始模型，在 Assets 内） | 485 MB | `du -sh Assets/TideTown/SourceGLB` |

## 资产面数（不减面，仅登记）

| 指标 | 基线值 | 来源 |
|---|---|---|
| 进工程 GLB | 41 件（缺失 3：B8/S10/D4N） | docs/assets.md |
| 资产总面数 | 5,215,760（工程书 scale_table 上限合计 281,820） | docs/assets.md |
| 单件最大 | A1 烤炉 298,172 面；B9 灯塔灯头 206,615 面 | docs/assets.md |

## 运行指标

| 指标 | 基线值 | 来源 |
|---|---|---|
| storyaudit 平均 fps（批处理加速模式，非真实游玩帧率） | 292.3 | docs/evidence/WP-22/story-audit.json |
| 全流程自动审计时长 | 96.5 s（加速值，非真人时长） | 同上 |
| 路线回归 | 28/28，airFrames 0，maxGroundGap 0.039 | docs/evidence/WP-22/route-audit.json |
| WebGL 无头冒烟 | 加载完成、0 error | docs/evidence/WP-22/webgl/webgl-check.json |

## 未测项（优化后仍要补）

- 真实游玩帧率（RTX 5070 Ti 本机实测）
- 核显机帧率（human-queue）
- 真显卡浏览器 Chrome/Edge 全流程、10 分钟内存
- 真人通关计时（human-queue）

## 优化后（2026-11-07，提交 `afe0d56`+）

| 指标 | 优化前 | 优化后 | 说明 |
|---|---|---|---|
| WebGL 包体 | 37 MB | **69 MB** | 真资产进场景的代价，仍 ≤120 MB 预算 |
| 场景建筑 | 全部灰盒 | 7 栋真 prefab + LODGroup | 灰盒保留为碰撞体和 LOD1 远景 |
| B6/B7 房壳法线 | 1196 面全朝内（黑立面） | Blender 重算外法线 | 不减面，只改法线 |
| 损坏 PNG | 15 张（IEND 后 1–3 字节垃圾） | 全部修复 | Unity 解码恢复正常 |
| 小道具纹理 | 2K | 上限 1024（建筑保留 2K） | CapPropTextures |
| story/route/map | 全绿 | 全绿（每步回归确认） | 行为不变 |
| 已知遗留 | — | B3 学校正面黑墙（known-issues #12） | 需单独修，不能跑全局重绑 |
