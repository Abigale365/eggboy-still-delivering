# 进度

规则见 `ENGINEERING_BOOK.md` 第 11 节。状态：todo、doing、done、cut、blocked。

| 工作包 | 内容 | 依赖 | 状态 | 负责 | 更新时间 | 备注 |
|---|---|---|---|---|---|---|
| WP-00 | 基线和版本管理 | 无 | done | A(agent) | 2026-11-06 12:52 | 基线 ba7236e；NATIVE_WINDOWS/NATIVE_WEBGL Succeeded errors=0；routeaudit passed 12/12；证据 docs/evidence/WP-00 |
| WP-01 | 自动化工具 | WP-00 | done | A(agent) | 2026-11-06 13:20 | TideBuild.All 成功（场景哈希不变）；-shot preview 出图；routeaudit 12/12；证据 docs/evidence/WP-01 |
| WP-02 | 比例表和素材登记 | WP-00 | done | B(agent) | 2026-11-06 13:10 | Data/scale_table.json 41/41 对上 town_assets；docs/assets.md 已建；B8/S10/D4N 记为缺失 |
| WP-03 | 素材瘦身 | WP-02 | blocked | B | 2026-11-06 13:10 | 用户 2026-11-06 决定暂不减面 |
| WP-04 | 世界布局数据 | WP-02 | done | B(agent) | 2026-11-06 13:35 | world_layout.json + 高度/可走/分区 bytes 已生成；zones_overlay.jpg 已核对；5 个机位已写入；坡度 p99 62.7° 记入 human-queue |
| WP-05 | 生成岛 | WP-01, WP-04 | done | B(agent) | 2026-11-06 14:10 | TideTown_Main 白盒：岸线底座 + 9 台地 + 崖壁 + 6 路 + 8 栋灰盒；5.7 布局 0px / 轮廓 0.984 / 建筑全过；重建两次对象集合一致；证据 docs/evidence/WP-05 |
| WP-06 | 接入移动（M1） | WP-05 | done | A(agent) | 2026-11-06 17:10 | 真实 Move 1 倍速 28/28、离地 0 帧；5.7 回归通过；证据 docs/evidence/WP-06。未验证：WebGL、镜头实跑、05 号鞋底截图被挡。详见 docs/handoff-wp06-20261106.md |
| WP-07 | 互动、任务、存档 | WP-06 | done | A(agent) | 2026-11-06 17:45 | storyaudit passed：取信/读信锁/不重复/存档重开；旧扩展场景 routeaudit 12/12；证据 docs/evidence/WP-07 |
| WP-08 | 海水和水位 | WP-06 | done | A(agent) | 2026-11-06 18:58 | wateraudit passed：五档 L0-L4 截图、4 s 涨水单调且镜头抬高 1.1 m、木箱区真实 Move 走通（waterFrames=0）、码头掉水 0 s 回安全点；L0 路线回归 28/28；Windows+WebGL 构建成功且 WebGL 编译了 TideTown/Water。证据 docs/evidence/WP-08。未验证：WebGL 浏览器实跑画面 |
| WP-09 | 预留接口 | WP-07 | done | A(agent) | 2026-11-06 19:20 | scanaudit passed：Features.scan=false 时站子不可见不注册，打开后可见、提示“暂未开放”；storyaudit 1.7 s 通过，路线回归 28/28；无联网代码；说明 docs/reserved-interfaces.md，证据 docs/evidence/WP-09 |
| WP-10 | 序章 | WP-07, WP-08, WP-09 | done | A(agent) | 2026-11-06 19:35 | storyaudit passed：预览机位 4.7 s 落到邮局门廊身后、面屏 sleepy→happy、P0/P1 两段通过、取信 6.7 s（<30 s）；P_03 真邮筒 + 暖光，教学提示只在 P0；路线回归 28/28；Windows+WebGL 构建成功；证据 docs/evidence/WP-10。未验证：WebGL 浏览器实跑画面 |
| WP-11 | 面包店（M3） | WP-10 | done | A(agent) | 2026-11-06 21:25 | storyaudit passed：B0 到点自动读信、A4/A5/A6/A7/A8 回忆线索（抽测面包篮）、火柴→柴火→装炉→点火全链、邮戳 1/3、水位 L1、烟囱粒子和炉火可见；面包店 11.2 s，重开恢复；route 28/28；Windows+WebGL 构建成功；证据 docs/evidence/WP-11。占位灰盒待 WP-17，A1–A8 未减面（WP-03 blocked） |
| WP-12 | 小黑入工程 | WP-06 | done | A(agent) | 2026-11-06 23:00 | 模型未导入，按用户决定先做灰盒占位并预留 `DogVisual` 接口（换模型只改 `BuildDog`）：跟随身后 1.2 m、贴地、走/小跑/摇尾；storyaudit passed，dog follow_max 2.94 m、收尾 1.26 m、ground_error 0、behind true；route 28/28；Windows+WebGL 构建成功；证据 docs/evidence/WP-12，接口说明 docs/reserved-interfaces.md。未验证：WebGL 浏览器实跑画面、真实小黑 GLB |
| WP-13 | 学校（M4） | WP-11, WP-12 | done | A(agent) | 2026-11-06 23:35 | storyaudit passed：B4→S0 自动读信、爪印一路到操场、拖鞋→红球→狗碗、S4 逐行 5/5 与面屏 sad→sad→curious→curious→happy、邮戳 2/3、水位 L2、重开恢复；小黑 S3 探头（滑梯旁）S4 接回身后；route 28/28；Windows+WebGL 构建成功；证据 docs/evidence/WP-13。C1–C8 灰盒待 WP-17。未验证：WebGL 浏览器实跑画面、读信蹲下（当前无 crouch 接口） |
| WP-14 | 灯塔（M5） | WP-13 | doing | A(agent) | 2026-11-07 | Windows 自动复验 L0–L5 全过，40 处手电回应。B4/B9 使用原贴图 GLB，灯室、可走平台和光束位置已对齐。已录 31 s Windows 自动流程实机片段，含读信、点灯、扫海和手电回应。塔身比例待画面验收。见 docs/evidence/WP-22/glb-recheck-20261107/lighthouse-windows-audit.mp4。 |
| WP-15 | 结局（M6） | WP-14 | doing | A(agent) | 2026-11-07 | Windows storyaudit 22/22，自动流程 109.6 s。课桌在黎明触发后出现，小黑跟到终点，字幕、水位 L4、存档恢复均通过。新城仍是光点占位，待真人从头通关并记录卡点。见 docs/evidence/WP-22/glb-recheck-20261107。 |
| WP-16 | 界面收尾 | WP-11 | done | A(agent) | 2026-11-07 01:42 | mapaudit passed：5 个点（03/07/11/20/31）红点按预览相机投影对位、读信时地图打不开、地图开着来信会自动让位；地图改用 world_layout + preview.png，带目标与邮戳；标题画面、Esc 暂停、音量滑杆（PlayerPrefs 记忆）；中文字体统一 NotoSansSC，调试标签默认隐藏；storyaudit 22 步仍全过、route 28/28；Windows+WebGL 构建成功；证据 docs/evidence/WP-16。未验证：真人点击标题/暂停/拖音量、WebGL 浏览器实跑 |
| WP-17 | 画面样板段 | WP-05, WP-03 前 10 件 | doing | A(agent) | 2026-11-07 22:10 | 用户要求“场景重构”，直接全镇做了第一版（没先做样板段）：灯光（TownLighting：环境光、反射探针、太阳软阴影、后期）+ SSAO；IslandBuilder.Nature 程序生成圆石崖壁、团簇树、崖边/路边花丛、海岸礁石，草地/沙滩/崖壁程序贴图。不改 Lux3D 模型，装饰无碰撞。story 22/22、route 28/28（救援 0）、water、map 通过；WebGL 加载到标题无报错。对比图 docs/evidence/lookdev-20261107/compare_scene.jpg。待：两位队员看图验收（决策点 11/11）、主街石板、海面颜色与浪花、栅栏、核显帧率（known-issues 25） |
| WP-18 | 环境素材 | WP-17，用户批准生成单 | todo | B | | 11-07 先用程序化圆石/树/花丛代替（不花积分），Lux3D 生成单仍待用户批准 |
| WP-19 | 全镇铺开 | WP-17，决策点 11/11 | todo | B | | |
| WP-20 | 声音 | WP-11 | todo | B | | |
| WP-21 | 性能和包体 | WP-15, WP-19 进行中 | todo | A | | |
| WP-22 | 全流程 QA | WP-15, WP-16 | doing | A(agent) | 2026-11-07 | 当前原贴图版 Windows 自动复验通过，story 22/22、route 28/28、map 5 点、water 五档及回退。Chrome 和 Edge 在 RTX 5070 Ti 上通过加载、开始、暂停、音量零位、地图和短距离移动检查。待浏览器全流程、关页续玩、10 分钟内存和两名新玩家试玩。见 docs/evidence/WP-22/glb-recheck-20261107、known-issues.md 和 human-queue.md。 |
| WP-23 | 部署（M7） | WP-21，用户确定位置 | todo | A | | |
| WP-24 | 报告 | WP-15 | doing | B(agent) | 2026-11-07 03:00 | 草稿 v1 完成：docs/report/WP-24-成果报告草稿.md（真实数据替换 + 三层对齐：叙事层已完成/生态层实体层规划中 + 8 组资产迭代对比 + 工具说明表 + 数字来源索引）；底稿缺失已入 human-queue。待做：图片嵌入排版成 PDF/PPT、两位队员读过 |
| WP-25 | 视频和发布包（M8） | WP-22 | todo | B | | |
| WP-X1 | 扫描小版本（可选） | 决策点 11/14，用户要做 | todo | A | | 没开工 |
| WP-X2 | 打印一件实物（可选） | 打印机型号已知 | todo | A | | 没开工 |

