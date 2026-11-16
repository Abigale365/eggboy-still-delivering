# EasyInput V2.0 网页接入

在支持 WebHID 的桌面 Chrome 或 Edge 打开 WebGL 构建（HTTPS 或 localhost）。用 USB 数据线连接板子，再点击游戏右上角「连接 EasyInput」。浏览器设备选择器需要玩家亲自选中设备。板上使用含续想 Host Action v1 UUID 的现有配置即可。

- S1/S2/S3/S4 对应前、左、后、右的短步移动。Host Action 本身是一条动作脉冲，连续移动可用键盘。
- S5 或旋钮单按触发互动。S6 跳跃，S7 地图，S8 暂停或返回。
- 旋钮左右拨动产生短步，在灯塔手动调向时转动灯头。
- 打开木栓齿轮谜题后，S1/S2/S3 拨动三枚木栓，按住旋钮再按 S1/S2/S3 选环，旋钮左右转动当前环，单按旋钮或按 S5 尝试解锁。网页显示收到的标准化动作。

游戏打开 USB Vendor HID 并读取输入，协议为 Host Action v1。离开网页、失焦或断开会清理状态。键盘、鼠标可同时使用。原生 Windows 版通过 EasyInputNativeBridge 与内置 EasyInputRelay 接收 USB Host Action 或 BLE GATT 通知，游戏手柄入口沿用 WinMM。BLE 实板通知格式待验证，接入状态与验收步骤见项目根 08_最终提交材料/2026-11-16_开发板接入/开发板接入与验收清单.md。这个 WebHID 接入随 WebGL 构建交付。

实板验收需在获准使用 USB 的桌面浏览器内进行。验证 S1–S8、旋钮三动作及 Fn 组合，再切换标签页、拔插 USB，最后验证续想仍能使用板子。


Fn 组合补充映射为按住旋钮加 S4/S5/S6/S7/S8，依次触发 V 造物笺、Tab 信件、R 潮汐残影、B 下塔、F 吹气或小黑操作。谜题内 Fn+S1/S2/S3 沿用选环。
