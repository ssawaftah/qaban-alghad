#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
# Optional Mono bundle root; otherwise use the installed compiler/runtime.
if [[ -n "${QABAN_MONO_ROOT:-}" ]]; then
  export MONO_PATH="$QABAN_MONO_ROOT/usr/lib/mono/4.5"
  export MONO_CFG_DIR="$QABAN_MONO_ROOT/etc"
  export LD_LIBRARY_PATH="$QABAN_MONO_ROOT/usr/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
  runtime=("$QABAN_MONO_ROOT/usr/bin/mono-sgen" --config "$QABAN_MONO_ROOT/etc/mono/config")
  compiler=("${runtime[@]}" "$MONO_PATH/mcs.exe")
else
  runtime=(mono)
  compiler=(mcs)
fi
mkdir -p "$root/out"
mapfile -t sources < <(find "$root/src/QabanAlGhad" -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' | sort)
refs=(-r:System.Windows.Forms -r:System.Drawing -r:System.Web.Extensions -r:System.Security -r:System.IO.Compression -r:System.IO.Compression.FileSystem)
"${compiler[@]}" -warnaserror -target:winexe -platform:anycpu "-win32icon:$root/src/QabanAlGhad/Assets/QabanAlGhad.ico" "-out:$root/out/QabanAlGhad.exe" "${refs[@]}" "${sources[@]}"
cp "$root/src/QabanAlGhad/App.config" "$root/out/QabanAlGhad.exe.config"
mapfile -t checks < <(find "$root/tests/QabanAlGhad.Tests" -maxdepth 1 -name '*.cs' | sort)
"${compiler[@]}" -warnaserror -target:exe "-out:$root/out/QabanAlGhad.Tests.exe" "-r:$root/out/QabanAlGhad.exe" "${refs[@]}" "${checks[@]}"
"${runtime[@]}" "$root/out/QabanAlGhad.Tests.exe" --all
