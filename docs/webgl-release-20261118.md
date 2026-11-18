# 潮汐镇 WebGL 发布记录（2026-11-18）

## 源码与修复范围

本次以 `eggboy-still-delivering-latest` 的 `1830196` 为 Git 基线，保留工作区已合入的 2026-11-18 新版运行时代码。新版运行时代码来自已有的 `TideTown-EGGBOY-20261118-merged-src` 合流结果，其中包含从最终 Windows 程序集恢复的脚本；这次解决其与原有 Unity 工程同时编译的冲突。

- 原 `Assets/Scripts` 中角色、镜头和 UI 脚本沿用原路径和 `.meta` GUID，内容使用新版实现。拆分出来的枚举、数据结构继续保留在 `Runtime/Core`。
- 合并文件中的旧剧情、交互物、影灯、声音瓶、愿望物件、Lux3D 网关由已有新版拆分实现接替。旧文件保留迁移说明和原 `.meta`，不再声明重复类型。
- `StoryDirector.cs` 已包含新版面包店、学校、灯塔和结局逻辑，旧四个 partial 文件退出编译，拍摄扩展保留。
- 保留 `MicLevel.cs` 中浏览器麦克风桥接，撤出重复的 Windows 反编译版本。
- `HardwareAndSensorHub` 的 UDP 后台线程仅在原生平台启动；WinMM 手柄接口仅在 Windows 编译。网页版仍保留键盘、鼠标、EasyInput 网页桥接与浏览器麦克风入口。
- 源码备份保存在本机 `Logs/source-before-dedup-*.zip`，不上传这些过程备份。

## 新版功能核对

`GameBootstrap` 创建 `BasicsTutorial` 与 `QuestGuide`；新手教程共 10 步；任务指引根据剧情目标更新方向、距离和路径引路点。源码核查结果与文件 SHA-256 清单位于 `docs/evidence/webgl-20261118/`。

## 发布与验收

构建、浏览器试玩、发布结果在完成后补充。正式域名为 `https://tidetown.xbalab.cn`，GitHub 使用 `Abigale365/eggboy-still-delivering`，仓库名保持不变。
