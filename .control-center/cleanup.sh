#!/usr/bin/env bash
set -euo pipefail
root="${1:?Site root required}"
public="${2:?Public link required}"
keep="${3:?History policy required}"
[[ "$keep" = true || "$keep" = false ]]
if [[ "$keep" = true ]]; then printf 'Release history retained\n'; exit 0; fi
[[ "$root" =~ ^/[a-zA-Z0-9_./-]+$ && "$root" != */ && "$(realpath -- "$root")" = "$root" ]]
[[ "$public" = public || "$public" = wwwroot ]]
exec 9>"$root/.control-center-deploy.lock"
flock -w 30 9
releases="$root/releases"
test -d "$releases" && test ! -L "$releases"
test "$(realpath -- "$releases")" = "$releases"
test -L "$root/$public"
active=$(realpath -- "$root/$public")
[[ "$active" = "$releases/"* ]]
active_name="${active##*/}"
[[ "$active_name" =~ ^[0-9]+-[0-9]+-[0-9a-f]{12}$ ]]
for candidate in "$releases"/*; do
    name="${candidate##*/}"
    [[ "$name" =~ ^[0-9]+-[0-9]+(-[0-9a-f]{12})?$ ]] || continue
    [[ "$candidate" != "$active" ]] || continue
    test -d "$candidate" && test ! -L "$candidate" || continue
    test "$(stat -c %u -- "$candidate")" = "$(id -u)" || continue
    test "$(stat -c %d -- "$candidate")" = "$(stat -c %d -- "$releases")" || continue
    mountpoint -q -- "$candidate" && continue
    test "$(realpath -- "$candidate")" = "$candidate"
    test "$(realpath -- "$root/$public")" = "$active"
    rm -rf --one-file-system -- "$candidate"
done
