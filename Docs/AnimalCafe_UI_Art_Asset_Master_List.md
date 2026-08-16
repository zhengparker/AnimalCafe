# AnimalCafe UI Art Asset Master List

> 用途：作为 AnimalCafe 全游戏 UI 美术素材的长期制作清单。  
> 当前阶段：Phase 5 review / UI visual exploration。此文档是规划与素材 backlog，不代表提前实施后续 Phase。  
> 原则：先建立可复用的视觉系统，再逐步制作各玩法模块素材；正式接入 Unity 前仍需通过对应 Phase 的设计与 review。

## 1. 如何使用这份清单

每个条目包含：

- **Asset ID**：方便在 Figma、素材文件夹和 Unity 中使用一致名称。
- **交付格式**：最终应准备的文件类型。
- **推荐软件**：最适合制作该类素材的软件，不要求只使用一种。
- **负责人**：说明谁负责首稿与最终判断。
- **Codex 可以帮忙**：Codex 可承担的具体工作。
- **你需要 review / 完成**：需要你做出的视觉或产品判断。
- **目标 Phase**：预计正式确定或接入的阶段，不是现在必须完成的期限。
- **状态**：建议使用 `Not Started / Exploring / Draft / Approved / Exported / Integrated`。

### 负责人说明

| 标记 | 含义 |
|---|---|
| **Codex + 你 Review** | Codex 可以制作结构化首稿、组件、简单 vector、规格和导出方案；你负责视觉判断与批准。 |
| **共同完成** | Codex 可以提供多个方向和可编辑底稿，但风格、质感及最终取舍需要你持续参与。 |
| **你主导 + Codex Assist** | 角色插画、品牌气质、关键视觉等高度依赖个人审美的内容由你主导；Codex 可做概念、变体、整理和技术辅助。 |
| **Unity/Codex + 你 QA** | 主要是 Unity 内的 runtime 效果或交互实现；Codex 可在对应 Phase 获批后实现，你检查实际手机画面。 |
| **自动生成 + 你 Review** | 由 Blender、Unity 或工具 pipeline 批量生成，再由你检查构图和品质。 |

## 2. 软件分工建议

| 软件 | 最适合做什么 | 不建议主要用来做什么 |
|---|---|---|
| **Figma** | UI layout、components、variants、颜色与间距 variables、按钮/面板、简单 icon、prototype、export spec | 复杂手绘、厚重纹理、精细 bitmap painting、Unity runtime shader |
| **Affinity Designer 或 Inkscape** | 精细 vector icon、logo、装饰线稿、可无限缩放的图形 | 大面积数字绘画、Unity 动态效果 |
| **Krita 或 Affinity Photo** | 手绘插画、纸张/布料纹理、bitmap cleanup、banner、角色表情 | UI component 状态管理、完整 interaction prototype |
| **Blender** | 家具、食物、建筑等 3D thumbnail 的渲染源 | 2D UI 排版与交互逻辑 |
| **Unity** | 最终 UGUI、TextMeshPro、9-slice、动画、mask、material、shader、真实游戏数据与状态 | 早期视觉探索和大量静态素材绘制 |
| **Image generation（可选）** | moodboard、方向探索、插画草稿、纹理候选 | 未经人工修改与授权检查就直接作为最终统一素材 |

### 一致性的关键

一致性并不等于“所有东西都在同一个软件制作”。真正保证一致的是统一的 **design tokens、组件规则、线宽、圆角、光影、材质配方、icon grid 和 export 规范**。建议以 Figma 作为 UI 的视觉 source of truth，Krita/Affinity 负责复杂画面，Unity 只负责必须实时计算或响应游戏状态的部分。

## 3. 全局视觉基础 Global Foundations

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `UI-TOKEN-COLOR` 颜色系统 | 背景、surface、文字、accent、success、warning、danger；Figma Variables + 文档 | Figma | Codex + 你 Review | 建变量、semantic 命名、light/dim 状态与对比检查 | 决定正式色感和品牌气质 | P5 / P47 | Exploring |
| `UI-TOKEN-TYPE` 字体系统 | Display、Heading、Body、Label、数字；font style | Figma | 共同完成 | 推荐层级、字号、行高与 TMP 对照表 | 选定有合适 license 的正式字体 | P5 / P47 | Exploring |
| `UI-TOKEN-SPACING` 间距与尺寸 | 4/8-based spacing、touch target、safe area、mobile portrait grid | Figma | Codex + 你 Review | 建 variables、grid 和使用说明 | 检查手机上是否舒适 | P5 / P50 | Draft |
| `UI-TOKEN-SHAPE` 形状语言 | 圆角、边框、切角、tab 轮廓；variables/spec | Figma | 共同完成 | 出 2–3 套 shape recipe | 选择是否偏纸张、陶瓷、木质或软布 | P5 / P47 | Exploring |
| `UI-TOKEN-ELEVATION` 阴影层级 | 0–4 层 elevation、pressed/inset；style/spec | Figma + Unity | 共同完成 | 制作比较板与 Unity 参数建议 | 判断立体程度和实际画面融合度 | P5 / P47 | Exploring |
| `UI-MAT-SURFACE` 主 surface 材质 | 实色、轻纸感、陶瓷感等；tileable PNG + recipe | Krita/Affinity Photo + Figma | 共同完成 | 生成/整理候选、做应用 mockup | 选最终质感并检查是否抢画面 | P47 | Not Started |
| `UI-MAT-FROST` Frost 配方 | 轻 HUD frost 或 modal backdrop；texture + Unity material spec | Figma + Krita + Unity | Unity/Codex + 你 QA | 比较真 blur、假 frost、gradient/noise 配方并实现获批方案 | 在 MainCafe 真画面判断是否高级、清晰 | P47 / P48 | Exploring |
| `UI-MAT-PAPER` 纸张纹理 | 菜单、信件、任务卡；tileable PNG | Krita/Affinity Photo | 共同完成 | 制作低/中/高三种密度候选 | 选择颗粒、边缘和装饰程度 | P47 | Not Started |
| `UI-MAT-WOOD` 木质点缀 | 标题牌、少量框架；tileable PNG / 9-slice | Krita/Affinity Photo | 共同完成 | 生成可平铺底稿与压缩测试 | 避免 UI 过于厚重或像旧式手游 | P47 | Not Started |
| `UI-MAT-FABRIC` 布艺点缀 | 标签、活动、温暖装饰；tileable PNG | Krita/Affinity Photo | 你主导 + Codex Assist | 提供纹理方向与清理建议 | 决定是否真的纳入核心风格 | P47 | Not Started |
| `UI-GUIDE-EXPORT` 导出规范 | 命名、尺寸、padding、pixels-per-unit、compression、9-slice border | Figma + 文档 | Codex + 你 Review | 建完整规则和检查表 | 确认规则符合实际工作习惯 | P5 / P47 | Not Started |

