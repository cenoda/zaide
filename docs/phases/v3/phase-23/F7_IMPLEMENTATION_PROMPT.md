# F7 구현 프롬프트

다음 프롬프트를 사용하여 F7을 구현하세요:

---

## 프롬프트 (Prompt)

```
Phase 23 F7을 구현하세요: 하단 패널 목적 명확화 및 빈 상태 개선

구현 계획: docs/phases/v3/phase-23/F7_BOTTOM_PANEL_PURPOSE_PLAN.md

## 작업 내용

### 1. 모드 스트립 선택 상태 스타일링 (필수)

`src/App/Shell/BottomPanelHost.cs`를 수정하여 활성 모드 버튼이 시각적으로 구별되도록 합니다.

**현재 상태:** 모든 모드 버튼이 `TextSecondaryBrush` 색상을 사용합니다.

**목표:** 활성 모드 버튼이 `TextPrimaryBrush` 색상으로 변경되고, 하단에 `AccentBrush` 테두리가 표시됩니다.

**구현 단계:**
1. `BottomPanelHost.WireToViewModel` 메서드에서 각 버튼의 `Is*BottomMode` 플래그를 구독합니다.
2. `UpdateButtonStyle(Button button, bool isActive)` 헬퍼 메서드를 추가합니다.
3. 활성 버튼은 `TextPrimaryBrush` + 하단 2px `AccentBrush` 테두리
4. 비활성 버튼은 `TextSecondaryBrush` + 투명한 테두리
5. `MainWindowViewModel`의 기존 `IsTerminalBottomMode`, `IsProblemsBottomMode`, `IsOutputBottomMode`, `IsTestResultsBottomMode`, `IsDebugBottomMode` 플래그를 사용합니다.

### 2. 빈 상태 카피 추가 (필수)

각 패널이 빈 상태일 때 설명 카피를 표시합니다.

**ProblemsPanel (`src/Features/ProjectSystem/Presentation/ProblemsPanel.cs`):**
```
"문제가 없습니다.

코드를 작성하거나 프로젝트를 빌드하면 여기에 문제와 경고가 표시됩니다."
```

**OutputPanel (`src/Features/ProjectSystem/Presentation/OutputPanel.cs`):**
```
"출력이 없습니다.

프로젝트를 빌드하거나 작업플로우를 실행하면 여기에 결과가 표시됩니다."
```

**TestResultsPanel (`src/Features/ProjectSystem/Presentation/TestResultsPanel.cs`):**
```
"테스트 결과가 없습니다.

테스트를 실행하면 여기에 결과가 표시됩니다."
```

**DebugPanel (`src/Features/Debugging/Presentation/DebugPanel.cs`):**
```
"디버그 세션이 활성 상태가 아닙니다.

디버거를 시작하면 여기에 콘솔 출력, 콜 스택, 변수가 표시됩니다."
```

**구현 단계:**
1. 각 패널에 `_emptyStateText` (TextBlock)를 추가합니다.
2. `TextWrapping = TextWrapping.Wrap`, `Foreground = TextSecondaryBrush`
3. `HorizontalAlignment = HorizontalAlignment.Center`, `VerticalAlignment = VerticalAlignment.Center`
4. 데이터가 비어있으면 `_emptyStateText.IsVisible = true`, `_list.IsVisible = false`
5. 데이터가 있으면 반대
6. ViewModel의 컬렉션 크기 또는 상태 플래그를 관찰하여 자동 업데이트

### 3. 테스트 작성 (필수)

`tests/Zaide.Tests/App/Shell/Phase23BottomPanelEmptyStateTests.cs`를 생성합니다.

**테스트 케이스:**
1. `BottomPanel_ActiveMode_HasPrimaryBrushAndAccentBorder`
   - `IsTerminalBottomMode = true`일 때 Terminal 버튼의 Foreground가 `TextPrimaryBrush`인지 확인
   - BorderThickness가 `(0, 0, 0, 2)`인지 확인
   - 다른 모드 버튼은 `TextSecondaryBrush` + 투명한 테두리

2. `ProblemsPanel_EmptyWhenNoProblems_ShowsEmptyStateText`
   - `Problems.Count == 0`일 때 `_emptyStateText.IsVisible == true`
   - `_list.IsVisible == false`

3. `OutputPanel_EmptyWhenNoOutput_ShowsEmptyStateText`
4. `TestResultsPanel_EmptyWhenNoResults_ShowsEmptyStateText`
5. `DebugPanel_IdleState_ShowsEmptyStateText`

### 4. 문서 업데이트 (필수)

`docs/phases/v3/phase-23/TOFIX.md`에서 F7 섹션을 업데이트합니다:

```markdown
### F7 — Bottom panel: only Terminal feels usable; other modes have no clear product job

- [x] Fixed (2026-XX-XX) — 모드 스트립 선택 상태 스타일링 + 4개 패널 빈 상태 카피 추가.
      Active mode button now uses TextPrimaryBrush + AccentBrush bottom border.
      Empty panels show explanatory copy with next-action guidance.
      Covered by `Phase23BottomPanelEmptyStateTests`.
```

## 검증

1. `dotnet build Zaide.slnx` 성공
2. `dotnet test Zaide.slnx --no-build` 통과
3. 수동 테스트:
   - 앱 실행 → 하단 패널 열기
   - 각 모드 전환 시 활성 버튼이 시각적으로 구별되는지 확인
   - 각 모드에서 빈 상태 카피가 표시되는지 확인
   - 스크린샷 촬영 (PR에 첨부, 커밋하지 않음)

## 주의사항

- `Is*BottomMode` 플래그는 이미 `MainWindowViewModel`에 존재합니다. 새로 만들지 마세요.
- 빈 상태 카피는 영어로 작성하세요 (프로젝트 규칙: 모든 문서/코드는 영어).
- F7은 IA/empty-state 작업입니다. F6(레이아웃) 작업을 다시 하지 마세요.
- F8(이미지 열기)은 이 작업에 포함하지 마세요.

## 관련 파일

- `src/App/Shell/BottomPanelHost.cs`
- `src/Features/ProjectSystem/Presentation/ProblemsPanel.cs`
- `src/Features/ProjectSystem/Presentation/OutputPanel.cs`
- `src/Features/ProjectSystem/Presentation/TestResultsPanel.cs`
- `src/Features/Debugging/Presentation/DebugPanel.cs`
- `src/App/Shell/MainWindowViewModel.cs`
```

---

## 빠른 시작

위 프롬프트를 복사하여 구현 에이전트에게 전달하세요. 또는 이 프롬프트를 그대로 사용하여 직접 구현을 시작할 수 있습니다.

구현을 시작하려면 이렇게 말하세요:

> "F7 구현을 시작하세요" 또는 "Implement F7"
