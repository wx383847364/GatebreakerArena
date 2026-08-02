# Gatebreaker Arena 10:16 固定视口适配方案 v0.1

## 文档定位

本文定义 Gatebreaker Arena 在不同设备屏幕比例下的画面适配规则。目标是以竖屏 `10:16` 为唯一设计基准，使游戏世界、HUD、操作区及交互坐标在其他屏幕比例下保持一致。

本文描述目标方案和实施要求，不表示相关代码与场景配置已经完成。

## 1. 目标与非目标

### 1.1 目标

- 以 `10:16` 作为游戏逻辑视口比例，比例值为 `0.625`。
- 不同屏幕比例下，视口内显示相同的游戏世界范围。
- UI 元素的大小、相对位置和布局关系保持一致。
- 不拉伸画面，不裁切基准内容，不因设备更高或更宽而增加竞技视野。
- 游戏相机、UI 相机及输入坐标使用同一个有效视口。
- 视口之外由独立背景相机填充，默认显示黑色或项目指定的装饰背景。

### 1.2 非目标

- 不要求游戏内容铺满所有设备屏幕。
- 不通过扩大相机视野利用超长屏的额外区域。
- 不针对每一种设备比例单独制作一套 UI。
- 本方案不改变玩法空间、碰撞边界、砖块排列或挡板控制规则。

## 2. 核心决策

采用“固定 10:16 逻辑视口 + Letterbox/Pillarbox”的方案：

- 屏幕比 `10:16` 更宽时，保持完整高度并在左右留边。
- 屏幕比 `10:16` 更高时，保持完整宽度并在上下留边。
- 屏幕正好为 `10:16` 时，视口铺满屏幕。
- 横屏运行时仍显示完整的竖屏 `10:16` 内容，左右会产生较宽留边；若产品不支持横屏，应同时锁定竖屏方向。

只有固定比例并允许留边，才能同时保证“不拉伸、不裁切、不增加视野”。

## 3. 当前实现问题

### 3.1 适配器固定的是正方形

当前文件：

```text
Assets/Scripts/App.AOT/Bootstrap/SquareViewportCameraAdapter.cs
```

`CalculateSquareViewport` 使用屏幕短边生成 `1:1` 视口，与本方案要求的 `10:16` 不一致。该组件应改为固定比例适配器，例如：

```text
FixedAspectViewportCameraAdapter
```

如果为了减少场景引用改动而暂时保留原类名，也必须同步修改类注释、字段命名和测试，避免继续表达“正方形视口”的错误语义。

### 3.2 Canvas 设计分辨率不是 10:16

当前 `BootstrapScene.scene` 中的 Canvas Scaler 使用：

```text
Reference Resolution: 1080 × 1920
```

该比例为 `9:16`。应改为严格的 `10:16`，建议使用：

```text
Reference Resolution: 1000 × 1600
```

如果希望沿用 1080 的设计宽度，也可以使用 `1080 × 1728`。两者只能选择其一并作为后续 UI 标注基准，本文默认使用 `1000 × 1600`。

### 3.3 Overlay Canvas 不受 UI Camera 视口限制

当前 Canvas 使用 `Screen Space - Overlay`。即使给 UI Camera 设置固定 `rect`，Overlay Canvas 仍会覆盖完整物理屏幕，导致 UI 和游戏世界使用不同的有效区域。

Canvas 应改为：

```text
Render Mode: Screen Space - Camera
Render Camera: UI Camera
```

### 3.4 多处代码竞争写入 Camera.rect

当前 `GatebreakerPrototypeRunner` 会在运行期间执行全屏设置：

```csharp
_prototypeCamera.rect = new Rect(0f, 0f, 1f, 1f);
```

同时，现有适配器只在屏幕宽高变化时重新应用视口。因此当 Runner 在分辨率不变时覆盖 `Camera.rect`，适配器不会自动恢复目标视口。

最终实现必须遵守单一所有权：

- 只有固定比例适配器可以写入游戏相机和 UI 相机的 `rect`。
- `GatebreakerPrototypeRunner` 只配置位置、旋转、正交尺寸、裁剪面及渲染层，不再写入 `rect`。
- 如果短期内无法移除其他写入点，适配器至少要比较相机当前 `rect` 与目标值，并在不一致时恢复；这只能作为迁移措施，不作为最终结构。

