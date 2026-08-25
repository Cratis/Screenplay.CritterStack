#!/usr/bin/env bash
# Copyright (c) Cratis. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

set -euo pipefail

if [[ $# -ne 4 ]]; then
    echo "Usage: $0 <runner.dll> <project.csproj> <expectations> <output.play>" >&2
    exit 2
fi

runner=$1
project=$2
expectations=$3
output=$4
repeat_output="${output}.repeat"

rm -f "$output" "$repeat_output"
dotnet "$runner" "$project" "$expectations" "$output"
dotnet "$runner" "$project" "$expectations" "$repeat_output"
cmp "$output" "$repeat_output"
rm -f "$repeat_output"
