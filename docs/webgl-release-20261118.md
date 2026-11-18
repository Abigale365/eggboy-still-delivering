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

已完成完整 WebGL 构建及两次资源增量构建，Unity 返回 0，构建日志报告 Succeeded、0 errors。最终源代码提交为 `4b2ba4b`。修复 `LighthouseKit` 的旧脚本 GUID 引用，恢复最终 Windows 包中的 14 张信封、邮票、明信片纹理，保留非二次幂原始尺寸并开启透明边缘处理。

本地对最终发布包进行了真实浏览器操作：开场进入游戏，镜头转动、WASD、Shift 慢走、空格跳跃、新手教程推进至取信，沿山路走到邮筒，按 E 成功取出并阅读三封信；收信后任务更新为面包店，M 地图同步更新路线；H 指引开关、Esc 暂停菜单正常；V 心愿簿中点击预置烤肉骨头，场景中出现对应 3D 物件。截图见 `evidence/webgl-20261118/local-first-letter.jpg`、`local-wish-object.jpg`。此次造物验证使用本地预置链路，没有调用付费模型 API。

测试覆盖首段主线与上述功能，没有声称完整通关。浏览器日志不再出现缺失 MonoBehaviour 警告；灯塔新特效着色器未随源工程提供，程序会沿用已有光锥，仍记录一条 BEACON_FX 回退警告。

正式域名为 `https://tidetown.xbalab.cn`，GitHub 使用 `Abigale365/eggboy-still-delivering`，仓库名保持不变。

## 正式域名发布结果

2026-11-18 已通过 Cloudflare Pages 发布，项目 `xbalab-tidetown`。首次正式部署 ID 为 `bdceee3d-99b6-463f-98fa-7efa327727a0`。正式域名的 `release.json` 返回 HTTP 200，内容与本地发布包完全一致，Unity 运行时来源提交为 `4b2ba4b3928d6ca9be003fef17c482b768f5972d`。

正式站点实测已完成资源下载、开场明信片、进入游戏、镜头与移动、新手教程显示、地图打开和当前取信目标显示。随后用户要求自行检查，已停止进一步试玩。正式站点没有完整通关，也没有在本轮连接实体开发板或调用付费造物 API。正式页面截图为 `production-opening.jpg`、`production-tutorial.jpg`、`production-map.jpg`；本地更完整的首段验证见前文。

加载页的 Shift 提示已从“跑”更正为“慢走”，与新版游戏操作一致；此项只修改网页提示与版本清单，Unity 运行文件保持一致。GitHub 交付分支为 `tidetown-webgl-20261118`，包含 Unity 源码、`WebGL/` 游戏构建与游戏说明，未改仓库名称。

提示更正后的最终部署 ID：`2cf5380d-fe27-4448-a84b-4c95a38bec79`。此次云端复用 52 个文件，仅上传 2 个小文件，Cloudflare 返回部署成功。
