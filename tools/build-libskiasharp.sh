#!/bin/sh
# Spindle tools — build libSkiaSharp.so for FreeBSD, for a given SkiaSharp release.
#
#   tools/build-libskiasharp.sh 3.119.4 [work-folder]
#
# SkiaSharp ships no FreeBSD native library, and FreeBSD's graphics/libskiasharp
# port lags (Skia 116 when Avalonia 12's SkiaSharp 3.119 needs 119). So: the
# exact Skia that SkiaSharp release pins (mono/SkiaSharp's externals/skia
# submodule at the tag), with the port's FreeBSD patches and its export map,
# built against the system's libraries the way the port builds it. The result is
# <work>/out/<version>/libSkiaSharp.so, with BUILD-INFO.txt saying what went in.
#
# Needs on FreeBSD: gn, ninja, python3, curl, and expat, jpeg-turbo, freetype2,
# png, webp, fontconfig (pkg install gn ninja python3 curl expat jpeg-turbo
# freetype2 png webp fontconfig).
set -eu

version=${1:?usage: $0 <SkiaSharp version, e.g. 3.119.4> [work folder]}
work=${2:-$HOME/build/skiasharp}
ports=https://raw.githubusercontent.com/freebsd/freebsd-ports/main/graphics/libskiasharp
api=https://api.github.com/repos

[ "$(uname -s)" = FreeBSD ] || { echo "FreeBSD only" >&2; exit 1; }
for t in gn ninja python3 curl; do command -v $t >/dev/null || { echo "missing: $t" >&2; exit 1; }; done

# 1. The Skia commit SkiaSharp $version pins. The NuGet package names the SkiaSharp
#    commit it was built from (its nuspec's <repository commit=...>), which holds even
#    when GitHub has no tag for the release; externals/skia at that commit is the Skia.
skiasharp=$(curl -fsSL "https://api.nuget.org/v3-flatcontainer/skiasharp/$version/skiasharp.nuspec" | sed -n 's/.*<repository [^>]*commit="\([0-9a-f]\{40\}\)".*/\1/p' | head -1)
[ -n "$skiasharp" ] || { echo "SkiaSharp $version: no package on NuGet, or no commit in its nuspec" >&2; exit 1; }
sha=$(curl -fsSL "$api/mono/SkiaSharp/contents/externals/skia?ref=$skiasharp" | sed -n 's/^ *"sha": *"\([0-9a-f]\{40\}\)".*/\1/p' | head -1)
[ -n "$sha" ] || { echo "no externals/skia submodule at SkiaSharp $skiasharp" >&2; exit 1; }
echo "SkiaSharp $version is mono/SkiaSharp $skiasharp, which pins mono/skia $sha"

src=$work/src/skia-$sha

# Port patches that newer Skia made unnecessary in its own words, so they neither
# apply nor reverse: each with the test that shows Skia has the change.
superseded() {
  case $1 in
    # musttail off on PowerPC (the port's !defined(__powerpc__)); Skia 13x says !defined(SK_CPU_PPC).
    patch-src_core_SkRasterPipeline.h) grep -q 'defined(SK_CPU_PPC)' "$src/src/core/SkRasterPipeline.h" ;;
    *) return 1 ;;
  esac
}

