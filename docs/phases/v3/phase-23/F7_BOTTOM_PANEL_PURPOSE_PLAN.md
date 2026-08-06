# F7 — 하단 패널 목적 명확화 및 빈 상태 개선

Phase 23 F7 구현 계획

---

## 목표 (Aim of Fix)

사용자가 **하단 패널의 용도를 즉시 이해**하고, 각 모드가 **무엇을 위한 것인지** 알 수 있도록 합니다.

현재 상태:
- 사용자가 "하단 패널이 뭔지 모름"
- Terminal만 사용 가능해 보이고, 나머지 4개 모드는 빈 데드존
- 모드 스트립에 선택 상태 시각 표시가 없음
- 빈 상태에서 다음 행동을 안내하지 않음

목표 상태:
- 사용자가 한 문장으로 답할 수 있음: "하단 패널 = Terminal + 도구 결과 (문제, 빌드 출력, 테스트, 디버그)"
- 활성 모드가 스트립에서 시각적으로 명확히 선택됨
- 빈 패널이 "왜 비어 있는지"와 "다음에 무엇을 해야 하는지"를 설명

---

## 난이도 및 범위

- **난이도**: L (Large)
- **영역**: Shell 하단 패널 전체 (`BottomPanelHost` + 4개 패널)
- **예상 작업량**: 
  - 모드 스트립 선택 상태 스타일링
  - 4개 패널의 빈 상태 카피 추가
  - 선택적: 하단 패널 전체 목적을 설명하는 카피

---

## 작업 항목

### 1. 모드 스트립 선택 상태 스타일링

**현재 상태:**
```csharp
// BottomPanelHost.cs L221-236
private static Button CreateModeButton(...)
{
    var button = new Button
    {
        Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
        // 모든 버튼이 동일한 secondary 색상
    };
}
```

**목표:**
- 활성 모드 버튼이 **다른 색상/스타일**로 표시됩니다.
- VM의 기존 `Is*BottomMode` 플래그를 소비합니다.

**구현 방식:**
```csharp
// BottomPanelHost.WireToViewModel에서 각 버튼의 Foreground를 바인딩
disposables.Add(viewModel.WhenAnyValue(x => x.IsTerminalBottomMode)
    .Subscribe(active => UpdateButtonStyle(terminalButton, active)));

disposables.Add(viewModel.WhenAnyValue(x => x.IsProblemsBottomMode)
    .Subscribe(active => UpdateButtonStyle(problemsButton, active)));

// ... Output, TestResults, Debug도 동일

private static void UpdateButtonStyle(Button button, bool isActive)
{
    button.Foreground = isActive
        ? (IBrush?)Application.Current!.Resources["TextPrimaryBrush"]
        : (IBrush?)Application.Current!.Resources["TextSecondaryBrush"];
    
    // 선택적: 하단 테두리 추가
    button.BorderBrush = isActive
        ? (IBrush?)Application.Current!.Resources["AccentBrush"]
        : Brushes.Transparent;
    button.BorderThickness = isActive
        ? new Thickness(0, 0, 0, 2)
        : Thickness.Parse("0");
}
```

### 2. 빈 상태 카피 추가 (각 패널)

**현재 상태:**
각 패널은 데이터가 없을 때 단순히 빈 `ListBox`만 표시합니다.

**목표:**
각 패널이 빈 상태일 때 **왜 비어 있는지**와 **다음 행동**을 설명하는 카피를 표시합니다.

**ProblemsPanel 빈 상태:**
```
"문제가 없습니다.

코드를 작성하거나 프로젝트를 빌드하면 여기에 문제와 경고가 표시됩니다."
```

**OutputPanel 빈 상태:**
```
"출력이 없습니다.

프로젝트를 빌드하거나 작업플로우를 실행하면 여기에 결과가 표시됩니다."
```

**TestResultsPanel 빈 상태:**
```
"테스트 결과가 없습니다.

테스트를 실행하면 여기에 결과가 표시됩니다."
```

**DebugPanel 빈 상태:**
```
"디버그 세션이 활성 상태가 아닙니다.

디버거를 시작하면 여기에 콘솔 출력, 콜 스택, 변수가 표시됩니다."
```