## 4. 视口计算规则

设：

```text
目标比例 targetAspect = 10 / 16 = 0.625
屏幕比例 screenAspect = screenWidth / screenHeight
```

### 4.1 屏幕更宽

当：

```text
screenAspect > targetAspect
```

保持完整高度，计算归一化视口宽度：

```text
viewportWidth = targetAspect / screenAspect
x = (1 - viewportWidth) / 2
y = 0
width = viewportWidth
height = 1
```

### 4.2 屏幕更高

当：

```text
screenAspect <= targetAspect
```

保持完整宽度，计算归一化视口高度：

```text
viewportHeight = screenAspect / targetAspect
x = 0
y = (1 - viewportHeight) / 2
width = 1
height = viewportHeight
```

### 4.3 参考实现

```csharp
using UnityEngine;

namespace App.AOT.Bootstrap
{
    public sealed class FixedAspectViewportCameraAdapter : MonoBehaviour
    {
        public const float ReferenceAspect = 10f / 16f;

        [SerializeField]
        private Camera[] _cameras;

        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;

        private void Awake()
        {
            Apply(true);
        }

        private void OnEnable()
        {
            Apply(true);
        }

        private void LateUpdate()
        {
            Apply(false);
        }

        private void Apply(bool force)
        {
            int screenWidth = Mathf.Max(Screen.width, 1);
            int screenHeight = Mathf.Max(Screen.height, 1);
            Rect targetRect = CalculateReferenceViewport(screenWidth, screenHeight);

            bool resolutionChanged =
                screenWidth != _lastScreenWidth ||
                screenHeight != _lastScreenHeight;

            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;

            if (_cameras == null)
            {
                return;
            }

            for (int i = 0; i < _cameras.Length; i++)
            {
                Camera targetCamera = _cameras[i];
                if (targetCamera == null)
                {
                    continue;
                }

                if (force || resolutionChanged || targetCamera.rect != targetRect)
                {
                    targetCamera.rect = targetRect;
                }
            }
        }

        public static Rect CalculateReferenceViewport(
            int screenWidth,
            int screenHeight)
        {
            float width = Mathf.Max(screenWidth, 1);
            float height = Mathf.Max(screenHeight, 1);
            float screenAspect = width / height;

            if (screenAspect > ReferenceAspect)
            {
                float viewportWidth = ReferenceAspect / screenAspect;
                return new Rect(
                    (1f - viewportWidth) * 0.5f,
                    0f,
                    viewportWidth,
                    1f);
            }

            float viewportHeight = screenAspect / ReferenceAspect;
            return new Rect(
                0f,
                (1f - viewportHeight) * 0.5f,
                1f,
                viewportHeight);
        }
    }
}
```

最终落地时，应在消除其他 `Camera.rect` 写入点后评估是否还需要每帧比较。若视口所有权已经完全收敛，可只在初始化、启用、分辨率变化和设备方向变化时更新。

## 5. 相机与 Canvas 配置

### 5.1 相机层级

推荐结构：

```text
全设备屏幕
├── Letterbox Camera（全屏）
└── 居中的 10:16 固定视口
    ├── Main Camera（游戏世界）
    └── UI Camera（Canvas）
```

### 5.2 Letterbox Camera

```text
Viewport Rect: (0, 0, 1, 1)
Depth: 低于 Main Camera
Culling Mask: Nothing
Clear Flags: Solid Color
Background: 黑色或指定背景色
```

Letterbox Camera 不加入固定比例适配器。

### 5.3 Main Camera

- 加入固定比例适配器的 `_cameras` 数组。
- 保持正交投影。
- 正交尺寸以 `10:16` 视口为输入进行计算。
- 禁止根据完整物理屏幕比例扩大玩法视野。

### 5.4 UI Camera

- 加入固定比例适配器的 `_cameras` 数组。
- `Depth` 高于 Main Camera。
- 只渲染 UI Layer。
- Clear Flags 使用不覆盖游戏世界的配置。

### 5.5 Canvas