## 4. 通用容器与控件 Core Components

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `UI-PANEL-LG` 大型页面面板 | 全屏/大 modal；9-slice PNG + Figma component | Figma + Krita | Codex + 你 Review | 制作 variants 和 9-slice safe zones | 判断材质、边框和画面反差 | P47 | Not Started |
| `UI-PANEL-MD` 中型弹窗 | 确认、详情、升级；9-slice PNG | Figma + Krita | Codex + 你 Review | 做 title/no-title、scroll/non-scroll variants | 检查信息层级 | P47 | Not Started |
| `UI-PANEL-SM` 小卡片/tooltip | 资源提示、状态说明；9-slice PNG | Figma | Codex + 你 Review | 完成 component 与尺寸规则 | 检查最小尺寸可读性 | P47 | Not Started |
| `UI-SHEET-BOTTOM` Bottom sheet | 手机竖屏的次级操作与详情；component | Figma | Codex + 你 Review | 设计展开高度、handle、safe area variants | 确认是否符合实际 flow | P47 / P50 | Not Started |
| `UI-TITLE-PLATE` 标题牌 | 页面/模块标题；SVG/PNG/component | Figma + Designer/Inkscape | 共同完成 | 出简洁、纸质、轻木质版本 | 选定正式造型 | P47 | Not Started |
| `UI-DIVIDER` 分隔线 | 列表、section；SVG/PNG | Figma | Codex + 你 Review | 建 straight/decorative variants | 控制装饰强度 | P47 | Not Started |
| `UI-SCROLLBAR` 滚动条 | 长列表反馈；SVG/PNG/component | Figma | Codex + 你 Review | 设计 normal/drag 状态 | 手机实测是否明显但不抢眼 | P47 / P50 | Not Started |
| `UI-BTN-PRIMARY` 主按钮 | 购买、确认、开始；9-slice/component | Figma | Codex + 你 Review | 做 normal/pressed/disabled/loading variants | 确认主行动是否醒目 | P5 / P47 | Existing baseline — Review |
| `UI-BTN-SECONDARY` 次按钮 | 取消、返回、替代操作 | Figma | Codex + 你 Review | 完成状态和 token binding | 检查不会与主按钮混淆 | P5 / P47 | Existing baseline — Review |
| `UI-BTN-ICON` Icon button | 关闭、设置、信息、加号 | Figma + Designer/Inkscape | Codex + 你 Review | 建 touch target、badge、状态 variants | 检查 icon 识别度 | P47 / P50 | Not Started |
| `UI-TAB` 页签 | 当前/预订、分类、商店栏目 | Figma | Codex + 你 Review | 做 top/side/segmented variants | 决定不同页面统一用法 | P47 | Not Started |
| `UI-CHIP` Filter/status chip | 筛选、标签、稀有度、状态 | Figma | Codex + 你 Review | 建 selected/unselected/locked | 控制信息密度 | P47 | Not Started |
| `UI-TOGGLE-CHECK-RADIO` 选择控件 | 设置与筛选；SVG/component | Figma | Codex + 你 Review | 设计完整交互状态 | 检查 touch target | P47 / P50 | Not Started |
| `UI-SLIDER` 滑杆 | 音量、灵敏度；SVG/component | Figma | Codex + 你 Review | 建 fill/thumb/disabled 状态 | 在手机上检查操作感 | P47 / P50 | Not Started |
| `UI-BADGE` 数量/提醒角标 | 未读、可领取、库存数字 | Figma | Codex + 你 Review | 设计 1–99+ 和 dot variants | 检查小尺寸可读性 | P47 | Not Started |
| `UI-PROGRESS` 进度条 | 经验、任务、制作、关系值 | Figma + Unity | Codex + 你 Review | 做多用途 component 与 fill 规则 | 检查颜色语义 | P47 | Not Started |
| `UI-TOAST` Toast/通知 | 保存、获得奖励、错误；component | Figma + Unity | Codex + 你 Review | 设计类型、进出场规格 | 检查不遮挡核心操作 | P47 / P48 | Not Started |
| `UI-EMPTY-LOADING-ERROR` 系统状态 | 空列表、loading、断线、失败；illustration/icon + component | Figma + Krita | 共同完成 | 先做统一模板与 placeholder illustration | 决定文案语气和插画风格 | P18 / P47 | Not Started |

