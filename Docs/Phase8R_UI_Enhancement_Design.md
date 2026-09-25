# Phase 8R：时间控制、准备状态与收纳提示

状态：2026-09-23 已按用户确认方案实施，受影响自动化回归及真实 Game View 截图验证通过，待用户体验检查。

## 已批准的当前方案

1. 保留两个时间按钮，不显示速度文字。单/双三角按相同可见高度等比显示。现有桃色tab_selected表示当前状态，奶油tab_idle表示非当前，灰米tab_unavailable表示Decor锁定。
2. 1×点击左按钮暂停，右按钮2×；2×点击左按钮1×，再次点击右按钮暂停；暂停时左按钮显示pause并固定返回1×，右按钮直接2×。Decor前Normal/Paused锁左，Fast锁右；退出由原有pause lease恢复进入前状态。
3. Checklist无底板、标题、折叠按钮，位于Normal/Decoration标识下方左侧，固定三类row。空心圆表示尚未放置，绿色勾表示有效，暖黄色感叹号表示已放置但需调整，无红色。使用用户确认的透明PNG原图切片；列表只读confirmed状态，不吃Mouse/Touch。保留无效原因、额外无效实例数量；三类有效但未连通时保留简短Connect提示。
4. 桌面上仍有内容时，点击Store立即显示原有奶油modal：Clear the counter first；Move or store these items before storing this counter:；实际阻挡物品清单；单个Got it按钮。无需先确认再失败，不自动收纳附属物，关闭后保持桌子编辑状态。
5. Furniture分类与原Pickup入口保持原方案；不恢复已撤回的Opening essentials说明区。

## 实施边界

在codex/p8r-existing-feature-enhancement worktree内修改。复用已确认的game-ui-design/game-ui-ux方案，不更改原有图片、字体、材质、Scene或Prefab，不进入Phase9。用户已授权将本轮改动 commit/push 到现有 P8R remote branch。

主要文件：TimeControlPanel/P8RButtonLayout处理时间与图标；ValidationMessageView/P8RAppearance处理三行提示；P8RStatusIcons和独立Resources PNG提供新批准icon；DecorationModeController/DecorationStoreModalView处理受阻提示。英文文案进入既有JSON。

## 验证

新增测试先RED再实现。验证三角等高、暂停固定1×、Decor恢复；confirmed报告与三类icon；无底板/无交互/左侧Safe Area；Store阻挡前置提示、真实物品数量、Got it/Back/owner shutdown不执行Store且保留选中状态。真实Game View截图用于视觉检查，不能等同真实手机验收。

验证结果：239 个不同 PlayMode 测试通过，1 个 opt-in capture 跳过；36 个 EditMode 测试通过；另行启用的真实 Editor 截图测试 1 项通过。最后 Store 回归 96 项通过，已包含在 239 项内。此次未重跑全套测试，也未进行实体手机验收。详见 outputs/p8r-ui-enhancement/VERIFICATION.md。

## 2026-09-23 可读性微调

用户批准：Checklist 文字和三态 icon 增加细奶油色外缘，保持无面板；2×用两个现有单三角组成，每个三角的高度、线宽与1×一致，间隔2 UI单位。字号、颜色、交互不变。文字使用当前清单专用材质，icon使用内存透明度遮罩；不写入原图或共用字体材质。

## 2026-09-23 按模式显示准备状态

完整三行 checklist 仅在 Decor mode 显示。Normal mode 若 confirmed layout 尚不能开业，只显示简短的 Setup incomplete · Enter Decor to finish；能开业后隐藏。沿用现有颜色、字体和描边；通路连通性纳入完成判断，切换模式不修改原始 report 或诊断。

## 已批准的 compact panel 方案

Decor checklist 使用现有 panel_cream 的半透明底色（alpha 0.75）和独立不透明边框，复用原图而不修改源素材。字体12 logical units，状态icon14，行距4，四周内边距8。面板左侧与模式按钮对齐，距HUD底部8。清单文字及icon不再描边；Normal简短提醒逻辑保持。时间控制以现有 button_secondary_normal 的 border覆盖层统一边框，保留速度高亮及锁定底色。

用户要求后续 example 使用1080×1920；真实Native UI example capture按此分辨率执行，logical viewport 360×640，不能用生成图替代运行时验收。

## 场地内拖动限制

普通 furniture 沿用现有完整占地 clamp。CR、CM、Pickup Point 离开桌面后的地面悬浮预览限制在 grid bounds 内，拖出四边/角落时停在最近场内格；无桌面时初始悬浮位置也限制在场内。仅修正预览位置，不允许在地面确认功能设施，不改变 confirmed layout、readiness 或有效 Slot 规则。

## 2026-09-24 Normal incomplete 提示面板

Normal 的 incomplete 提示复用 Decor checklist 的半透明奶油底板和不透明边框，四周内边距同为8 UI单位。保留现有字号、文案及左侧对齐，取消已不需要的文字描边。Normal readiness 完成时文字和面板一起隐藏，所有图形仍允许场景输入穿透。

## 2026-09-24 加粗文字与固定分类栏

Normal/Decoration 模式标识、Decor/Done 按钮与 Normal incomplete 提示使用 Bold，并在既有 panel 内居中。目录四个 tab 和右侧箭头统一为4 logical units间距，箭头底板与 tab 同高。移除 P8R 的 Add Another 显示，保留同一个箭头按钮切换展开/收起及方向；收起后四个 tab 保持横向位置和宽度。沿用原素材及48-unit触控高度，legacy 界面保留原入口。

相关 PlayMode 回归19项通过，覆盖手机、小手机、横屏和平板布局、分类栏状态切换及家具操作；不代表全套回归或实体手机验收。

## 2026-09-24 Floor 范围与提示面板

Whole Room / Single Grid 仅随展开目录显示，收起后隐藏并停止拦截点击；已有 Apply/Cancel 预览操作保留。两种范围按较宽的粗体文案预留相同宽度，并固定可见底板尺寸，选中切换不再伸缩。场景指引使用对称文本内边距、居中字形及垂直居中的信息icon；面板底色alpha 0.75，复用原素材的独立不透明边框。
