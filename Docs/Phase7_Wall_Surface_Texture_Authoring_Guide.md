# Phase 7 Wall Surface Texture Authoring Guide

## Floor Surface contract / 地板贴图合同

- 每张 Floor texture 的世界尺寸固定为 `1 Grid = 1 m × 1 m`。
- 上、下、左、右四条边都必须 seamless；相同 texture 与相同 rotation 连续铺设时不可出现接缝或异常宽边。
- 一个 tile 内可以包含木板、砖块、纯色或其他任意 pattern units，但边缘必须能二维循环。
- Floor tile 只允许按 `90°` increments 改变 texture orientation；不同 rotation 的交界可作为玩家刻意制作的方向分界。
- Texture Import Settings 使用 `Wrap Mode = Repeat`。贴图不得创建额外 geometry、Collider、occupancy 或 Navigation obstacle。

> 状态：Approved Texture Authoring Reference
> 适用范围：Phase 7 — Interior Walls & Surface Customization
> 最后更新：2026-08-24

## 1. 文档目的

本文档定义 Phase 7 `Wallpaper` 与 `Wainscoting` 的 texture authoring contract，供以下工作共同引用：

- 制作、生成、购买或筛选 Surface textures；
- Unity Texture Import 与 Material 设置；
- Wall Surface renderer、UV 和 tiling 实现；
- 自动验证与 Studio Owner Play Mode Visual Acceptance。

本文档只约束视觉 Surface 素材，不授权提前实现 Phase 7 runtime、Save、UI 或 Decoration interaction。

## 2. 通用尺寸规则

- 每张 `Wallpaper` 或 `Wainscoting` texture 在 World 中的宽度固定对应 `1 Wall Grid column = 1 m`。
- 初始墙面有 8 columns，因此同一 texture 在整面墙上横向重复 8 次。
- Texture 不通过任意拉伸覆盖整面墙；Renderer 必须保持每个 Grid column 相同的 pattern scale。
- 一张 texture 内可以包含多个等宽 pattern units。例如 Wainscoting 可以在 `1 m` 宽度内包含 4 块等宽木板；整面 8-column 墙最终显示 32 块木板。

## 3. Seamless Repeat 规则

- 木板、纯色纹理、花纹或其他图案都可以使用，但左右边缘必须 `seamless / tileable`。
- 两张 texture 横向拼接后，边界处的图案间距必须与 texture 内部一致。
- 拼接处不能出现：
  - 异常宽板或异常窄板；
  - 双线、粗线或缺失接缝；
  - 图案断裂、突然位移或比例变化；
  - 明显色差、亮度跳变或空白边缘。
- 木板类 texture 的左右边缘必须共同组成一个正常接缝。不能在左右两侧各保留额外半块宽空白，导致拼接后形成双宽木板。
- 腰线与地线如果存在，也必须在左右边缘保持相同高度、厚度、颜色和明暗关系。
- 默认只允许横向 repeat；包含固定上下结构的 texture 不得纵向 repeat。

## 4. Wallpaper 规则

- 每张 Wallpaper texture 的 World 宽度对应 `1 Wall Grid column = 1 m`。
- Wallpaper texture 的上下内容必须映射到完整墙面高度，从墙底到墙顶填满，不留下未覆盖区域。
- Wallpaper 默认只横向循环，不上下循环。
- Wallpaper 可以使用植物、几何、木板、织物感、纯色纹理或其他可读图案，但必须符合第 3 节的 Seamless Repeat 规则。
- 启用 Wainscoting 时，Wainscoting 可以覆盖 Wallpaper 的下部显示区域；Wallpaper 本身仍保持完整墙高映射，不能因为 Wainscoting 开启而改变 pattern scale。

## 5. Wainscoting 规则

- 每张 Wainscoting texture 的 World 宽度对应 `1 Wall Grid column = 1 m`。
- Wainscoting 从地面向上显示，整体 World 高度必须与项目标准角色的腰部高度一致。
- 角色腰部高度由统一的 standard character waist reference 决定；不同 Wainscoting textures 不得自行使用不同高度。
- Wainscoting 可以有腰线，也可以没有腰线。
- 如果有腰线，腰线必须位于 texture 顶部，并沿左右边缘连续。
- Wainscoting 可以有地线／base trim，也可以没有。
- 如果有地线，地线必须位于 texture 底部，并沿左右边缘连续。
- 腰线、主体与地线必须共同组成一张完整高度的 Wainscoting texture；该 texture 不得纵向 repeat。
- Wainscoting 只改变 Surface appearance，不创建额外 wall geometry、Collider、Grid Occupancy 或 Navigation obstacle。

