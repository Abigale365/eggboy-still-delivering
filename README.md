# EGGBOY · Still Delivering（寄给不在的人）

潮汐镇的人都搬走了，海水一天天涨上来。小机器人邮差 EGGBOY 还在送信：每封信寄给一个已经不在的人，靠收信人留下的东西，一站一站拼出他们的故事。最后一封信，是寄给它自己的。

参加 Aholo 3D AI 黑客松的作品。场景里的建筑、道具和角色都用 Aholo Lux3D 生成，用 Unity 2022.3（URP）搭成可以玩的小镇。

**在线试玩：** https://forkseek.github.io/eggboy-still-delivering/

建议用电脑上的 Chrome 或 Edge 打开。第一次加载要下载约 195 MB。

## 操作

- Enter：开始
- WASD 或方向键：走路；按住 Shift：跑
- 空格：跳
- E：互动（取信、拿东西、看信）
- 点一下画面锁定鼠标，移动鼠标转镜头；Esc 释放鼠标、打开菜单；滚轮：拉远拉近
- M：地图

## 工程

- Unity 2022.3.62f2c1 + URP，工程就是这个仓库的根目录
- 场景由脚本生成：`Assets/TideTown/Editor/IslandBuilder*.cs`。主场景是 `Assets/TideTown/Scenes/TideTown_Main.unity`
- 地图流程：`tools/preview/build_layout.py` → `build_route.py` → `tools\build.ps1 -Method IslandBuilder.BuildMain` → `tools/preview/route_check.py`
- 打包：`tools\build.ps1 -OnlyWindows`，或者 `tools\build.ps1 -Method TideBuild.WebGL`
- 发布网页版：`python tools/webgl_split.py Build/MainWebGL <目录>`。这一步会把超过 90 MB 的数据文件切块（GitHub 单个文件不能超过 100 MB），网页模板会自动拼回去
- 自动检查：`tools\run-audit.ps1 -Mode story|route|water|map`；碰撞贴合检查用 `tools\build.ps1 -Method CollisionFitAudit.Run`
- Lux3D 模型原件在 `Assets/TideTown/SourceGLB`。它们内嵌的贴图会在打开工程时自动取出，放到 `Assets/TideTown/Art/GlbTex` 再压缩，这个目录不进 git
- 详细的设计和进度：`docs/ENGINEERING_BOOK.md`、`docs/progress.md`
