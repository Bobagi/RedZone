#!/usr/bin/env bash
# Builds the Windows player in batch mode and runs the robot playtest (AutoPlayer): every screen and every level,
# screenshots and autoplay.log in ./autoplay. The Unity editor must be closed on this project.
#   bash SourceAssets/Tools/playtest.sh [nobuild]
set -e
cd "$(dirname "$0")/../.."
UNITY="/c/Program Files/Unity/Hub/Editor/6000.5.1f1/Editor/Unity.exe"
if [ "$1" != "nobuild" ]; then
  "$UNITY" -batchmode -nographics -quit -projectPath "$(pwd -W)" -executeMethod RedZone.EditorTools.RedZoneTools.BuildWindows -logFile "$(pwd -W)/Logs/batch_win.log"
  tail -n 3 Builds/build_status.txt
fi
rm -rf autoplay && mkdir -p autoplay
./Builds/Windows/RedZone.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -autoplay \
  -autoplayOut "$(pwd -W)/autoplay" -logFile "$(pwd -W)/autoplay/player.log" > /dev/null
cat autoplay/autoplay.log
grep -E "Exception" -A2 autoplay/player.log | sort | uniq -c | sort -rn | head -20 || true