## 5. 通用 Icon 系统

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `ICON-GUIDE` Icon grid 与线条规则 | 24/32/48 grid、stroke、corner、filled/outline 规则 | Figma + Designer/Inkscape | 共同完成 | 建 grid、key shapes 和审查模板 | 决定手绘比例与线条个性 | P47 | Not Started |
| `ICON-NAV-*` 导航 icons | Cafe、Build、Menu、Staff、Inventory、Shop、Events、Settings；SVG | Figma + Designer/Inkscape | 共同完成 | 制作初稿与统一化 | 逐个确认含义和风格 | P47 | Not Started |
| `ICON-ACTION-*` 操作 icons | Close、Back、Info、Add、Remove、Edit、Rotate、Move、Store、Confirm、Refresh；SVG | Figma + Designer/Inkscape | Codex + 你 Review | 按 guide 批量制作 | 小尺寸识别 review | P47 | Not Started |
| `ICON-STATE-*` 状态 icons | Locked、New、Alert、Complete、Favorite、Unavailable；SVG | Figma | Codex + 你 Review | 制作 semantic variants | 检查不能只靠颜色表达 | P47 | Not Started |
| `ICON-TIME-*` 时间 icons | Clock、Day、Night、Timer、Calendar；SVG | Figma | Codex + 你 Review | 制作统一图标 | 审查识别度 | P15 / P47 | Not Started |
| `ICON-ACCESS-*` Accessibility icons | Text size、contrast、motion、audio、haptics；SVG | Figma | Codex + 你 Review | 制作与系统设置映射 | 审查含义和可访问性 | P47 / P50 | Not Started |
| `ICON-PLATFORM-*` 平台/输入提示 | Touch、mouse、keyboard、controller；SVG | Figma | Codex + 你 Review | 建 input prompt set | 目标平台实测 | P50 / P51 | Not Started |

## 6. HUD 与资源显示

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `HUD-RESOURCE-CAPSULE` 资源胶囊 | Coins、premium currency、level/XP；9-slice/component | Figma + Unity | Codex + 你 Review | 制作自适应 variants | 检查是否遮挡 Cafe 画面 | P16 / P47 | Not Started |
| `ICON-CURRENCY-*` 货币 icons | Coins、premium、special tokens；SVG/PNG | Designer/Inkscape + Krita | 共同完成 | 出 vector base 与尺寸测试 | 定义品牌化造型 | P16 | Not Started |
| `HUD-DAY-CLOCK` 日期与营业时间 | Day/shift/clock/timer；component | Figma + Unity | Codex + 你 Review | 设计 compact/expanded 状态 | 实际 loop 中检查优先级 | P15 | Not Started |
| `HUD-TASK-TRACKER` 当前目标 | 任务、教程或活动追踪；component | Figma + Unity | Codex + 你 Review | 建折叠、完成和领取状态 | 检查是否造成屏幕拥挤 | P25 / P47 | Not Started |
| `HUD-NOTIFICATION-STACK` 状态提醒 | 缺货、订单完成、员工空闲 | Figma + Unity | Codex + 你 Review | 设计优先级和堆叠规则 | 实机判断打扰程度 | P14–P20 | Not Started |
| `HUD-CONTEXT-ACTION` 场景情境按钮 | 点选家具/员工/顾客后出现 | Figma + Unity | Codex + 你 Review | 做 anchor 和 action variants | MainCafe 操作实测 | P6 onward | Not Started |

## 7. Cafe 建造、装饰与摆放

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `BUILD-CATEGORY-ICON-*` 建造分类 | Tables、chairs、counters、decor、walls、floors、utilities；SVG | Figma + Designer/Inkscape | Codex + 你 Review | 批量制作与统一 | 识别度 review | P6 / P7 | Not Started |
| `BUILD-ITEM-CARD` 家具卡片 | thumbnail、name、price、locked/state；component | Figma + Unity | Codex + 你 Review | 设计 card variants | 检查浏览效率 | P6 | Not Started |
| `BUILD-THUMB-*` 家具缩略图 | 每件家具/装饰的 transparent PNG | Blender + Unity render | 自动生成 + 你 Review | 对应 Phase 可搭批量 render pipeline | 检查角度、光线、裁切 | P6 onward | Not Started |
| `BUILD-PLACEMENT-CONTROLS` 摆放控件 | Rotate、confirm、cancel、store、invalid；SVG/component | Figma + Unity | Codex + 你 Review | 设计状态并接入既有逻辑 | 手机单手操作测试 | P6 / P50 | Existing functional baseline — Art pending |
| `BUILD-GRID-VALIDITY` 合法/非法反馈 | footprint、outline、blocked markers；runtime material/sprite | Unity | Unity/Codex + 你 QA | 制作视觉反馈方案 | 检查清楚但不过亮 | P6 / P7 | Existing functional baseline — Art pending |
| `BUILD-WALL-FLOOR-SWATCH` 墙面/地板 swatch | 材质选择预览；PNG/component | Blender/Krita + Figma | 自动生成 + 你 Review | 建模板与批量整理 | 检查颜色和实际材质一致 | P7 / P30 | Not Started |
| `BUILD-ROOM-MAP` 房间/楼层选择 | 楼层 tab、房间缩略图、锁定状态 | Figma + Unity | Codex + 你 Review | 设计 navigation 和状态 | 检查复杂 Cafe 可理解性 | P34 / P35 | Not Started |