if [ ! -d "$src" ]; then
  mkdir -p "$work/src"
  curl -fsSL "https://github.com/mono/skia/archive/$sha.tar.gz" | tar -xzf - -C "$work/src"
  # 2. The FreeBSD port's patches (named patch-<path with _ for />) and export map.
  mkdir -p "$work/port-$sha"
  for f in $(curl -fsSL "$api/freebsd/freebsd-ports/contents/graphics/libskiasharp/files" | sed -n 's/^ *"name": *"\([^"]*\)".*/\1/p'); do
    curl -fsSL "$ports/files/$f" -o "$work/port-$sha/$f"
  done
  # A patch upstream Skia has since taken applies in reverse: it is skipped. One that
  # neither applies nor is already in stops the build, for a person to look at.
  for p in "$work/port-$sha"/patch-*; do
    if (cd "$src" && patch -p0 -s -f --dry-run < "$p" >/dev/null 2>&1); then
      echo "patch: $(basename "$p")"
      (cd "$src" && patch -p0 -s -f < "$p")
      echo "applied $(basename "$p")" >> "$work/port-$sha/PATCHES.txt"
    elif (cd "$src" && patch -p0 -s -f -R --dry-run < "$p" >/dev/null 2>&1); then
      echo "patch: $(basename "$p") is already in this Skia, skipped"
      echo "skipped $(basename "$p") (already in this Skia)" >> "$work/port-$sha/PATCHES.txt"
    elif superseded "$(basename "$p")"; then
      echo "patch: $(basename "$p") is superseded by this Skia's own change, skipped"
      echo "skipped $(basename "$p") (superseded by this Skia's own change)" >> "$work/port-$sha/PATCHES.txt"
    else
      echo "patch: $(basename "$p") does not apply to this Skia; stopping" >&2
      rm -rf "$src" "$work/port-$sha"; exit 1
    fi
  done
fi

milestone=$(sed -n 's/^#define SK_MILESTONE \([0-9]*\).*/\1/p' "$src/include/core/SkMilestone.h")
echo "Skia milestone $milestone"

# 3. Built as the port builds it: system libraries, no ICU, no HarfBuzz (SkiaSharp
#    has HarfBuzzSharp for that), Ganesh on, Vulkan off. SKIA_C_DLL exports the C API.
out=$work/out/$version
# Skia's switches come and go between milestones (skia_use_sfntly is gone by 155, and
# 13x adds skia_use_partition_alloc, on for standalone clang builds: Chromium's
# allocator, which SkiaSharp's library does not use and which is not fetched here).
# gn refuses a switch no build file declares, so each is passed only where this Skia
# declares it, and the ones left out are listed.
args=""; unused=""
for a in skia_enable_tools=false skia_use_icu=false skia_use_sfntly=false skia_use_piex=true \
         skia_use_harfbuzz=false skia_use_wuffs=false skia_use_system_expat=true \
         skia_use_system_libjpeg_turbo=true skia_use_system_freetype2=true skia_use_system_libpng=true \
         skia_use_system_libwebp=true skia_use_system_zlib=true skia_enable_ganesh=true \
         skia_use_vulkan=false skia_enable_skottie=true skia_enable_pdf=true skia_use_dng_sdk=false \
         skia_use_partition_alloc=false; do
  if grep -rqE --include='*.gn' --include='*.gni' "^[[:space:]]*${a%%=*}[[:space:]]*=" "$src" 2>/dev/null; then
    args="$args $a"
  else
    unused="$unused ${a%%=*}"
  fi
done
[ -n "$unused" ] && echo "not in this Skia, left out:$unused"
gn gen "$out" --root="$src" --args="is_official_build=true target_os=\"linux\" $args
  extra_cflags=[\"-DSKIA_C_DLL\", \"-I/usr/local/include\", \"-I/usr/local/include/freetype2\"]
  extra_ldflags=[\"-L/usr/local/lib\", \"-Wl,--version-script=$work/port-$sha/libskiasharp.map\"]
  linux_soname_version=\"$milestone.0.0\""
ninja -C "$out" SkiaSharp

lib=$(ls "$out"/libSkiaSharp.so.* | head -1)
cp "$lib" "$out/libSkiaSharp.so"
{
  echo "libSkiaSharp.so for FreeBSD $(uname -r) $(uname -m), built $(date -u +%Y-%m-%dT%H:%MZ)"
  echo "SkiaSharp $version (mono/SkiaSharp $skiasharp); Skia: https://github.com/mono/skia/commit/$sha (milestone $milestone)"
  echo "FreeBSD port patches (graphics/libskiasharp, freebsd-ports main):"
  sed 's/^/  /' "$work/port-$sha/PATCHES.txt"
  echo "export map: the port's libskiasharp.map"
  echo "exported sk_* functions: $(nm -D --defined-only "$out/libSkiaSharp.so" | grep -c ' sk_')"
  echo "sha256: $(sha256 -q "$out/libSkiaSharp.so")"
} > "$out/BUILD-INFO.txt"
cat "$out/BUILD-INFO.txt"
