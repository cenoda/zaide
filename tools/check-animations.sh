#!/usr/bin/env bash
set -e

bad=0
helper="src/App/Shell/Animations.cs"
# Feature-first presentation + shell chrome (Refactor 6.2 M12); exclude Animations helper.
scan_roots=(
  "src/App/Shell"
  "src/Features"
)

scan_grep() {
  local pattern="$1"
  local hits
  hits=$(grep -rnE "$pattern" "${scan_roots[@]}" 2>/dev/null | grep -v "${helper}" || true)
  if [ -n "$hits" ]; then
    printf '%s\n' "$hits"
    return 0
  fi
  return 1
}

if scan_grep 'new LinearEase\('; then
  echo "ERROR: new LinearEase in view code (use CubicEaseOut/In)"
  bad=1
fi

if scan_grep 'new Animation\s*\{'; then
  echo "ERROR: inline new Animation in view code (use Animations helper)"
  bad=1
fi

if scan_grep 'new (DoubleTransition|BrushTransition|TransformOperationsTransition|ThicknessTransition|IntegerTransition|VectorTransition)\s*\('; then
  echo "ERROR: raw Avalonia transition type in view code (use Animations helper)"
  bad=1
fi

if scan_grep 'new Transitions\s*\{'; then
  echo "ERROR: inline Transitions collection in view code (use Animations helper)"
  bad=1
fi

if [ -f "$helper" ]; then
  while IFS= read -r ms; do
    if [ "$ms" -lt 150 ] || [ "$ms" -gt 200 ]; then
      echo "ERROR: $helper has out-of-budget duration ${ms}ms"
      bad=1
    fi
  done < <(grep -oE 'TimeSpan\.FromMilliseconds\([0-9]+\)' "$helper" | grep -oE '[0-9]+')
fi

exit $bad
