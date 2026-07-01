# Design Rules

Zaide must look like a 2027 native app — Electron-quality polish, Apple-inspired
aesthetic, full OS respect. No "framework defaults" look.

---

## 1. XAML Policy

XAML is a last resort for layout and views. Prefer C# for view construction.

| Tier | When XAML is acceptable |
|------|-------------------------|
| **1. Required** | Framework requires it — `App.axaml` bootstrap, `StyleInclude`, resource dictionaries, theme overrides. |
| **2. Templates & Styles** | DataTemplates, ControlTemplates, Styles — these are native XAML concepts and read better in XAML. |
| **3. Clarity** | A deeply nested static layout where XAML is genuinely more readable than C#. Must include a comment justifying this. |

**Default:** Build views in C#. If a `.axaml` file exists beyond tier 1–2, it must
have a comment at the top explaining why XAML was chosen.

```csharp
// Building a panel in C# — the default approach
var panel = new Border
{
    Background = _glassBrush,
    CornerRadius = new CornerRadius(12),
    Padding = new Thickness(16),
    Child = new StackPanel
    {
        Spacing = 8,
        Children = { _header, _content }
    }
};
```

**Rationale:** C# views are easier for agents to generate, modify, and reason about.
XAML requires name lookups, x:Name references, and is harder to refactor programmatically.

---

## 2. Glass & Blur (Apple-Inspired)

### Goal (aspirational — verify before implementing)

- **Glass surfaces:** Backdrop blur on panels, sidebars, and overlays.
- **Depth:** Active/focused panels are brighter, background panels recede.
- **Borders:** No visible `BorderThickness` on panels. Use shadow or blur falloff.
- **Corner radius:** 10–14px on containers, consistent across all panels.
- **Transparency:** Semi-transparent fills over blurred backdrop.

Target aesthetic: Apple's Vibrant effect (macOS) meets VS Code's modern sidebar.

### Platform Reality

| Platform | Backdrop Blur Support |
|----------|----------------------|
| Windows | Acrylic/Mica via `TransparencyLevel` — works |
| macOS | NSVisualEffectView — works via Avalonia |
| Linux | Compositor-dependent (KDE yes, GNOME partial, tiling WMs no) |

**Rule:** Glass is a progressive enhancement. If blur is unavailable, fall back to
a solid dark semi-transparent fill. Never let the UI look broken without blur.

```csharp
// Fallback pattern
Background = CanUseBlur()
    ? new SolidColorBrush(Color.FromArgb(180, 30, 30, 35))  // semi-transparent over blur
    : new SolidColorBrush(Color.FromArgb(245, 30, 30, 35)); // near-opaque fallback
```

---

## 3. Native OS Respect

- **Window chrome:** Use OS-native title bar. Do not custom-draw window controls.
- **Font:** System UI font, never bundled. Let Avalonia resolve platform default.
- **Scrolling:** OS-native scroll behavior (inertia where platform provides it).
- **Context menus:** Platform-native right-click menus.
- **File dialogs:** OS-native open/save dialogs via Avalonia StorageProvider.
- **Dark mode:** Follow OS dark mode setting. Semi.Avalonia dark theme as base; extend, don't override.

---

## 4. Animation

- **Duration:** All animations 150–200ms. No slower. No instant jump cuts.
- **Easing:** `CubicEaseOut` for appearing, `CubicEaseIn` for disappearing. No linear.
- **Morphing:** Panel resize, tab switch, panel open/close should interpolate smoothly.
- **Avoid:** Spinners, loading bars, flashing. Prefer skeleton states or subtle opacity fades.

```csharp
// Avalonia animation example — panel slide
var animation = new Animation
{
    Duration = TimeSpan.FromMilliseconds(180),
    Easing = new CubicEaseOut(),
    Children =
    {
        new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0.0) } },
        new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1.0) } }
    }
};
```

---

## 5. Spacing & Layout