```text
Render Mode: Screen Space - Camera
Render Camera: UI Camera
Pixel Perfect: 按像素素材实机效果决定

Canvas Scaler
  UI Scale Mode: Scale With Screen Size
  Reference Resolution: 1000 × 1600
  Screen Match Mode: Match Width Or Height
  Match: 0.5
```

由于 UI Camera 的有效视口始终为 `10:16`，Canvas 的设计比例与实际有效区域一致。`Match = 0.5` 用于表达宽高等权，并避免未来配置漂移时只偏向某个轴。

## 6. UI 布局约束

固定视口解决整体比例问题，Canvas 内部仍需使用正确的 RectTransform 锚点：

| UI 区域 | 推荐锚点 | 说明 |
|---|---|---|
| 顶部状态区 | Top / Top Stretch | 距离逻辑视口顶部固定 |
| 中央计时与暂停区 | Middle / Horizontal Stretch | 始终位于逻辑视口中央 |
| 砖块和核心提示 | Middle | 与玩法区域中心对应 |
| 底部移动控制区 | Bottom | 距离逻辑视口底部固定 |
| 全屏弹窗 | Stretch | 只覆盖 10:16 有效视口 |

禁止使用以下方式补偿屏幕比例：

- 根据 `Screen.width` 或 `Screen.height` 手工缩放单个 UI。
- 对 UI 根节点使用非等比缩放。
- 在超长屏上移动 HUD 以占用黑边区域。
- 让关键按钮、比分或玩法提示出现在 10:16 视口之外。

## 7. 输入坐标要求

### 7.1 Unity UI 输入

Canvas 改为 `Screen Space - Camera` 并正确绑定 UI Camera 后，`GraphicRaycaster` 和 EventSystem 可使用相机有效视口处理 UI 点击。

必须实测留边区域：

- 点击黑边不得触发视口内按钮。
- 拖动从视口内进入黑边时，不应产生异常位置跳变。
- 左右移动按钮和暂停按钮的触控区域应与视觉位置一致。

### 7.2 游戏世界输入

使用目标游戏相机执行：

```csharp
camera.ScreenToWorldPoint(screenPosition);
```

如果项目自行计算归一化屏幕坐标，必须先相对 `camera.pixelRect` 转换，而不能直接除以完整的 `Screen.width` 和 `Screen.height`：

```csharp
Vector2 viewportPosition = new Vector2(
    (screenPosition.x - camera.pixelRect.x) / camera.pixelRect.width,
    (screenPosition.y - camera.pixelRect.y) / camera.pixelRect.height);
```

输入点不在 `camera.pixelRect` 内时应忽略。

## 8. Safe Area 策略

固定 10:16 与刘海、安全区是两个独立问题。

第一阶段建议先实现以完整屏幕为边界的固定 `10:16` 视口，确保竞技画面一致。第二阶段再根据移动设备验收结果选择以下策略之一：

1. **安全区内嵌 10:16 视口**：先取得 `Screen.safeArea`，再在安全区内部计算最大的 `10:16` 矩形。内容最安全，但黑边可能不对称或增加。
2. **视口保持屏幕居中，关键 UI 避让**：游戏世界仍按完整屏幕居中，只有关键按钮和文字进入 Safe Area 容器。画面更稳定，但需确认刘海不会遮挡玩法对象。

竞技相关内容不得因机型不同获得额外可视范围。推荐优先采用“安全区内嵌 10:16 视口”。

## 9. 分辨率示例

| 物理分辨率 | 物理比例 | 目标结果 |
|---|---:|---|
| 1000 × 1600 | 10:16 | 全屏显示，无留边 |
| 900 × 1600 | 9:16 | 视口高度为屏幕的 90%，上下各留 5% |
| 1080 × 1920 | 9:16 | 视口高度为屏幕的 90%，上下各留 5% |
| 1080 × 2340 | 9:19.5 | 视口高度约为屏幕的 73.85%，上下各留约 13.08% |
| 1200 × 1600 | 3:4 | 视口宽度约为屏幕的 83.33%，左右各留约 8.33% |
| 1600 × 1000 | 横屏 16:10 | 保留竖屏 10:16 视口，左右留边 |

## 10. 实施步骤

