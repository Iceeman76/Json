#!/usr/bin/env bash
set -euo pipefail
root="${1:?Site root required}"
public="${2:?Public link required}"
release="${3:?Release required}"
url="${4:?Website URL required}"
[[ "$root" =~ ^/[a-zA-Z0-9_./-]+$ && "$root" != */ && "$(realpath -- "$root")" = "$root" ]]
[[ "$public" = public || "$public" = wwwroot ]]
[[ "$release" =~ ^[0-9]+-[0-9]+-[0-9a-f]{12}$ ]]
[[ "$url" =~ ^(https?)://([a-zA-Z0-9.-]+)/?$ ]]
scheme="${BASH_REMATCH[1]}"
host="${BASH_REMATCH[2]}"
port=80
if [[ "$scheme" = https ]]; then port=443; fi
exec 9>"$root/.control-center-deploy.lock"
flock -w 30 9
target="$root/releases/$release"
test -d "$root/releases" && test ! -L "$root/releases"
test "$(realpath -- "$target")" = "$target"
test ! -L "$target" && test -s "$target/index.html" && test -s "$target/deployment-version.txt"
test -L "$root/$public"
previous=$(readlink -- "$root/$public")
previous_target=$(realpath -- "$root/$public")
[[ "$previous_target" = "$root/releases/"* ]]
previous_name="${previous_target##*/}"
[[ "$previous_name" =~ ^(initial-[0-9]{14}|[0-9]+-[0-9]+(-[0-9a-f]{12})?)$ ]]
pending="$root/.control-center-public.next"
test ! -e "$pending" && test ! -L "$pending"
trap 'rm -f -- "$pending"' EXIT
ln -s -- "releases/$release" "$pending"
mv -Tf -- "$pending" "$root/$public"
expected=$(cat "$target/deployment-version.txt")
if curl --fail --silent --max-time 20 --resolve "$host:$port:127.0.0.1" "${url%/}/" >/dev/null && actual=$(curl --fail --silent --max-time 20 --resolve "$host:$port:127.0.0.1" "${url%/}/deployment-version.txt") && [[ "$actual" = "$expected" ]]; then
    printf 'Activated release %s\n' "$release"
else
    ln -s -- "$previous" "$pending"
    mv -Tf -- "$pending" "$root/$public"
    exit 1
fi
