#!/usr/bin/env bash
set -e

bad=0
helper="src/Views/Animations.cs"

if grep -rnE 'new LinearEase\(' src/Views/ | grep -v "${helper}"; then
  echo "ERROR: new LinearEase in view code (use CubicEaseOut/In)"
  bad=1
fi

if grep -rnE 'new Animation\s*\{' src/Views/ | grep -v "${helper}"; then
  echo "ERROR: inline new Animation in view code (use Animations helper)"
  bad=1
fi

if grep -rnE 'new (DoubleTransition|BrushTransition|TransformOperationsTransition|ThicknessTransition|IntegerTransition|VectorTransition)\s*\(' src/Views/ | grep -v "${helper}"; then
  echo "ERROR: raw Avalonia transition type in view code (use Animations helper)"
  bad=1
fi

if grep -rnE 'new Transitions\s*\{' src/Views/ | grep -v "${helper}"; then
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
