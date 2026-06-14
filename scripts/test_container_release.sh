#!/usr/bin/env bash
# 실제 API를 사용하는 Raider 컨테이너의 출시 계약을 검증한다.
set -euo pipefail

image="${1:-raider:local}"
env_file="${2:?usage: test_container_release.sh IMAGE ENV_FILE}"
port="${RAIDER_CONTAINER_RELEASE_PORT:-18086}"
cpu_limit="${RAIDER_CONTAINER_CPUS:-1}"
memory_limit="${RAIDER_CONTAINER_MEMORY:-256m}"
name="raider-release-test-$$"
log_file="$(mktemp)"
timings="$(mktemp)"
data_dir="$(mktemp -d)"

cleanup() {
  docker rm -f "$name" >/dev/null 2>&1 || true
  rm -f "$log_file" "$log_file.html" "$timings"
  rm -rf "$data_dir"
}
trap cleanup EXIT

docker run --rm --user root --volume "$data_dir:/data" --entrypoint chown "$image" app:app /data

start_container() {
  docker run --detach --rm \
    --name "$name" \
    --read-only \
    --tmpfs /tmp:rw,noexec,nosuid,size=64m \
    --volume "$data_dir:/data" \
    --env RAIDER__FAVORITES__DATABASEPATH=/data/raider.db \
    --memory "$memory_limit" \
    --cpus "$cpu_limit" \
    --pids-limit 128 \
    --security-opt no-new-privileges \
    --publish "127.0.0.1:${port}:8080" \
    --env-file "$env_file" \
    "$image" >/dev/null
}

wait_for_live() {
  for _ in {1..60}; do
    if curl --fail --silent "http://127.0.0.1:${port}/health/live" >/dev/null; then
      return
    fi
    sleep 0.25
  done
  return 1
}

wait_for_ready_and_measure_collection() {
  : >"$timings"
  saw_not_ready=false
  for _ in {1..180}; do
    curl --silent --output /dev/null --write-out '%{time_total}\n' "http://127.0.0.1:${port}/" >>"$timings"
    code="$(curl --silent --output /dev/null --write-out '%{http_code}' "http://127.0.0.1:${port}/health/ready")"
    if [[ "$code" == "200" ]]; then
      echo "$saw_not_ready"
      return
    fi
    saw_not_ready=true
    sleep 0.25
  done
  return 1
}

read_percentile() {
  local percentile="$1"
  local count index
  count="$(wc -l <"$timings")"
  index="$(( (count * percentile + 99) / 100 ))"
  sed -n "${index}p" "$timings"
}

measure_ready_home() {
  : >"$timings"
  for _ in {1..50}; do
    curl --fail --silent --output /dev/null --write-out '%{time_total}\n' "http://127.0.0.1:${port}/" >>"$timings"
  done
  sort -n "$timings" -o "$timings"
  p50="$(read_percentile 50)"
  p95="$(read_percentile 95)"
  awk -v value="$p95" 'BEGIN { exit !(value < 0.100) }'
}

assert_no_secrets() {
  local artifact="$1"
  while IFS='=' read -r _ value; do
    value="${value%$'\r'}"
    if [[ -n "$value" ]] && grep --fixed-strings --quiet "$value" "$artifact"; then
      echo "secret value found in artifact" >&2
      exit 1
    fi
  done <"$env_file"
}

start_container
wait_for_live
cold_home_seconds="$(curl --fail --silent --output /dev/null --write-out '%{time_total}' "http://127.0.0.1:${port}/")"
saw_not_ready="$(wait_for_ready_and_measure_collection)"
test "$saw_not_ready" = "true"
sort -n "$timings" -o "$timings"
collection_p50="$(read_percentile 50)"
collection_p95="$(read_percentile 95)"
echo "collection-home-p50-seconds=${collection_p50} collection-home-p95-seconds=${collection_p95}"
awk -v value="$collection_p95" 'BEGIN { exit !(value < 0.100) }'

curl --fail --silent "http://127.0.0.1:${port}/" >"$log_file.html"
curl --fail --silent "http://127.0.0.1:${port}/?q=raider-search-probe" >/dev/null
grep --quiet 'class="stream-card"' "$log_file.html"
! grep --quiet '"broadcastId":' "$log_file.html"
assert_no_secrets "$log_file.html"

measure_ready_home
echo "ready-home-p50-seconds=${p50} ready-home-p95-seconds=${p95}"
stats="$(docker stats "$name" --no-stream --format '{{.MemUsage}}|{{.CPUPerc}}')"
health="$(docker inspect "$name" --format '{{.State.Health.Status}}')"
test "$health" = "healthy"
test "$(docker inspect "$name" --format '{{len .Mounts}}')" = "1"
test -f "$data_dir/raider.db"

docker logs "$name" >"$log_file" 2>&1
grep --quiet 'Platform: Chzzk' "$log_file"
grep --quiet 'Platform: Soop' "$log_file"
! grep --quiet 'raider-search-probe' "$log_file"
! grep --quiet 'Set-Cookie' "$log_file"
assert_no_secrets "$log_file"

docker stop --time 10 "$name" >/dev/null
start_container
wait_for_live
wait_for_ready_and_measure_collection >/dev/null
curl --fail --silent "http://127.0.0.1:${port}/" >"$log_file.html"
grep --quiet 'class="stream-card"' "$log_file.html"

echo "container-release-smoke=passed cpu_limit=${cpu_limit} memory_limit=${memory_limit} cold_home_seconds=${cold_home_seconds} collection_p50_seconds=${collection_p50} collection_p95_seconds=${collection_p95} ready_p50_seconds=${p50} ready_p95_seconds=${p95} stats=${stats}"
