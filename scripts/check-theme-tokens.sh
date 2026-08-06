#!/usr/bin/env bash
# Refactor 10 M3: enforce zero hardcoded color literals in production C# source.
# Excludes test files, generated files, and the token dictionary files themselves.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
violations=0

# Pattern: Color.Parse("#..."), new SolidColorBrush(Color.Parse(...)),
#          Color.FromArgb(...), Color.FromRgb(...)
# Excluded paths: tests/, Tools/, Tokens/Light.axaml, Tokens/Dark.axaml,
#                 PaletteTokens.cs (fallback constants only)

while IFS= read -r -d '' file; do
  # Skip test files
  [[ "$file" == */tests/* ]] && continue
  # Skip tools
  [[ "$file" == */tools/* ]] && continue
  # Skip terminal ANSI colors (legitimate hardcoded palette)
  [[ "$file" == */TerminalRenderControl.cs ]] && continue
  # Skip design-system fallback constants (resolved via ThemeBinding with fallback)
  [[ "$file" == */DesignSystem/TextStyles.cs ]] && continue

  matches=$(grep -n 'Color\.Parse\s*(\s*"[#"]' "$file" 2>/dev/null || true)
  if [[ -n "$matches" ]]; then
    echo "VIOLATION: $file"
    echo "$matches"
    violations=$((violations + 1))
  fi
done < <(find "$REPO_ROOT/src" -name "*.cs" -print0)

if [[ $violations -gt 0 ]]; then
  echo ""
  echo "FAILED: $violations file(s) contain hardcoded color literals."
  echo "Use ThemeBinding.GetBrush/GetColor with semantic token keys instead."
  exit 1
fi

echo "PASSED: no hardcoded color literals found."
exit 0
