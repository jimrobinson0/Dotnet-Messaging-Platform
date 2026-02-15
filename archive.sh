#!/usr/bin/env bash

set -euo pipefail

TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
OUTPUT_FILE="csharp_source_${TIMESTAMP}.tar.gz"

find . \
  -type d \( \
    -name bin -o \
    -name obj -o \
    -name .git -o \
    -name .vs -o \
    -name node_modules \
  \) -prune -o \
  -type f -name "*.cs" -print0 \
| tar --null -czf "$OUTPUT_FILE" --files-from=-

echo "Archive created: $OUTPUT_FILE"