## 8. 订单、顾客与 Cafe 运营

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `ORDER-TICKET` 订单票据 | 顾客、饮品、数量、timer、status；9-slice/component | Figma + Krita | 共同完成 | 做 compact/expanded/late variants | 决定纸张感与信息密度 | P14 | Not Started |
| `ORDER-QUEUE` 订单队列 | 多订单排序、优先级与 ready 状态 | Figma + Unity | Codex + 你 Review | 设计 portrait flow 和 prototype | 检查忙碌时是否易读 | P14 | Not Started |
| `ORDER-ITEM-ICON-*` 订单商品 icons | 饮品、食物、加料小图；PNG/SVG | Blender render / Krita | 自动生成 + 你 Review | 生成/整理缩略图 pipeline | 检查相似商品可区分 | P14 / P19 onward | Not Started |
| `CUSTOMER-STATE-BUBBLE` 顾客气泡 | Ordering、waiting、happy、angry、need help | Figma + Unity | Codex + 你 Review | 设计 bubble 与状态 icon | 场景中检查遮挡 | P14 / P21 | Not Started |
| `PATIENCE-METER` 耐心值 | 顾客等待反馈；component/runtime | Figma + Unity | Codex + 你 Review | 设计渐变、警告和动画规则 | 确认焦虑感不过强 | P14 / P48 | Not Started |
| `STAFF-TASK-BUBBLE` 员工任务气泡 | 制作、清洁、搬运、休息、blocked | Figma + Unity | Codex + 你 Review | 统一 task icon/state | 检查场景可读性 | P22 | Not Started |
| `SERVICE-RESULT` 服务结果 | Tips、rating、bonus、mistake | Figma + Unity | Codex + 你 Review | 建 result variants | 决定正负反馈语气 | P15 / P16 | Not Started |

## 9. 菜单、配方、饮品与食品

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `MENU-CATEGORY-ICON-*` 菜单分类 | Coffee、tea、cold drinks、bakery、specials；SVG | Figma + Designer/Inkscape | 共同完成 | 制作统一 icon set | 检查分类语义 | P19 / P41 | Not Started |
| `RECIPE-CARD` 配方卡 | image、ingredients、price、time、level；component | Figma | Codex + 你 Review | 设计 list/grid/locked variants | 检查信息优先级 | P19 | Not Started |
| `RECIPE-DETAIL` 配方详情 | 大图、介绍、升级效果、材料 | Figma + Krita | 共同完成 | 做页面结构和 prototype | 确认视觉氛围与文案 | P19 | Not Started |
| `FOOD-THUMB-*` 饮品/食品图 | 产品 hero 与 card thumbnail；transparent PNG | Blender render + Krita | 自动生成 + 你 Review | render pipeline、背景清理和尺寸导出 | 每项检查食欲感与辨识度 | P19 / P39 / P41 | Not Started |
| `INGREDIENT-ICON-*` 原料 icons | Beans、milk、tea、flavor、toppings、bakery ingredients；PNG/SVG | Blender/Krita/Designer | 共同完成 | 建统一模板并批量整理 | 审查原料真实性和统一度 | P19 onward | Not Started |
| `RECIPE-LOCK-UNLOCK` 解锁状态 | level、quest、relationship、purchase 条件 | Figma | Codex + 你 Review | 设计多种 unlock rule 模板 | 检查玩家是否看得懂 | P19 / P27 | Not Started |
| `QUALITY-RARITY-MARK` 品质/稀有度 | Beans、recipe、item 等统一等级标识 | Figma | Codex + 你 Review | 建不只依赖颜色的 shape system | 确定正式等级数量和命名 | P27 / P37 | Not Started |

## 10. Inventory、补货与经济

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `INV-ITEM-CARD` Inventory item card | icon、quantity、capacity、expiry/status；component | Figma | Codex + 你 Review | 做 grid/list/selected variants | 检查大库存的浏览效率 | P20 | Not Started |
| `INV-CAPACITY-METER` 库存容量 | 当前/上限、warning、full | Figma + Unity | Codex + 你 Review | 设计状态和动效规格 | 判断提醒强度 | P20 | Not Started |
| `RESTOCK-CARD` 补货卡 | supplier、quantity、price、delivery time | Figma | Codex + 你 Review | 建 bulk stepper 和购买状态 | 检查购买 flow | P20 | Not Started |
| `SHOP-OFFER-CARD` 商店 offer | 单品、bundle、限时、locked | Figma + Krita | 共同完成 | 做模板、价格和状态规则 | 审查是否过度商业化 | P16 / P44 | Not Started |
| `PRICE-TAG` 价格标签 | coins、premium、discount、free | Figma | Codex + 你 Review | 做可复用 variants | 检查价格是否清楚 | P16 | Not Started |
| `DAILY-REPORT` 每日报告 | revenue、costs、tips、ratings、goals | Figma + Unity | Codex + 你 Review | 设计信息架构与 prototype | 确认数据优先级 | P16 | Not Started |
| `ECONOMY-CHART` 简单趋势图 | income/cost/sales trend；component/runtime | Figma + Unity | Codex + 你 Review | 建可读 chart style | 判断是否符合轻量游戏 | P16 / P28 | Not Started |
| `REWARD-PRESENTATION` 奖励展示 | coins、items、unlock、claim | Figma + Unity | Codex + 你 Review | 设计小/大/稀有奖励 variants | 决定庆祝程度 | P16 / P48 | Not Started |

