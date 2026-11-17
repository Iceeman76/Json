#!/usr/bin/env bash
set -euo pipefail
output="${1:?Publish directory required}"
source="${2:?Source directory required}"
scripts="${3:?Deployment script directory required}"
[[ "$DEPLOY_ROOT" =~ ^/[a-zA-Z0-9_./-]+$ && "$DEPLOY_ROOT" != *'/../'* && "$DEPLOY_ROOT" != */.. && "$DEPLOY_ROOT" != */ ]]
[[ "$DEPLOY_USER" =~ ^[a-z_][a-z0-9_-]*$ && "$DEPLOY_HOST" =~ ^[a-zA-Z0-9][a-zA-Z0-9.-]+$ && "$DEPLOY_PORT" =~ ^[0-9]+$ ]]
[[ "$DEPLOY_PUBLIC_LINK" = public || "$DEPLOY_PUBLIC_LINK" = wwwroot ]]
[[ "$DEPLOY_URL" =~ ^https?://[a-zA-Z0-9.-]+/?$ ]]
test -s "$output/index.html"
expected=$(git -C "$source" rev-parse HEAD)
printf '%s\n' "$expected" > "$output/deployment-version.txt"
release="${GITHUB_RUN_ID}-${GITHUB_RUN_ATTEMPT}-${expected:0:12}"
key_directory=$(mktemp -d "$RUNNER_TEMP/control-center-ssh.XXXXXXXX")
trap 'rm -f -- "$key_directory/key" "$key_directory/hosts"; rmdir -- "$key_directory"' EXIT
umask 077
printf '%s\n' "${DEPLOY_KEY:?Deployment key secret missing}" > "$key_directory/key"
printf '%s\n' "${DEPLOY_KNOWN_HOSTS:?Known-hosts secret missing}" > "$key_directory/hosts"
transport="ssh -i $key_directory/key -p $DEPLOY_PORT -o BatchMode=yes -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile=$key_directory/hosts -o ConnectTimeout=15"
destination="$DEPLOY_USER@$DEPLOY_HOST"
$transport -n "$destination" "test ! -L '$DEPLOY_ROOT' && test -L '$DEPLOY_ROOT/$DEPLOY_PUBLIC_LINK' && test -d '$DEPLOY_ROOT/releases' && test ! -L '$DEPLOY_ROOT/releases' && mkdir -p '$DEPLOY_ROOT/releases/$release.incoming'"
rsync -a --safe-links --delete --chmod=D755,F644 -e "$transport" "$output/" "$destination:$DEPLOY_ROOT/releases/$release.incoming/"
$transport "$destination" "test ! -e '$DEPLOY_ROOT/releases/$release' && test ! -L '$DEPLOY_ROOT/releases/$release' && mv -T '$DEPLOY_ROOT/releases/$release.incoming' '$DEPLOY_ROOT/releases/$release' && bash -s -- '$DEPLOY_ROOT' '$DEPLOY_PUBLIC_LINK' '$release' '$DEPLOY_URL'" < "$scripts/activate.sh"
curl --fail --silent --show-error --max-time 30 "${DEPLOY_URL%/}/" >/dev/null
actual=$(curl --fail --silent --show-error --max-time 30 "${DEPLOY_URL%/}/deployment-version.txt")
test "$actual" = "$expected"
$transport "$destination" "bash -s -- '$DEPLOY_ROOT' '$DEPLOY_PUBLIC_LINK' '$KEEP_HISTORY'" < "$scripts/cleanup.sh"
