#!/bin/bash
# Makes the trailer: the 2016 trailer's edit (data9/BlenderNLE/Cubicolor-Trailer-New.blend), shot again in the game
# at 1080p. Its frame numbers, fades and sound levels are the old edit's, at 60 frames a second; each shot is a
# level played headless with `film` (no Completed panel, no bell) and recorded with its sound.
#
#   tools/trailer/make.sh [output.mp4]      (default: artifacts/trailer/cubicolor-trailer.mp4)
#
# EDIT_ONLY=1 cuts again from the last run's recordings, for changing the edit without shooting again.
#
# Needs ffmpeg and ImageMagick, and the 2016 project folder for the music cut and the platform logos:
# CUBICOLOR_2016, default ~/Dropbox/Projekte/Cubicolor.
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
out="${1:-$root/artifacts/trailer/cubicolor-trailer.mp4}"
old="${CUBICOLOR_2016:-$HOME/Dropbox/Projekte/Cubicolor}"
music="$old/data9/BlenderNLE/soundtrack_trailer-final.wav"
logos="$old/data8/Cubicolor/logos"
work="$(dirname "$out")/work"
background=0xf2f0ed

if [ "${EDIT_ONLY:-}" != 1 ]; then rm -rf "$work"; fi
mkdir -p "$work/data"
# The game's own music stays out of the recordings, and so do the level's name and hints, as in 2016.
printf '# Cubicolor settings\nversion = 1\nhide_ui = true\nmusic_muted = true\n' > "$work/data/settings.txt"

dotnet build "$root/src/Cubicolor" -c Release > "$work/build.log"
game() { dotnet run --project "$root/src/Cubicolor" -c Release --no-build -- "$@"; }
# Seconds from the old edit's frame numbers, and sums of them, with a leading zero as ffmpeg wants.
f() { awk "BEGIN { printf \"%.6f\", ($1) / 60 }"; }
ms() { awk "BEGIN { printf \"%d\", ($1) / 60 * 1000 }"; }