## 11. App Shell、设置与存档

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `BRAND-LOGO` 游戏 logo/wordmark | Title、store、loading；SVG + PNG | Designer/Inkscape + Krita | 你主导 + Codex Assist | 出概念方向、排版与技术清理 | 最终品牌决定与 license review | P18 / P52 | Not Started |
| `TITLE-BACKGROUND` Title screen 背景 | 多比例 illustration/render | Blender + Krita | 你主导 + Codex Assist | 概念构图、render/paint 辅助 | 最终画面与气氛 | P18 | Not Started |
| `LOADING-SCREEN` Loading 画面 | background、tips panel、spinner/progress | Figma + Krita + Unity | 共同完成 | 做模板和 loading states | 选择插画与 tips 呈现 | P18 | Not Started |
| `SAVE-SLOT-CARD` 存档卡 | Cafe name、day、thumbnail、cloud/local state | Figma + Unity | Codex + 你 Review | 设计 normal/conflict/corrupt variants | 检查新手理解度 | P18 / P46 | Not Started |
| `SETTINGS-SCREEN` 设置页面 | Audio、graphics、controls、language、accessibility | Figma | Codex + 你 Review | 设计 sections 和 components | 确认游戏实际选项 | P18 / P47 / P50 | Not Started |
| `PAUSE-MENU` 暂停菜单 | Resume、settings、save、exit | Figma + Unity | Codex + 你 Review | 设计 overlay 与 hierarchy | MainCafe 中检查反差和遮挡 | P18 | Not Started |
| `CONFIRM-DIALOG-*` 系统确认弹窗 | Delete、overwrite、quit、purchase | Figma | Codex + 你 Review | 建通用模板与语义颜色 | 检查文案风险 | P18 | Not Started |
| `CLOUD-SYNC-STATE` Cloud 状态 | Syncing、offline、conflict、success、failure | Figma + Unity | Codex + 你 Review | 建 icon + dialog flow | 若启用在线功能再确认 | P46 | Optional / Not Started |

## 12. 角色、员工、关系与叙事

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `CHAR-PORTRAIT-*` 角色头像 | Profile、dialogue、staff list；transparent PNG | Blender render 或 Krita | 你主导 + Codex Assist | 设计模板、render setup、表情变体整理 | 角色气质和最终表情 | P21 onward | Not Started |
| `CHAR-EXPRESSION-*` 表情组 | Neutral、happy、sad、angry、surprised、tired | Blender/Krita | 你主导 + Codex Assist | 建清单和批量输出规则 | 逐角色演出 review | P21 / P23 | Not Started |
| `DIALOGUE-BOX` 对话框 | Speaker、portrait、text、choices、continue | Figma + Krita | 共同完成 | 设计 component、文本扩展和 prototype | 选择叙事风格 | P21 / P23 | Not Started |
| `SPEECH-BUBBLE` 场景对白 | 短句、thought、reaction | Figma + Unity | Codex + 你 Review | 做 variants 和 anchor rules | 场景遮挡检查 | P21 | Not Started |
| `STAFF-CARD` 员工卡 | portrait、role、energy、skill、task | Figma | Codex + 你 Review | 设计 list/detail variants | 确认数值优先级 | P22 | Not Started |
| `STAFF-ROLE-ICON-*` 员工职责 | Barista、server、cleaner、manager 等 | Figma + Designer/Inkscape | 共同完成 | 做统一 icon set | 角色职责 review | P22 | Not Started |
| `MOOD-ICON-*` 情绪 icons | Happy、calm、stressed、tired、angry | Figma + Krita | 共同完成 | 出易读和较可爱两套方向 | 选手绘程度 | P21 / P22 | Not Started |
| `RELATIONSHIP-METER` 关系值 | Hearts/friendship tiers、next unlock | Figma + Unity | Codex + 你 Review | 设计 tiers 与进度 component | 确定情感表达是否合适 | P23 / P24 | Not Started |
| `CHAR-PROFILE` 角色档案 | Bio、likes、relationship、events | Figma | Codex + 你 Review | 做页面结构 | 决定信息开放顺序 | P23 / P24 | Not Started |
| `CHOICE-BUTTON` 对话选择 | Choice、locked requirement、consequence hint | Figma + Unity | Codex + 你 Review | 做多状态 component | 确认选择信息透明程度 | P25 | Not Started |