1. 将 `SquareViewportCameraAdapter` 改造或替换为固定 `10:16` 适配器。
2. 为视口计算函数增加 EditMode 单元测试。
3. 在 `BootstrapScene.scene` 中把 Main Camera 和 UI Camera 都绑定到适配器。
4. 保持 Letterbox Camera 全屏，不加入适配器。
5. 将 Canvas 改为 `Screen Space - Camera` 并绑定 UI Camera。
6. 将 Canvas Scaler 的参考分辨率改为 `1000 × 1600`。
7. 删除 `GatebreakerPrototypeRunner` 及其他脚本中对 `Camera.rect` 的写入。
8. 检查玩法相机正交尺寸是否仍使用完整屏幕 `aspect`，必要时改为固定视口后的相机比例。
9. 检查所有局内 UI 的锚点和拉伸规则。
10. 检查所有基于完整屏幕宽高的输入或 UI 坐标计算。
11. 使用验收矩阵逐项测试编辑器、Android 和微信小游戏。

## 11. 自动化测试建议

至少覆盖以下计算用例：

```text
1000 × 1600 -> Rect(0, 0, 1, 1)
900 × 1600  -> Rect(0, 0.05, 1, 0.9)
1200 × 1600 -> Rect(约0.08333, 0, 约0.83333, 1)
0 × 0       -> 不产生 NaN 或 Infinity
负数输入     -> 按最小值 1 处理，不产生非法 Rect
```

还应增加 PlayMode 检查：

- Main Camera 与 UI Camera 的 `rect` 一致。
- Letterbox Camera 的 `rect` 保持全屏。
- Canvas Render Mode 为 `Screen Space - Camera`。
- Canvas 绑定的相机为 UI Camera。
- 运行玩法初始化后，相机 `rect` 不会被重置成全屏。

## 12. 视觉与交互验收标准

以 `1000 × 1600` 截图作为基准，对其他比例截图进行对比。

### 12.1 游戏世界

- 顶部和底部核心位置一致。
- 场地左右边界、砖块行列和挡板大小一致。
- 不因屏幕变高显示更多场地。
- 不因屏幕变窄裁掉左右内容。
- 游戏对象没有横向或纵向拉伸。

### 12.2 UI

- 中央 HUD 始终位于 10:16 视口中心。
- 顶部状态、比分、暂停按钮和底部控制区相对位置一致。
- 字号、图标尺寸和按钮触控尺寸一致。
- 全屏遮罩只覆盖有效视口，是否覆盖外部黑边由产品表现统一决定。

### 12.3 输入

- 所有按钮点击位置与视觉位置一致。
- 黑边区域不会触发玩法输入。
- Android 触摸、鼠标和微信小游戏指针输入结果一致。

### 12.4 推荐验收分辨率

```text
1000 × 1600    基准 10:16
900 × 1600     9:16
1080 × 1920    常见 9:16
1080 × 2160    9:18
1080 × 2340    9:19.5
1440 × 3200    9:20
1200 × 1600    较宽竖屏
1600 × 1000    横屏容错
```

## 13. 风险与注意事项

- 如果只改 Main Camera，不改 Overlay Canvas，UI 仍会随完整屏幕变化。
- 如果只改 Canvas Scaler，不固定相机视口，游戏世界仍会获得不同视野。
- 如果 Runner 继续写 `Camera.rect`，进入不同玩法后会重新变成全屏。
- 如果正交尺寸在设置视口之前计算，读取到的 `camera.aspect` 可能不是目标比例。
- 如果将黑边也作为可交互区域，可能出现看不见但能点击的控件。
- Pixel Perfect 与非整数缩放可能造成像素素材抖动，需要在目标设备上单独确认。
- 微信小游戏窗口尺寸和设备方向变化可能发生在运行中，适配器必须响应尺寸变化。

## 14. 完成情况

- 当前状态：未开始
- 进度说明：固定 10:16 视口方案、配置要求和验收标准已形成文档；代码、场景、测试和实机验收尚未实施。
- 最近更新：2026-08-02，完成初版方案，明确固定视口算法、相机与 Canvas 配置、输入边界、Safe Area 策略及验收矩阵。

