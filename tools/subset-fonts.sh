#!/bin/bash
# Trims Noto Serif CJK to the characters the game shows in Chinese, Japanese and Korean, so the game ships a
# few hundred kilobytes instead of tens of megabytes. Run it again whenever a zh-Hans, ja or ko table changes;
# LanguageTests fails while a character is missing.
#
#   tools/subset-fonts.sh <source dir> [weight]
#
# <source dir> holds NotoSerifSC-, NotoSerifJP- and NotoSerifKR-<weight>.otf from github.com/notofonts/noto-cjk
# (Serif/SubsetOTF); the weight defaults to Medium. Needs fonttools (`pip install fonttools`) for pyftsubset.
set -euo pipefail
source_dir=$1
weight=${2:-Medium}
repo=$(cd "$(dirname "$0")/.." && pwd)
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

subset() # <region> <table> <native name>
{
    # Everything after " = " on each translation line, and the language's own name for the language list.
    { sed -n 's/^[^#][^=]* = //p' "$repo/assets/lang/$2.txt"; printf '%s\n' "$3"; } > "$work/$1.txt"
    pyftsubset "$source_dir/NotoSerif$1-$weight.otf" --text-file="$work/$1.txt" \
        --output-file="$repo/assets/fonts/NotoSerif$1-$weight-Subset.otf" --layout-features='*' --no-hinting
    echo "NotoSerif$1-$weight-Subset.otf: $(wc -c < "$repo/assets/fonts/NotoSerif$1-$weight-Subset.otf") bytes"
}

subset SC zh-Hans "简体中文"
subset JP ja "日本語"
subset KR ko "한국어"