## 13. 活动、任务、进度与解锁

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `QUEST-CARD` 任务卡 | Goal、progress、reward、claim、locked | Figma | Codex + 你 Review | 建 daily/story/event variants | 检查目标是否易懂 | P25 | Not Started |
| `EVENT-HUB` 活动页 | Event art、timer、tabs、tasks、rewards | Figma + Krita | 共同完成 | 页面 structure 与 reusable shell | 选择正式活动视觉 | P25 | Not Started |
| `EVENT-BANNER-*` 活动 banner | Hub/header/promo；多比例 PNG | Krita/Affinity Photo | 你主导 + Codex Assist | 概念、构图、文字安全区和变体 | 最终 key art 与版权确认 | P25 onward | Not Started |
| `CALENDAR-REWARD` 签到/日历 | Day states、streak、claim | Figma + Krita | Codex + 你 Review | 做完整日期状态 | 判断是否符合游戏定位 | P25 | Optional / Not Started |
| `PLAYER-LEVEL` 玩家等级 | XP bar、level badge、reward preview | Figma + Unity | Codex + 你 Review | 建 component 和 level-up state | 确认成长节奏表达 | P27 | Not Started |
| `UNLOCK-PRESENTATION` 解锁展示 | Recipe、room、feature、character、decor | Figma + Unity | 共同完成 | 做 modular reveal template | 决定庆祝感和演出长度 | P27 / P48 | Not Started |
| `PROGRESSION-MAP` 进度/发展图 | Milestones、branches、locks | Figma | Codex + 你 Review | 设计可扩展 node system | 检查路线可理解性 | P27 / P28 | Not Started |
| `EXPANSION-CARD` Cafe 扩建 | Cost、requirements、preview、benefits | Figma + Blender render | 共同完成 | 做 card 和前后对比模板 | 判断价值表达 | P28 / P35 | Not Started |

## 14. Exterior、气氛与特殊空间

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `EXTERIOR-MAP-UI` 外部区域导航 | Cafe exterior、locations、locked zones | Figma + Unity | Codex + 你 Review | 设计 map markers 和 flow | 审查世界感与易用性 | P29 / P36 | Not Started |
| `WEATHER-TIME-ICON-*` 天气时间 | Sunny、rain、snow、day、evening、night | Figma + Krita | 共同完成 | 出统一 icon set | 选择自然或可爱程度 | P31 / P32 | Not Started |
| `ATMOSPHERE-OVERLAY-*` 气氛 overlay | 暗角、雨滴、暖光等；PNG/material | Krita + Unity | Unity/Codex + 你 QA | 制作轻量候选与 runtime 参数 | MainCafe 中检查是否影响清晰度 | P31 / P32 | Not Started |
| `SPECIAL-ROOM-ICON-*` 特殊空间 | Kitchen、bakery、storage、staff room 等 | Figma + Designer/Inkscape | 共同完成 | 制作统一 icon | 确认房间定义 | P33 / P34 | Not Started |
| `FLOOR-SELECTOR` 楼层选择器 | Floor tabs、mini-map、locked state | Figma + Unity | Codex + 你 Review | 设计 portrait/mobile interaction | 多楼层实测 | P35 | Not Started |

## 15. Coffee Beans、Syrup、Bakery 与 Merchandise

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `BEAN-ICON-*` 咖啡豆品种 | Bean card、recipe、inventory；PNG/SVG | Blender/Krita | 共同完成 | 统一缩略图模板和输出 | 检查品种区分 | P37 | Not Started |
| `BEAN-CARD` 咖啡豆卡 | Origin、roast、flavor、rarity、stock | Figma | Codex + 你 Review | 设计 compact/detail variants | 确定 flavor 信息量 | P37 | Not Started |
| `BEAN-EXPLORATION-MAP` 豆产地探索 | Locations、routes、rewards、locks | Figma + Krita | 共同完成 | 设计 navigation 和 map base | 选择地图艺术方向 | P38 | Not Started |
| `FLAVOR-NOTE-ICON-*` 风味 icons | Chocolate、nutty、fruit、floral 等 | Figma + Krita | 共同完成 | 做一套统一隐喻 | 审查文化与可理解性 | P37 / P38 | Not Started |
| `SYRUP-ICON-*` Syrup/加料 | Bottle、flavor、inventory；PNG | Blender/Krita | 自动生成 + 你 Review | 批量 render/整理 | 检查颜色和形状区分 | P39 / P40 | Not Started |
| `ADDON-SELECTOR` 加料选择 | Size、milk、syrup、topping、price delta | Figma + Unity | Codex + 你 Review | 设计 step-by-step flow | 检查点单速度 | P39 / P40 | Not Started |
| `BAKERY-ITEM-*` 烘焙食品图 | Display、recipe、inventory；PNG | Blender + Krita | 自动生成 + 你 Review | render pipeline 和 cleanup | 检查食欲感、比例和品类差异 | P41 / P42 | Not Started |
| `BAKERY-CASE-UI` 烘焙展示/补货 | Slots、freshness、stock、sales | Figma + Unity | Codex + 你 Review | 做 display management flow | 检查经营体验 | P41 / P42 | Not Started |
| `MERCH-ITEM-*` 周边商品图 | Mug、bag、beans、souvenir 等；PNG | Blender render | 自动生成 + 你 Review | 批量输出与命名 | 检查品牌一致性 | P43 / P44 | Not Started |
| `MERCH-SHELF-UI` Merchandise 管理 | Shelf slots、stock、price、sales | Figma + Unity | Codex + 你 Review | 设计 card/slot interaction | 检查与 inventory 的一致性 | P43 / P44 | Not Started |

