# 终版配乐、实拍与引擎录制

本轮只修改 latest 工程；不运行场景生成器，不调用付费 Lux3D 接口，不推送远程。

## 配乐与收声

`Resources/Music` 的 theme / bakery / school / lighthouse / ending / ambient_explore 来自 2026-11-16 配乐 v2 长版，保留五首旧曲 meta/GUID。ending 单次播放，通关后自由探索交叉淡化到 ambient_explore。背景潮水及既有音效保留。

麦克风底层在 `MicLevel.cs`：优先阵列/USB/麦克风设备，Virtual/Broadcast/混音设备后置；连续约 3 秒无有效采样或无声时切换下一台。读音频样本跨循环边界，不把 GetPosition 的回绕误判成永久失效。离开端详和已点燃的烤炉附近会释放设备；权限拒绝、设备拔出时键盘替代仍可用。RMS 是声能判断，不能区分人声与吹气；物理麦克风与实板需要真人验收。

## F5–F9

正常窗口中按 F5–F9 可进入拍摄状态。审计参数（含 audit / -innov）时不挂载脚本，StoryDirector.IsAuditMode 时不响应。进入实拍后使用 `tidetown.filming.save.v1`，不覆盖正常游玩或 QA 存档；退出重开恢复正常游玩存档。

- F5：邮局门前演出起点，S5 / 旋钮按下 / E 触发推门动作与迈出。当前关卡没有可开合的邮局门实体，使用现有推门动作演出。
- F6：面包店烤炉前，预置火柴、干柴并点亮炉火，Tab 重读信；直接在炉边吹气鼓风，便于拍 H3。
- F7：学校后门空狗碗前，V 打开心愿笺，可输入骨头；此处不自动调用付费生成。
- F8：涨潮渡口绞盘前，R 怀表，C 取景框；旋钮左右或 A/D 微调，旋钮单按 / S5 / E / 空格拓印。
- F9：当前场景灯塔顶层灯头前，补齐前置并打开手动信号台；旋钮左右扫海，S5 / 空格闪灯，B 下塔。拍摄期间锁住角色位移，旋钮不会同时让角色在塔顶横移。

F9 使用当前关卡灯头与甲板的实际位置，不把旧版“17m”固定值套到新版地图。端详/机关弹窗中请先 Esc 合上再跳关。

## 引擎级录制

```
TideTown.exe -trailerRecord "G:\输出目录" -lux3d offline -noIntro -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "G:\输出目录\Player.log"
```

需要 PATH 中有 FFmpeg。只有 `-trailerRecord <dir>` 才挂载导演；读取随包 Resources/Trailer/timing_manifest_150s，4500 帧，Time.captureFramerate=30，1920×1080。末尾四秒为片尾卡。屏幕图像直接由引擎 RenderTexture 抓取，RGB 经 stdin 编码，包含 Overlay UI，与桌面窗口遮挡和鼠标无关。Windows D3D 下纹理方向已实测校正。

导演调用现有揭字、心愿、残影、拓印、炉火、小黑、灯塔与手电 API，外加拍摄机位、动作调度和转台小窗。它是引擎内编排实录，不代表真人一次无剪辑通关；offline 展示预生成保底物，不伪称现场完成了付费模型生成。

合成工具与产物位于项目根 `08_最终提交材料/2026-11-16_介绍视频/`：`finish_trailers.py`、`compose_pip.py`、`合成实拍手部小窗.ps1`。实拍素材 H1–H9 放入同级“实拍素材”；缺号自动跳过。H1/H7 默认全屏同框，`-WindowOnly` 可统一用小窗。

验证证据以该目录“成片与终版构建交付说明.md”为准。`tools/run-final-audits.py` 会先归档旧审计及截图，不删除旧证据。