## 6. Unity 映射规则

- `Texture Wrap Mode` 使用 `Repeat`，但 runtime 只调整横向 tiling。
- 横向 tiling 数量必须与墙面 Grid columns 对应；初始 8-column 墙使用 8 次 `1 m` texture repeat。
- 纵向 tiling 保持 1，确保 Wallpaper 上下覆盖一次完整墙高，Wainscoting 上下结构也只显示一次。
- Wallpaper 与 Wainscoting 的显示边界必须精确相接，不能留下可见 geometry gap。
- Surface Material 不得静默改变 wall geometry、Door、Window、Wall Slot occupancy、Floor Grid occupancy 或 Navigation。

## 7. 验证要求

### 7.1 Texture 预检

- 至少把同一 texture 横向排列 3 次，检查 repeat boundary。
- 木板或规则图案必须逐一比较内部间距与跨 texture 边界间距。
- 检查左右边缘颜色、亮度、线条厚度和 pattern phase 是否连续。
- 检查腰线与地线是否在拼接处保持同一高度和厚度。

### 7.2 Unity 验证

- 在完整 8-column 墙面检查实际 Material tiling。
- 检查 Wallpaper 是否填满完整墙高。
- 检查 Wainscoting 是否从地面延伸到统一角色腰部 reference。
- 检查 Wallpaper 与 Wainscoting 之间没有空隙、重叠闪烁或 UV 拉伸。
- 检查墙角、不同 Camera 距离、实际 Lighting 与目标 resolution。
- 验证 Surface appearance 不增加 Collider，不改变 Occupancy 或 Navigation。

### 7.3 Acceptance 边界

- 自动验证负责 texture settings、tiling、边界数值与 Scene contract。
- 接缝是否自然、图案是否美观、腰线／地线是否合适，必须由 Studio Owner 在 Unity Play Mode 完成 `Visual Acceptance`。

## 8. Phase 7 已确认示例

- Wainscoting 可以采用 `1 m` texture 内 4 块等宽木板的 modular pattern。
- 初始 8-column 墙横向 repeat 8 次，最终显示 32 块等宽木板。
- 已确认的参考方向使用暖白色低对比木纹、简约顶部腰线、无额外 geometry 和无 Collider。

## 9. Formal Wall-mounted model intake reference

- Author source (`.glb`) is preserved byte-for-byte under `Assets/Art/Phase7/RawSources`; the deterministic Blender script creates Unity-native derived FBX without editing source Mesh data.
- Every production Prefab uses a stable root at position `(0,0,0)`, identity rotation and scale `(1,1,1)`. Axis, bottom pivot, wall-plane offset and depth corrections belong only to the `Visual` child wrapper.
- Visible bottom is `y = 0`; the back wall plane is `min z = 0`; front width/height stays inside the declared integer footprint; maximum wall-normal depth is `0.35 m`.
- Selection uses trigger Collider geometry only. Formal wall-mounted Prefabs contain no `Rigidbody`, character-blocking Collider or `NavMeshObstacle`.
- Embedded GLB BaseColor images are extracted as derived textures, capped at `1024 px` on the longest edge and explicitly bound to URP Materials. Painting portrait alpha is imported from the supplied PNG. Window glass uses a named transparent technical Material.
- Thumbnail rendering uses one deterministic Editor Camera/light/background setup and pre-generated Sprite output. Production Catalogue cards never use placeholder images or runtime `RenderTexture` Cameras.
- Source path and SHA-256 provenance is recorded in `ArtSource/Phase7/FormalAssetProvenance.json`.
- Repeatable builds use only repository authority under `Assets/Art/Phase7/RawSources` and `ArtSource/Phase7/Derived`; the external Studio Owner drop folder is intake-only and is not required to rebuild or validate the project on another machine.
- MainCafe has no authored startup Window seed. Both Window sizes are catalogue-only and appear only after the player confirms a placement in the current session. Reloading the Scene clears those session placements; persistent player Save/Load is deferred to its owning phase.
- Technical validation closes dimensions, IDs, import, UV, Material, Collider and Navigation contracts only. Appearance, shelf sizing and overall fit remain `Studio Owner Visual Acceptance` decisions.