**구현 방식:**
```csharp
// 각 패널에 빈 상태 패널 추가
// 예: ProblemsPanel.cs

private readonly TextBlock _emptyStateText;

public ProblemsPanel()
{
    _emptyStateText = new TextBlock
    {
        Text = "문제가 없습니다.\n\n코드를 작성하거나 프로젝트를 빌드하면 여기에 문제와 경고가 표시됩니다.",
        TextWrapping = TextWrapping.Wrap,
        Foreground = (IBrush?)Application.Current!.Resources["TextSecondaryBrush"],
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = LayoutTokens.Symmetric(LayoutTokens.SpacingLg, LayoutTokens.SpacingLg),
        IsVisible = false,
    };
    
    // Layout에 추가
    // ...
}

// ViewModel의 데이터가 비어있는지 확인하여 표시
private void UpdateEmptyStateVisibility()
{
    var isEmpty = ViewModel?.Problems.Count == 0;
    _emptyStateText.IsVisible = isEmpty;
    _list.IsVisible = !isEmpty;
}
```

### 3. (선택적) 하단 패널 전체 목적 카피

**옵션 A — 모드 스트립 옆에 서브타이틀:**
```
[Terminal] [Problems] [Output] [Test Results] [Debug]
──────────────────────────────────────────────────────
Terminal + 도구 결과 (문제, 빌드 출력, 테스트, 디버그)
```

**옵션 B — 각 패널 상단에 짧은 설명:**
각 패널 내부 상단에 한 줄 설명을 추가합니다.

**권장:** 옵션 B를 권장합니다. 옵션 A는 공간이 부족하고, 옵션 B는 각 모드 전환 시 문맥을 제공합니다.

---

## 구현 순서

1. **모드 스트립 선택 상태 스타일링** (필수)
   - `BottomPanelHost.WireToViewModel` 수정
   - `UpdateButtonStyle` 헬퍼 메서드 추가
   - 수동 테스트: 모드 전환 시 활성 버튼 색상 변경 확인

2. **빈 상태 카피 추가** (필수)
   - 각 패널(`ProblemsPanel`, `OutputPanel`, `TestResultsPanel`, `DebugPanel`)에 `_emptyStateText` 추가
   - 데이터 상태에 따른 표시/숨김 로직
   - 수동 테스트: 빈 상태에서 카피 표시 확인

3. **테스트 작성** (필수)
   - `Phase23BottomPanelEmptyStateTests.cs`
   - 모드 스트립 선택 상태 스타일링 테스트
   - 빈 상태 카피 표시/숨김 테스트

4. **문서 업데이트** (필수)
   - `TOFIX.md` F7을 "Fixed"로 표시
   - 커밋 해시와 함께 기록

---

## 관련 파일

- `src/App/Shell/BottomPanelHost.cs` — 모드 스트립 및 패널 호스트
- `src/Features/ProjectSystem/Presentation/ProblemsPanel.cs`
- `src/Features/ProjectSystem/Presentation/OutputPanel.cs`
- `src/Features/ProjectSystem/Presentation/TestResultsPanel.cs`
- `src/Features/Debugging/Presentation/DebugPanel.cs`
- `src/App/Shell/MainWindowViewModel.cs` — `Is*BottomMode` 플래그

---

## 검증 기준 (Done When)

- [ ] 사용자가 한 문장으로 답할 수 있음: "하단 패널 = Terminal + 도구 결과"
- [ ] 활성 모드가 스트립에서 시각적으로 명확히 선택됨
- [ ] 빈 패널이 "왜 비어 있는지"를 설명하는 카피 표시
- [ ] 수동 테스트 통과: 모든 모드에서 빈 상태 카피 확인
- [ ] `dotnet test Zaide.slnx --no-build` 통과

---

## 범위 제외 (Non-Goals)

- F6 (하단 패널 레이아웃 분쇄) — 이미 수정됨
- F8 (이미지 파일 열기) — 선택사항, 별도 작업
- 디버거/테스트/빌드 기능 자체 구현 — 이들은 이미 존재, 단지 발견 가능성만 개선
- F7은 IA/empty-state 작업입니다. F6은 레이아웃 문제였습니다. 분리를 유지합니다.
