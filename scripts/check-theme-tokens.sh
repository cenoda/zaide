#!/usr/bin/env bash
# Refactor 10: enforce no hardcoded color literals in production C# under src/.
#
# Detects:
#   - Color.Parse("#...") hex strings
#   - Color.FromArgb(...) with all-numeric literal channel arguments
#   - Color.FromRgb(...) with all-numeric literal channel arguments
#
# Not flagged (by design):
#   - Computed channels, e.g. Color.FromArgb(0x30, accent.R, accent.G, accent.B)
#   - Path excludes listed in is_allowed_path() below
#
# Run: bash scripts/check-theme-tokens.sh
# Or:  make check-theme-tokens

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
violations=0

is_allowed_path() {
  case "$1" in
    */tests/*|*/tools/*)
      return 0 # test and tool sources are out of scope
      ;;
    */TerminalRenderControl.cs)
      return 0 # ANSI 16-color palette and terminal-local chrome literals
      ;;
    */DesignSystem/TextStyles.cs)
      return 0 # ThemeBinding.GetColor fallback constants when resources are missing
      ;;
    */DesignSystem/Elevation.cs)
      return 0 # BoxShadow fallback values behind ThemeBinding.Resolve
      ;;
    *)
      return 1
      ;;
  esac
}

scan_file() {
  local file="$1"
  awk -v file="$file" '
    function trim(s) {
      sub(/^[ \t\r\n]+/, "", s)
      sub(/[ \t\r\n]+$/, "", s)
      return s
    }

  function args_are_all_numeric_literals(args,    t) {
      t = trim(args)
      return t ~ /^[0-9xX0-9A-Fa-f]+([ \t]*,[ \t]*[0-9xX0-9A-Fa-f]+)*$/
    }

    function report(line, kind, detail) {
      printf "%s:%d:%s\n", file, line, detail
    }

    {
      lines[NR] = $0
    }

    END {
      i = 1
      while (i <= NR) {
        line = lines[i]

        if (match(line, /Color\.Parse[ \t]*\([ \t]*"#[^"]*"/)) {
          report(i, "parse", "Color.Parse hex literal")
          i++
          continue
        }

        if (match(line, /Color\.FromArgb[ \t]*\(/)) {
          start = i
          args = substr(line, RSTART + RLENGTH)
          depth = 1
          while (depth > 0) {
            for (j = 1; j <= length(args); j++) {
              c = substr(args, j, 1)
              if (c == "(") depth++
              else if (c == ")") depth--
            }
            if (depth == 0) break
            i++
            if (i > NR) break
            args = args "\n" lines[i]
          }
          sub(/\)[^)]*$/, "", args)
          if (args_are_all_numeric_literals(args)) {
            report(start, "fromargb", "Color.FromArgb with literal channels")
          }
          i++
          continue
        }

        if (match(line, /Color\.FromRgb[ \t]*\(/)) {
          start = i
          args = substr(line, RSTART + RLENGTH)
          depth = 1
          while (depth > 0) {
            for (j = 1; j <= length(args); j++) {
              c = substr(args, j, 1)
              if (c == "(") depth++
              else if (c == ")") depth--
            }
            if (depth == 0) break
            i++
            if (i > NR) break
            args = args "\n" lines[i]
          }
          sub(/\)[^)]*$/, "", args)
          if (args_are_all_numeric_literals(args)) {
            report(start, "fromrgb", "Color.FromRgb with literal channels")
          }
          i++
          continue
        }

        i++
      }
    }
  ' "$file"
}

while IFS= read -r -d '' file; do
  if is_allowed_path "$file"; then
    continue
  fi

  matches=$(scan_file "$file" || true)
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
  echo "See script header for allowed path excludes and computed-channel patterns."
  exit 1
fi

echo "PASSED: no hardcoded color literals found."
exit 0
