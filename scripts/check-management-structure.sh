#!/usr/bin/env sh
set -eu

required_files="
AGENTS.md
RULES.md
.ai/CONTEXT.md
.ai/NEXT_ACTION.md
docs/03-planning/CURRENT_PHASE.md
docs/04-execution/TASK_BOARD.md
docs/05-quality/DEFINITION_OF_DONE.md
docs/02-architecture/DEPENDENCY_RULES.md
"

failed=0
for file in $required_files; do
  if [ ! -f "$file" ]; then
    echo "Missing required file: $file"
    failed=1
  fi
done

if [ "$failed" -ne 0 ]; then
  exit 1
fi

echo "Management structure check passed."