- **Panel padding:** 16px inner padding minimum on all content containers.
- **Element gap:** 8px between adjacent controls (buttons, inputs, labels).
- **Sidebar width:** Default 260px. Resizable via GridSplitter.
- **Line height:** 1.5× font size for all text content.
- **Icon size:** 16px inline, 20px standalone.
- **Separation:** Panels are separated by 1px gap or subtle opacity difference — not thick borders.
- No element should touch a panel edge without padding.

---

## 6. Reactive UI (Decision: ReactiveUI)

> **MVVM framework chosen: ReactiveUI.** Rationale: Zaide's agent-to-agent model
> requires complex reactive pipelines (throttled streams, observable state merging,
> activation lifecycle). CommunityToolkit.Mvvm is simpler but doesn't provide these.

All UI state must be reactive. ViewModels expose observable properties; Views subscribe and react.

- **Bindings:** `WhenAnyValue`, `Bind`, `OneWayBind` — never manually push state.
- **Activation:** `WhenActivated` for setup/teardown to avoid leaks.
- **Commands:** `ReactiveCommand` for all user actions. Enable/disable states are reactive.
- **Throttling:** User input that triggers expensive work must throttle (150–300ms).
- **Disposal:** All subscriptions use `d.Add(...)` inside `WhenActivated`.

```csharp
// Reactive binding pattern
this.WhenActivated(d =>
{
    d.Add(this.OneWayBind(ViewModel, vm => vm.AgentOutput, v => v._outputText.Text));

    d.Add(this.BindCommand(ViewModel, vm => vm.SendCommand, v => v._sendButton));

    d.Add(this.WhenAnyValue(x => x.ViewModel!.SomeProperty)
        .Subscribe(value => { /* react */ }));
});
```

---

## 7. Visual Quality Baseline

Zaide should feel as polished as VS Code or Warp at a glance:

- **No flicker:** Layout stable on first paint. No cascade of elements appearing.
- **Smooth resize:** No layout jumps or blank areas during window resize.
- **Text rendering:** Crisp at all DPI. No blurry text on HiDPI.
- **Color palette:** Monochromatic dark base with blue accent system (matched to concept.png):
  - **Primary Accent:** `#066ADB` (Bright Blue — for active tabs, primary buttons, focus borders, links)
  - **Secondary Accent:** `#3ED3E4` (Cyan Teal — for code type highlights, secondary indicators)
  - **Warning Accent:** `#FCBB47` (Amber — for warnings, modified indicators, alerts)
  - **Success Accent:** `#28A745` (Green — for added indicators, active status dots, sync)
  - **Deep Base:** `#0E1520` (Near-Black Navy — for window background)
  - **Surface Base:** `#131B27` (Slate Navy — for editor/terminal surfaces)
  - **Panel Surface:** `#1A2330` (Elevated Slate — for inputs, buttons, active highlights)
  - **Glass Base:** `#151E2B` at 78% opacity (translucent sidebar and panel fills)
  - **Glass Panel:** `#1C2635` at 72% opacity (translucent elevated surfaces)
  - **Glass Border:** `#3A4A61` at 35% opacity (subtle glass edge)
  - **Text Active:** `#E8EAF2` (Pale Ice Blue-White — for high-contrast text)
  - **Text Secondary:** `#9AA5B5` (Muted Blue-Gray — for timestamps, line numbers, labels)
  - **Separator:** `#1E2937` (Darkest — for 1px panel separators)
- **Separator style:** Space or 1px semi-transparent line — never 2px+ solid borders.
- **Focus states:** Clear but subtle — slight brightness shift, not thick outline.

---

## 8. Verification Checklist

Before marking any UI task complete:

- [ ] No XAML added beyond tier 1–2 without a justifying comment
- [ ] All animations 150–200ms with cubic easing
- [ ] All panels have ≥ 16px inner padding
- [ ] No visible thick panel borders — separation by gap, shadow, or blur depth
- [ ] Reactive bindings use `WhenActivated` with `d.Add(...)`
- [ ] Font is system-native (not custom/bundled)
- [ ] Glass fallback works on non-composited environments
- [ ] Resize window to 800×600 — no layout breaks
- [ ] Looks like it belongs in 2027, not 2017

---

*Last updated: 2025-06-25*
