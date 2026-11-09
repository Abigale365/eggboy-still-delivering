# EGGBOY · Still Delivering（寄给不在的人）

潮汐镇的人都搬走了，海水一天天涨上来。小机器人邮差 EGGBOY 还在送信：每封信寄给一个已经不在的人，靠收信人留下的东西，一站一站拼出他们的故事。最后一封信，是寄给它自己的。

参加 Aholo 3D AI 黑客松的作品。场景里的建筑、道具和角色都用 Aholo Lux3D 生成，用 Unity 2022.3（URP）搭成可以玩的小镇。

**在线试玩：** https://forkseek.github.io/eggboy-still-delivering/

建议用电脑上的 Chrome 或 Edge 打开。第一次加载要下载约 195 MB。

## 操作与多解玩法（键鼠 + 单手柄无缝共存）

- **基础移动与互动**：
  - `Enter`：开始
  - `WASD` 或方向键 / 手柄左摇杆：走路；按住 `Shift`：跑（逆海风时压低重心）
  - `空格`：跳；`E` / 手柄 `A` 或 `X`：互动（取信、拿东西、端详 3D 旧物、拨动 3D 机关）
  - 点一下画面锁定鼠标，移动鼠标转镜头；`Esc` 释放鼠标、打开菜单；滚轮：拉远拉近
  - `M`：地图
- **密室逃脱 & 一题多解核心能力**：
  - `V` 键 / 手柄 `Y` 键：呼出 **【邮差心愿 3D 造物笺 & 15 项一题多解成就图鉴】**（支持自由打字或一键点选灵感词牌，按个人喜好为小黑造美食/新玩具、在涨潮渡口造大黄鸭浮船/海龟浮台过海、或在三站心愿展台留下永久 3D 纪念摆件）
  - `Tab` 或 `L` 键：翻阅当前信件，用鼠标按住信纸底部的字签拖出信纸抛向 3D 空中，直接化为真实 Lux3D 物件
  - `R` 键：唤出十秒前的潮汐残影与自己协力摇动渡口绞盘
  - `B` 键 / 手柄 `B` 键：在灯塔顶层任意时刻顺旋梯安全下塔回到地面（塔门按 `E` 可随时重返塔顶）
  - **麦克风吹气 & 摄像头挥手（含无外设平替）**：端详 3D 旧物背面或烤炉旁可对着麦克风吹气（无麦可用鼠标擦拭 / `F` 键 / 手柄 `RB` 平替）；灯塔顶层可开启摄像头挥手闪灯（或用 `空格` / `E` / 手柄按键平替）
- **完整通关与 15 项“嘀—嘟！”成就说明**：详见仓库根目录下的 [`邮差密室通关与多解成就全攻略.md`](./邮差密室通关与多解成就全攻略.md)

## 工程

- Unity 2022.3.62f2c1 + URP，工程就是这个仓库的根目录
- 场景由脚本生成：`Assets/TideTown/Editor/IslandBuilder*.cs`。主场景是 `Assets/TideTown/Scenes/TideTown_Main.unity`
- 地图流程：`tools/preview/build_layout.py` → `build_route.py` → `tools\build.ps1 -Method IslandBuilder.BuildMain` → `tools/preview/route_check.py`
- 打包：`tools\build.ps1 -OnlyWindows`，或者 `tools\build.ps1 -Method TideBuild.WebGL`
- 发布网页版：`python tools/webgl_split.py Build/MainWebGL <目录>`。这一步会把超过 90 MB 的数据文件切块（GitHub 单个文件不能超过 100 MB），网页模板会自动拼回去
- 自动检查：`tools\run-audit.ps1 -Mode story|route|water|map`；碰撞贴合检查用 `tools\build.ps1 -Method CollisionFitAudit.Run`
- Lux3D 模型原件在 `Assets/TideTown/SourceGLB`。它们内嵌的贴图会在打开工程时自动取出，放到 `Assets/TideTown/Art/GlbTex` 再压缩，这个目录不进 git
- 详细的设计和进度：`docs/ENGINEERING_BOOK.md`、`docs/progress.md`