## 16. Offline、Online 与平台系统

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `OFFLINE-REPORT` 离线收益 | Time away、earnings、cap、claim | Figma + Unity | Codex + 你 Review | 设计简洁 report 与 edge states | 审查是否产生压力/误导 | P45 | Not Started |
| `OFFLINE-CAP-ICON` 离线上限提示 | Capacity、upgrade、full | Figma | Codex + 你 Review | 建状态 icon/component | 检查易懂程度 | P45 | Not Started |
| `ONLINE-STATUS` 在线状态 | Online、offline、syncing、maintenance | Figma + Unity | Codex + 你 Review | 设计低打扰状态 | 只有启用 online 时批准 | P46 | Optional / Not Started |
| `ACCOUNT-PANEL` 账号/身份 | Guest、signed-in、link、logout | Figma | Codex + 你 Review | 做安全清晰的 flow | 决定是否需要账号系统 | P46 | Optional / Not Started |
| `MOBILE-SAFE-AREA` 刘海/底部手势适配 | Portrait device templates/spec | Figma + Unity | Codex + 你 Review | 建 device frames 和 layout rules | 多台设备检查 | P50 | Not Started |
| `INPUT-PROMPT-*` 输入提示素材 | Tap、hold、drag、pinch、controller actions | Figma + Unity | Codex + 你 Review | 做 prompt library | 真机操作 review | P50 / P51 | Not Started |

## 17. Tutorial 与 Accessibility

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `TUTORIAL-SPOTLIGHT` 教程聚焦 | Dim overlay、cutout、arrow、caption | Figma + Unity | Codex + 你 Review | 设计多位置适配与 sequence spec | 检查不会遮住必要信息 | P47 / P50 | Not Started |
| `TUTORIAL-GESTURE-*` 手势图 | Tap、drag、rotate、pinch；SVG/animation frames | Figma + Unity | Codex + 你 Review | 制作图形与动画规格 | 真机理解度测试 | P47 / P50 | Not Started |
| `TUTORIAL-CARD` 教学卡 | Illustration、short copy、next/skip | Figma + Krita | 共同完成 | 模板和简单图示 | 文案与节奏 review | P47 | Not Started |
| `FOCUS-STATE` Keyboard/controller focus | Outline、selected、pressed | Figma + Unity | Unity/Codex + 你 QA | 实现 focus visual system | 目标平台检查 | P47 / P51 | Not Started |
| `COLOR-SAFE-STATE` 非颜色状态标记 | Shape、icon、pattern alternatives | Figma | Codex + 你 Review | 审计所有 semantic components | 做可访问性最终判断 | P47 | Not Started |
| `TEXT-SCALE-LAYOUT` 字体放大适配 | 100/125/150% component variants | Figma + Unity | Codex + 你 Review | 建测试页面和 overflow checklist | 检查本地化与手机画面 | P47 / P50 | Not Started |

## 18. 插画、装饰与品牌氛围

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `ILLUS-EMPTY-*` 空状态小插画 | No orders、empty inventory、no staff、no events | Krita/Affinity Photo | 你主导 + Codex Assist | 草图、构图和批量尺寸规划 | 最终画风与角色准确性 | P47 | Not Started |
| `ILLUS-ONBOARDING-*` Onboarding 插画 | First launch / major system introduction | Krita + Blender render | 你主导 + Codex Assist | concept sheet 和 composition | 最终叙事与画面 | P18 / P47 | Not Started |
| `DECOR-CORNER-*` 装饰角花 | 少量用于纸张/menu/event；SVG/PNG | Designer/Inkscape + Krita | 共同完成 | 出低、中、高装饰密度套装 | 控制使用比例 | P47 | Not Started |
| `DECOR-STICKER-*` 小贴纸/印章 | New、special、sold out、staff pick | Figma + Krita | 共同完成 | 做统一 sticker family | 审查是否显得幼稚或杂乱 | P47 | Not Started |
| `PATTERN-BRAND-*` 品牌 pattern | Packaging、loading、empty background | Designer/Inkscape | 共同完成 | 生成可平铺 pattern 和变体 | 选择正式品牌符号 | P47 / P52 | Not Started |
| `PROMO-KEY-ART-*` 商店/宣传 key art | Store capsule、social、announcement；多比例 PNG | Blender + Krita/Affinity Photo | 你主导 + Codex Assist | 构图、尺寸矩阵、草稿与 cleanup | 最终商业发布品质和版权 | P52 | Not Started |

## 19. UI VFX 与 Motion 素材

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `VFX-SPARKLE-*` 闪光/奖励粒子 | Sprite sheet / particle texture | Krita + Unity | Unity/Codex + 你 QA | 制作 texture 候选和 particle setup | 检查频率与高级感 | P48 | Not Started |
| `VFX-CONFETTI-*` 庆祝效果 | Unlock、level up、major reward | Krita + Unity | Unity/Codex + 你 QA | 制作轻/中/强 variants | 决定哪些事件值得使用 | P48 | Not Started |
| `VFX-COIN-*` 货币获得效果 | Coin sprite/mesh + trail/animation | Blender/Krita + Unity | Unity/Codex + 你 QA | 建 runtime animation | 检查速度、数量和性能 | P48 | Not Started |
| `VFX-BUTTON-FEEDBACK` 按钮反馈 | Press scale、highlight、disabled shake | Figma prototype + Unity | Unity/Codex + 你 QA | 定义 motion tokens 并实现 | 手机手感 review | P48 / P50 | Not Started |
| `VFX-PANEL-TRANSITION` 页面转场 | Fade、scale、slide、bottom-sheet motion | Figma prototype + Unity | Unity/Codex + 你 QA | 建统一 duration/easing 规则 | 检查是否拖慢操作 | P48 | Not Started |
| `VFX-FROST-BACKDROP` Frost 动态背景 | Modal/HUD runtime backdrop | Unity | Unity/Codex + 你 QA | 做性能分级：real blur / fake frost / fallback | 在目标设备判断品质与性能 | P47 / P48 / P50 | Not Started |
| `VFX-STATE-PULSE` 状态提示 | Ready、claimable、urgent 的轻量 pulse | Unity | Unity/Codex + 你 QA | 实现可复用 animation preset | 检查不会造成视觉疲劳 | P48 | Not Started |