# record <name> <level> <seconds between moves>: the level from its first frame, its recorded solution played at
# that pace, to $work/<name>.mkv and .wav.
record() {
    local name="$1" level="$2" pace="$3"
    local moves script="$work/$name.script" pipe="$work/$name.pipe"
    moves="$(awk -v l="$level.bin" '$1 == l { print $2 }' "$root/tests/Cubicolor.Tests/Fixtures/solutions.txt")"
    {
        echo "film $level"
        echo "wait ready"
        echo "wait 0.4"
        for ((i = 0; i < ${#moves}; i++)); do echo "moves ${moves:i:1}"; echo "wait $pace"; done
        echo "wait 2"
    } > "$script"
    mkfifo "$pipe"
    ffmpeg -v error -y -f rawvideo -pix_fmt rgba -s 1920x1080 -r 60 -i "$pipe" \
        -c:v libx264 -crf 8 -preset fast -pix_fmt yuv420p "$work/$name.mkv" &
    game --headless --size 1920x1080 --timeout 120 --data-dir "$work/data" --script "$script" \
        --record "$pipe" --capture-audio "$work/$name.wav" > "$work/$name.log"
    wait $!
    rm "$pipe"
}

# The shots: the old trailer's first two levels exactly; the third and the grid around it with levels of the
# same kind, as the 2016 ones were made for the trailer and aren't in the game.
grid=(smart02 smart03 smart04 smart05 smart06 smart07 smart08 smart09)
if [ "${EDIT_ONLY:-}" != 1 ]; then
    record shot1 normal02 0.75
    record shot2 normal03 0.69
    record shot3 smart01 0.5
    for i in "${!grid[@]}"; do record "grid$i" "${grid[$i]}" 0.55; done
fi

# The words, set by the game; placed where the 2016 frames had them (their 720p positions times 1.5).
[ -d "$work/art" ] || game --headless --timeout 300 --art "$work/art" > "$work/art.log"
text="$work/art/trailer"
place() { # <strip> <top of its ink>: the strip trimmed to its ink, centred across, its ink starting at that row
    magick "$text/$1.png" -trim +repage "$work/$1.ink.png"
    local w; w=$(magick identify -format "%w" "$work/$1.ink.png")
    echo "$work/$1.ink.png -geometry +$(((1920 - w) / 2))+$2 -composite"
}
magick -size 1920x1080 xc:none $(place caption1 93) "$work/caption1.png"
magick -size 1920x1080 xc:none $(place caption2 93) "$work/caption2.png"
for logo in windows apple; do magick "$logos/$logo.xcf" -background none -layers merge -trim +repage "$work/$logo.png"; done
# Tux's file has a near-white background where the others have none; its belly and eyes are that colour too, so
# they become holes, as they look on the old card.
magick "$logos/tux.xcf" -background none -layers merge -fuzz 6% -transparent "$(magick "$logos/tux.xcf" -background none -layers merge -format '%[pixel:p{3,3}]' info:)" -trim +repage "$work/tux.png"
magick -size 1920x1080 xc:none $(place title 162) $(place website 837) \
    \( "$work/windows.png" -resize x174 \) -geometry +618+511 -composite \
    \( "$work/apple.png" -resize x176 \) -geometry +909+502 -composite \
    \( "$work/tux.png" -resize x171 \) -geometry +1162+516 -composite \
    "$work/endcard.png"

# The edit, in the old edit's frame numbers.
ease() { echo "(3*pow($1,2)-2*pow($1,3))"; }   # Blender's default keyframe easing, near enough
# The grid's zoom out, frames 1766 to 1946 (170 to 350 of the shot): from the middle level alone to all nine.
zoom="3-2*$(ease "clip((in-170)/180,0,1)")"
endin="clip(t/$(f 49),0,1)"

inputs=(-i "$work/shot1.mkv" -i "$work/shot2.mkv" -i "$work/shot3.mkv")
for i in "${!grid[@]}"; do inputs+=(-i "$work/grid$i.mkv"); done
inputs+=(-loop 1 -framerate 60 -i "$work/caption1.png" -loop 1 -framerate 60 -i "$work/caption2.png"
         -loop 1 -framerate 60 -i "$work/endcard.png" -i "$music"
         -i "$work/shot1.wav" -i "$work/shot2.wav" -i "$work/shot3.wav" -i "$work/grid1.wav" -i "$work/grid5.wav" -i "$work/grid6.wav")

# The grid's cells around the third shot: 0 1 2 / 3 c 4 / 5 6 7, each joining at frame 1766 with its level already
# under way, three seconds into its recording.
cells=""
for i in "${!grid[@]}"; do
    cells+="[$((i + 3)):v]trim=start=$(f "180 - 170"):duration=$(f 745),setpts=PTS-STARTPTS[g$i];"
done

graph="
color=$background:s=1920x1080:r=60:d=$(f 2896)[bg];
[0:v]trim=end_frame=548,setpts=PTS-STARTPTS,crop=1920:952:0:0,pad=1920:1080:0:128:$background,fade=t=out:st=$(f 518):d=$(f 30):color=$background,setpts=PTS+$(f 62)/TB[s1];
[1:v]trim=end_frame=865,setpts=PTS-STARTPTS,crop=1920:952:0:0,pad=1920:1080:0:128:$background,fade=t=out:st=$(f 834):d=$(f 30):color=$background,setpts=PTS+$(f 701)/TB[s2];
[2:v]trim=end_frame=745,setpts=PTS-STARTPTS[c];
$cells
[g0][g1][g2][g3][c][g4][g5][g6][g7]xstack=inputs=9:layout=0_0|w0_0|w0+w1_0|0_h0|w0_h0|w0+w1_h0|0_h0+h3|w0_h0+h3|w0+w1_h0+h3,
  zoompan=z='$zoom':x='(iw-iw/zoom)/2':y='(ih-ih/zoom)/2':d=1:s=1920x1080:fps=60,
  fade=t=out:st=$(f 655):d=$(f 90):color=$background,setpts=PTS+$(f 1596)/TB[s3];
[$((11)):v]trim=duration=$(f 609),format=rgba,fade=t=in:st=0:d=$(f 31):alpha=1,fade=t=out:st=$(f 579):d=$(f 30):alpha=1,setpts=PTS+$(f 31)/TB[t1];
[$((12)):v]trim=duration=$(f 926),format=rgba,fade=t=in:st=0:d=$(f 31):alpha=1,fade=t=out:st=$(f 896):d=$(f 30):alpha=1,setpts=PTS+$(f 670)/TB[t2];
[$((13)):v]trim=duration=$(f 533),format=rgba,scale=w='ceil(1920*(0.9+0.1*$(ease "$endin"))/2)*2':h=-2:eval=frame,
  fade=t=in:st=0:d=$(f 49):alpha=1,setpts=PTS+$(f 2363)/TB[end];
[bg][s1]overlay=eof_action=pass[v1];
[v1][s2]overlay=eof_action=pass[v2];
[v2][s3]overlay=eof_action=pass[v3];
[v3][t1]overlay=eof_action=pass[v4];
[v4][t2]overlay=eof_action=pass[v5];
[v5][end]overlay=x='(W-w)/2':y='(H-h)/2':eval=frame:eof_action=pass,format=yuv420p[v];
[14:a]volume=2[music];
[15:a]atrim=end=$(f 591),volume=2,adelay=$(ms 62):all=1[a1];
[16:a]atrim=end=$(f 865),volume=2,adelay=$(ms 701):all=1[a2];
[17:a]atrim=end=$(f 745),volume='2*(1-clip((t-$(f 655))/$(f 90),0,1))':eval=frame,adelay=$(ms 1596):all=1[a3];
[18:a]atrim=start=$(f "180 - 170"):duration=$(f 745),asetpts=PTS-STARTPTS,
  volume='if(lt(t,$(f 289)),0,if(lt(t,$(f 350)),0.4*(t-$(f 289))/$(f 61),if(lt(t,$(f 655)),0.4-0.2*(t-$(f 350))/$(f 305),0.2*(1-clip((t-$(f 655))/$(f 90),0,1)))))':eval=frame,
  adelay=$(ms 1596):all=1[a4];
[19:a]atrim=start=$(f "180 - 170"):duration=$(f 745),asetpts=PTS-STARTPTS,
  volume='if(lt(t,$(f 289)),0,if(lt(t,$(f 350)),0.2*(t-$(f 289))/$(f 61),0.2*(1-clip((t-$(f 655))/$(f 90),0,1))))':eval=frame,
  adelay=$(ms 1596):all=1[a5];
[20:a]atrim=start=$(f "180 - 170"):duration=$(f 745),asetpts=PTS-STARTPTS,
  volume='if(lt(t,$(f 289)),0,if(lt(t,$(f 350)),0.2*(t-$(f 289))/$(f 61),0.2*(1-clip((t-$(f 655))/$(f 90),0,1))))':eval=frame,
  adelay=$(ms 1596):all=1[a6];
[music][a1][a2][a3][a4][a5][a6]amix=inputs=7:normalize=0:duration=first,atrim=end=$(f 2896)[a]
"

ffmpeg -v error -y "${inputs[@]}" -filter_complex "$graph" -map "[v]" -map "[a]" -t "$(f 2896)" -r 60 \
    -c:v libx264 -crf 16 -preset slow -profile:v high -pix_fmt yuv420p \
    -c:a aac -b:a 256k -ar 48000 -movflags +faststart "$out"
echo "Made $out"
