#!/usr/bin/env bash
#
# Hapi's Havoc - test harness runner.
#
#   ./tools/run-tests.sh                    # whole PlayMode suite
#   ./tools/run-tests.sh DeterminismGate    # NUnit filter substring
#   HAPI_TEST_PLATFORM=EditMode ./tools/run-tests.sh ThemeAuthoring
#
# PlayMode is the default and is what "the suite" means. EditMode exists for the handful of
# things that need AssetDatabase - asset authoring - which cannot run in PlayMode and must not
# be done by opening the Editor, since the harness requires it closed.
#
# -batchmode WITHOUT -nographics is deliberate and load-bearing. -nographics kills the
# render loop and every capture comes back black, which makes image comparisons pass
# vacuously. Do not "optimise" it in.
#
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' "$REPO/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
UNITY="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/MacOS/Unity"
OUT="$REPO/TestOutput"
FILTER="${1:-}"
PLATFORM="${HAPI_TEST_PLATFORM:-PlayMode}"

red()   { printf '\033[31m%s\033[0m\n' "$*"; }
green() { printf '\033[32m%s\033[0m\n' "$*"; }
bold()  { printf '\033[1m%s\033[0m\n'  "$*"; }

# --- Unity present? ---------------------------------------------------------
if [[ ! -x "$UNITY" ]]; then
  red "Unity $UNITY_VERSION not found at:"
  red "  $UNITY"
  echo
  echo "Installed editors:"
  ls -1 /Applications/Unity/Hub/Editor/ 2>/dev/null | sed 's/^/  /' || echo "  (none)"
  echo
  echo "The project expects $UNITY_VERSION (ProjectSettings/ProjectVersion.txt)."
  echo "Install it via Unity Hub, or the harness cannot run."
  exit 1
fi

# --- Editor lock ------------------------------------------------------------
# Unity takes an exclusive lock on the project. A second instance will either refuse to
# start or silently do nothing, and the failure mode is confusing, so check explicitly.
if [[ -f "$REPO/Temp/UnityLockfile" ]]; then
  if command -v lsof >/dev/null 2>&1 && lsof "$REPO/Temp/UnityLockfile" >/dev/null 2>&1; then
    red "The Unity Editor currently has this project open (Temp/UnityLockfile is held)."
    echo
    echo "  CLOSE THE UNITY EDITOR, then re-run this script."
    echo
    echo "Holder:"
    lsof "$REPO/Temp/UnityLockfile" 2>/dev/null | sed 's/^/  /'
    exit 2
  fi
fi
if pgrep -f "Unity.app/Contents/MacOS/Unity .*-projectPath.*$(basename "$REPO")" >/dev/null 2>&1; then
  red "A Unity process already has this project open. Close the Editor and re-run."
  exit 2
fi

# --- Run --------------------------------------------------------------------
mkdir -p "$OUT"
RESULTS="$OUT/results.xml"
LOG="$OUT/unity.log"
rm -f "$RESULTS" "$LOG"

ARGS=(-batchmode -runTests -testPlatform "$PLATFORM"
      -projectPath "$REPO"
      -testResults "$RESULTS"
      -logFile "$LOG")
[[ -n "$FILTER" ]] && ARGS+=(-testFilter "$FILTER")

bold "Unity $UNITY_VERSION - $PLATFORM tests${FILTER:+ (filter: $FILTER)}"
echo "  log:     $LOG"
echo "  results: $RESULTS"
echo
"$UNITY" "${ARGS[@]}"
UNITY_EXIT=$?

# --- Report -----------------------------------------------------------------
if [[ ! -f "$RESULTS" ]]; then
  red "No results.xml produced (Unity exit $UNITY_EXIT). Compile errors?"
  grep -E "error CS[0-9]+" "$LOG" 2>/dev/null | sort -u | head -20
  exit 1
fi

python3 - "$RESULTS" <<'PY'
import sys, xml.etree.ElementTree as ET
CAP = 60          # generous; the point is that exceeding it is ANNOUNCED, not hidden.

r = ET.parse(sys.argv[1]).getroot()
total  = r.get('total');  passed = r.get('passed')
failed = r.get('failed'); skipped = r.get('skipped')
print(f"total={total} passed={passed} failed={failed} skipped={skipped} result={r.get('result')}")
print()
for tc in r.iter('test-case'):
    res = tc.get('result')
    mark = {'Passed':'PASS','Failed':'FAIL','Skipped':'SKIP','Inconclusive':'INCO'}.get(res, res)
    print(f"  [{mark}] {tc.get('fullname')}")
    if res == 'Failed':
        msg = tc.find('.//message')
        if msg is not None and msg.text:
            # NEVER truncate silently. This capped at 6 lines and hid two of V8's seven
            # stale goldens plus part of C2's element list - a shorter failure looked like
            # a smaller problem. If a cap is ever needed again, it must announce itself.
            lines = msg.text.strip().splitlines()
            for line in lines[:CAP]:
                print(f"         {line}")
            if len(lines) > CAP:
                print(f"         ... {len(lines) - CAP} MORE LINE(S) NOT SHOWN - see {sys.argv[1]}")
sys.exit(0 if (failed or '0') == '0' else 1)
PY
RC=$?

echo
if [[ $RC -eq 0 ]]; then green "Suite green."; else red "Suite has failures (see above)."; fi
echo "Captures: $OUT/Captures"
exit $RC
