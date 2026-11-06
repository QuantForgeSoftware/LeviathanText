#!/usr/bin/env bash
# Selected engineering regression suites. Fitting is deliberately outside this entry point.
set -euo pipefail

repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
skip_build=false
results=''
while (($#)); do
  case "$1" in
    --no-build)
      if $skip_build; then printf '%s\n' 'Duplicate --no-build.' >&2; exit 2; fi
      skip_build=true; shift ;;
    --results)
      if [[ -n "$results" || $# -lt 2 || -z "$2" || "$2" == --* ]]; then
        printf '%s\n' 'Expected one fresh --results directory.' >&2; exit 2
      fi
      results=$2; shift 2 ;;
    *) printf 'Usage: %s [--no-build] [--results <fresh-directory>]\n' "$0" >&2; exit 2 ;;
  esac
done

if [[ -z "$results" ]]; then
  results=$(mktemp -d "${TMPDIR:-/tmp}/leviathan-engineering.XXXXXX")
else
  # mkdir without -p refuses an existing receipt directory. Never erase prior evidence.
  mkdir "$results"
fi
results=$(cd "$results" && pwd)
printf 'Engineering verification receipts: %s\n' "$results"
cd "$repo"

# This entry point never opts into fitting, local corpus contents or locked labels,
# even when a caller's shell was previously configured for an authorized experiment.
export LEVIATHANTEXT_RUN_FITTING=0
export LEVIATHANTEXT_RUN_CORPUS=0
export LEVIATHANTEXT_RUN_LOCKED=0
# A caller's earlier live-provider opt-in must not leak into this engineering gate.
export LEVIATHAN_OLLAMA_LIVE=0

if ! $skip_build; then
  if ! dotnet build LeviathanText.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false > "$results/build.log" 2>&1; then
    cat "$results/build.log" >&2; exit 1
  fi
fi

# A successful process with no discovered tests is not a regression success.
valid_receipt() {
  local file=$1 xml counters executed
  local counter_pattern='<([[:alpha:]_][[:alnum:]_.-]*:)?Counters[[:space:]][^>]*>'
  local executed_pattern='[[:space:]]executed="([0-9]+)"'
  local failed_pattern='[[:space:]]failed="0+"'
  [[ -s "$file" ]] || return 1
  xml=$(< "$file")
  [[ "$xml" =~ $counter_pattern ]] || return 1
  counters=${BASH_REMATCH[0]}
  [[ "$counters" =~ $executed_pattern ]] || return 1
  executed=${BASH_REMATCH[1]}
  [[ "$executed" =~ [1-9] && "$counters" =~ $failed_pattern ]]
}

# Audited in lessons.md. The row-logistic detector also fits a model, even without TorchSharp.
filter='FullyQualifiedName!~TrainingDiagnosticsCommandTests&FullyQualifiedName!~TrainingAlphabetTests&FullyQualifiedName!~LearnedLayoutIntegrationTests&FullyQualifiedName!~LayoutModelApiTests&FullyQualifiedName!~PatchLayoutModelTests&FullyQualifiedName!~LearnedLineDetectorTests'
suites=(Contracts Core Application Imaging Infrastructure Worker Api Cli Formats AI Evaluation Layout TestPolicy)
printf '%s\n' "$filter" > "$results/filter.txt"
run_suite() {
  local suite=$1 project=$2 selection=$3
  if dotnet test "$project" -c Release --no-build --no-restore -m:1 \
      --filter "$selection" --logger "trx;LogFileName=$suite.trx" --results-directory "$results" \
      > "$results/$suite.log" 2>&1; then
    if ! valid_receipt "$results/$suite.trx"; then
      printf '%s invalid receipt: expected executed tests and zero failures in TRX.\n' "$suite" | tee -a "$results/summary.txt" >&2
      exit 1
    fi
    printf '%s passed\n' "$suite" | tee -a "$results/summary.txt"
  else
    printf '%s failed\n' "$suite" | tee -a "$results/summary.txt" >&2
    cat "$results/$suite.log" >&2
    exit 1
  fi
}
for suite in "${suites[@]}"; do
  run_suite "$suite" "tests/LeviathanText.$suite.Tests" "$filter"
done
# The mixed ML project is never run wholesale. These two inspected utility classes
# contain no model/tensor/fitting calls; the trailing dot fixes each exact class.
codec_filter='FullyQualifiedName~LeviathanText.ML.Tests.LabelCodecTests.'
invariant_filter='FullyQualifiedName~LeviathanText.ML.Tests.LabelCodecInvariantTests.'
printf '%s\n%s\n' "$codec_filter" "$invariant_filter" > "$results/codec-filters.txt"
run_suite Codec tests/LeviathanText.ML.Tests "$codec_filter"
run_suite CodecInvariants tests/LeviathanText.ML.Tests "$invariant_filter"
printf 'Selected C# engineering suites and pure codec classes passed. Inspect TRX for counts/skips: %s\n' "$results"