## 20. 技术交付与 Unity 接入素材

| Asset ID / 素材 | 用途与交付格式 | 推荐软件 | 负责人 | Codex 可以帮忙 | 你需要 review / 完成 | Phase | 状态 |
|---|---|---|---|---|---|---|---|
| `DELIVERY-NAMING` 文件/节点命名 | Figma pages、frames、components、export files、Unity assets | 文档 + Figma | Codex + 你 Review | 制定规则和 lint checklist | 确认是否容易人工维护 | P5 / P47 | Not Started |
| `DELIVERY-9SLICE` 9-slice source set | Panel/button/card borders 与 stretch guides | Figma + Unity Sprite Editor | Codex + 你 Review | 标记 border、测试极限尺寸 | 检查拉伸后材质不变形 | P47 | Not Started |
| `DELIVERY-SPRITE-ATLAS` Sprite Atlas 分组 | Global、feature、event、optional 等 | Unity | Unity/Codex + 你 QA | 规划 atlas 与 packing policy | 检查 memory 和更新便利性 | P47 / P50 | Not Started |
| `DELIVERY-TMP` TextMeshPro 字体资源 | Font asset、fallback、atlas、material presets | Unity + font tools | Unity/Codex + 你 QA | 在字体确定后建立 TMP assets | 确认 license、中文覆盖和清晰度 | P47 / P50 | Not Started |
| `DELIVERY-MATERIAL` UI materials | Frost、soft mask、grayscale、highlight、outline | Unity | Unity/Codex + 你 QA | 编写/配置获批效果与 fallback | 实机画质和性能验收 | P47 / P48 / P50 | Not Started |
| `DELIVERY-PREFAB` UI prefab library | Button、panel、card、modal、toast、HUD 等 | Unity | Unity/Codex + 你 QA | 按批准的 Figma system 建 prefab | 检查与设计一致及可维护性 | P47 / P50 | Not Started |
| `DELIVERY-LOCALIZATION` 本地化适配 | Long text、CJK、RTL readiness、dynamic size | Figma + Unity | Codex + 你 Review | 建 stress-test strings 与 layout test matrix | 决定正式语言范围 | P47 / P50 / P51 | Not Started |
| `DELIVERY-QA-SHEET` 视觉 QA 表 | Screen、device、state、expected、actual、approval | Figma/文档 | Codex + 你 Review | 创建 review board 和检查清单 | 逐屏批准 | P47 onward | Not Started |

## 21. 建议的制作顺序

不建议从几百个 icon 或插画开始。为了减少返工，推荐按以下顺序推进：

1. **Style decision board**：颜色、字体、shape、surface、shadow、frost、icon 与手绘比例。
2. **Core component sample**：只做一个 HUD、一个 panel、一张 card、三个按钮、一个 bottom sheet。
3. **MainCafe context test**：把 sample 放进真实或接近真实的 MainCafe screenshot，检查反差感、可读性和材质是否自然。
4. **Design tokens + component library**：方向批准后，再扩展完整 components 和 icon guide。
5. **优先制作 P6–P20 会最早使用的素材**：建造、订单、HUD、菜单、inventory、daily report。
6. **后续玩法素材按 Roadmap 解锁**：角色、活动、扩建、咖啡豆、bakery、merchandise 等不要一次性全部定稿。
7. **Unity integration**：在对应 Phase 获批后制作 prefab、material、animation、atlas 和 device QA。

## 22. 你不必亲自从零制作的内容

Codex 可以先承担：

- Figma foundations、variables、components、variants 和 prototype 首稿。
- 常规 panel、button、card、tab、badge、progress、toast 等组件系统。
- 基础 SVG icon 草稿及统一化。
- 素材命名、规格、导出矩阵、9-slice 建议与 QA checklist。
- 将批准后的设计转成 Unity prefab/material/animation（必须在对应开发 Phase 获批后）。
- Blender/Unity thumbnail 的自动化流程和批量整理。
- 给插画、texture、banner、logo 提供概念方向、prompt、构图与变体。

你最需要主导的是：

- 游戏最终给人的情绪与品牌印象。
- 是否采用 Frost、纸张、陶瓷、木材或布艺，以及各自所占比例。
- 正式字体、颜色、icon 手绘比例、角色表情和关键插画。
- 每个设计放进真实 MainCafe 画面后是否自然。
- 最终素材的版权、license、商业使用资格和发布品质。

## 23. 当前阶段的完成标准

Phase 5 review 期间，这份清单只需要做到：

- 能追踪所有未来 UI 美术资产，不遗漏重要系统。
- 能明确每种资产由什么软件完成，以及你和 Codex 的分工。
- 允许做少量视觉探索，但不提前锁死后续玩法尚未确定的数据或 UI flow。
- 不把探索稿接入正式 MainCafe，不修改当前 P5 implementation，除非另行批准。

