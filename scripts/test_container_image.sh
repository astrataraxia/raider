#!/usr/bin/env bash
# Raider 컨테이너 이미지의 최소 출시 계약을 검증한다.
set -euo pipefail

image="${1:-raider:local}"
port="${RAIDER_CONTAINER_TEST_PORT:-18085}"
name="raider-image-test-$$"
data_dir="$(mktemp -d)"

cleanup() {
  docker rm -f "$name" >/dev/null 2>&1 || true
  rm -f "/tmp/${name}.html" "/tmp/${name}.css"
  rm -rf "$data_dir"
}
trap cleanup EXIT

docker run --rm --user root --volume "$data_dir:/data" --entrypoint chown "$image" app:app /data

user="$(docker image inspect "$image" --format '{{.Config.User}}')"
if [[ -z "$user" || "$user" == "0" || "$user" == "root" ]]; then
  echo "image must configure a non-root user" >&2
  exit 1
fi

docker run --detach --rm \
  --name "$name" \
  --read-only \
  --tmpfs /tmp:rw,noexec,nosuid,size=64m \
  --volume "$data_dir:/data" \
  --env RAIDER__FAVORITES__DATABASEPATH=/data/raider.db \
  --publish "127.0.0.1:${port}:8080" \
  --env RAIDER__CHZZK__CLIENTID=fixture-id \
  --env RAIDER__CHZZK__CLIENTSECRET=fixture-secret \
  --env RAIDER__COLLECTION__CHZZK__ENABLED=false \
  --env RAIDER__COLLECTION__SOOP__ENABLED=false \
  "$image" >/dev/null

for _ in {1..60}; do
  if curl --fail --silent "http://127.0.0.1:${port}/health/live" >/dev/null; then
    break
  fi
  sleep 0.25
done

curl --fail --silent "http://127.0.0.1:${port}/health/live" >/dev/null
curl --fail --silent "http://127.0.0.1:${port}/" >"/tmp/${name}.html"
grep --quiet 'Raider - Live Radar' "/tmp/${name}.html"
curl --fail --silent "http://127.0.0.1:${port}/css/site.css" >"/tmp/${name}.css"
grep --quiet -- '--color-brand' "/tmp/${name}.css"

mount_count="$(docker inspect "$name" --format '{{len .Mounts}}')"
test "$mount_count" = "1"
test -f "$data_dir/raider.db"

for _ in {1..20}; do
  health="$(docker inspect "$name" --format '{{.State.Health.Status}}')"
  [[ "$health" == "healthy" ]] && break
  sleep 0.5
done
test "$health" = "healthy"
stats="$(docker stats "$name" --no-stream --format '{{.MemUsage}}|{{.CPUPerc}}')"

started="$(date +%s)"
docker stop --time 10 "$name" >/dev/null
elapsed="$(( $(date +%s) - started ))"
test "$elapsed" -le 10

echo "container-image-smoke=passed user=${user} stop_seconds=${elapsed} idle_stats=${stats}"
