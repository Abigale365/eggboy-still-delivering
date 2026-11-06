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
| WP-12 | 小黑入工程 | WP-06 | todo | A | | |
| WP-13 | 学校（M4） | WP-11, WP-12 | todo | A | | |
| WP-14 | 灯塔（M5） | WP-13 | todo | A | | |
| WP-15 | 结局（M6） | WP-14 | todo | A | | |
| WP-16 | 界面收尾 | WP-11 | todo | A | | |
| WP-17 | 画面样板段 | WP-05, WP-03 前 10 件 | todo | B | | |
| WP-18 | 环境素材 | WP-17，用户批准生成单 | todo | B | | |
| WP-19 | 全镇铺开 | WP-17，决策点 11/11 | todo | B | | |
| WP-20 | 声音 | WP-11 | todo | B | | |
| WP-21 | 性能和包体 | WP-15, WP-19 进行中 | todo | A | | |
| WP-22 | 全流程 QA | WP-15, WP-16 | todo | A+B | | |
| WP-23 | 部署（M7） | WP-21，用户确定位置 | todo | A | | |
| WP-24 | 报告 | WP-15 | todo | B | | |
| WP-25 | 视频和发布包（M8） | WP-22 | todo | B | | |
| WP-X1 | 扫描小版本（可选） | 决策点 11/14，用户要做 | todo | A | | 没开工 |
| WP-X2 | 打印一件实物（可选） | 打印机型号已知 | todo | A | | 没开工 |