### 2026-11-08 续做试玩收尾

- 面包店按住 E 擦火柴，学校红球滚入狗碗，灯塔按住 E 转动灯头并朝新城方向亮灯。现有任务步骤、旗标和自动测试调用入口保持一致。
- 山顶结局亮起复用的邮局模型和暖灯，原字幕后补出老周的“回家吧”，再开放可选寄件处。
- 寄件处经 IModelProvider 展示工程已有的预生成油壶。玩家可输入一段内容，主动保存到本机，读档后仍能打开。扫描、实时生成和打印沿用预留接口。
- 待在 Unity 2022.3.62f2c1 中重新构建并验证三站手感、山顶取景、寄件处输入与本机保存，含 WebGL 中文输入。现有线上试玩尚未更新。
- 静态检查通过：12 个 C# 文件的 Roslyn 语法检查、WebGL 模板脚本的 Node 语法检查、字体字形覆盖以及 git diff --check。


### 2026-11-09 P0 / P1 / P2 玩法深度升级合并与双端构建验收

- **P0-1 3D 记忆旧物端详与隐藏铭文（`ObjectInspectModal.cs`）**：全部 11 件 3D 记忆旧物 + 学校咬坏的拖鞋接入镜头前 3D 检视（支持鼠标拖拽/方向键 360° 旋转、滚轮缩放），转到背面/底部最佳角度自动触发微光与物件暗藏铭文，将 3D 空间资产转化为叙事解谜核心。
- **P0-2 灯塔手动对准与三段信号对答（`BeaconLight.cs` / `FlashlightReply.cs` / `StoryDirector.Lighthouse.cs`）**：按住 E 推转灯头后进入手动调向与信号发射（`A/D` 或鼠标对准山顶新城 + 按 `Space`/`E` 闪光三次），对岸 40 盏手电筒分三波（`13 → 27 → 40`）依次回应点亮，带 11 秒无操作自动辅助兜底。
- **P1 山顶新邮局「记忆包裹工作台」与明信片加盖邮戳（`EndingDispatch.cs`）**：结局山顶新邮局寄件处支持切换 4 件预生成 Lux3D 记忆物件（生锈黄铜油壶 D1、老周船长帽 D5、藤编面包篮 A4、旧木课桌 C8），支持 360° 鼠标拖拽旋转、缩放、预设视角、展示 Lux3D → Refine Skill 管线元数据、填写收件人与寄语并保存到本机，支持一键加盖潮汐镇邮戳生成纪念明信片并导出截图。
- **P2-1 面包店炉火风箱三段助燃（`BakeryEffects.cs` / `BakeryInteractables.cs`）**：按住 E（0.7s）擦亮火柴点火后，可继续按 E 鼓动风箱助燃（1/3 → 3/3），炉火粒子、暖光半径与飘向山城的烟囱浓烟随鼓风逐级暴涨。
- **P2-2 学校红球物理弹跳 + 小黑抚摸与寻路轻吠（`SchoolBall.cs` / `DogFollower.cs`）**：红球滚入狗碗带阻尼弹跳弧线；真实游玩下随时按 `F` 抚摸小黑（小黑跃起摇尾 + 蛋仔开心表情），小黑靠近水位木箱、船棚油壶、灯塔门与结局课桌时自动跑向目标轻吠引路。
- **Unity 2022.3.62f2c1 双端构建与全量自动化测试通过**：
  - `tools/build.ps1` 自动识别 `H:\Program Files\Unity 2022.3.62f2c1\Editor\Unity.exe` 并通过 `subst` 短路径映射解决深层中文路径下 `TypeDb-All.json` 命令行超长问题。
  - `TideBuild.All`（Windows `Build/Main/TideTown.exe` + WebGL `Build/MainWebGL`）零报错构建成功（`UNITY_EXIT 0`，`BUILD_OK TideBuild.All`）。
  - 五项自动化审计全部通过（证据归档于 `docs/evidence/p0-p2-merge/`）：`story`（22/22 步全过，平均 135.6 FPS，救援 0 次）、`route`（28/28 路点，腾空 0 帧）、`water`（L0–L4 单调涨水、木箱区与掉层救援全过）、`map`（5 点对位与读信互斥全过）、`scan`（预留接口开关验证全过）。
