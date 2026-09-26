#!/usr/bin/env bash
# Downloads the CC0 source art packs used by Tools/blender/process_models.py.
# Raw packs live in ThirdParty/Source (git-ignored); only processed models go into Assets/.
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p ThirdParty/Source && cd ThirdParty/Source
clone() { [ -d "$1" ] || git clone --depth 1 "https://github.com/KayKit-Game-Assets/$1.git"; }
clone KayKit-Character-Pack-Adventures-1.0
clone KayKit-Character-Pack-Skeletons-1.0
if [ ! -d kenney_blaster-kit ]; then
  curl -fsSL -o blaster.zip "https://kenney.nl/media/pages/assets/blaster-kit/261d80a716-1753959510/kenney_blaster-kit_2.1.zip"
  mkdir -p kenney_blaster-kit && unzip -q blaster.zip -d kenney_blaster-kit && rm blaster.zip
fi
echo "Source packs ready in ThirdParty/Source"
