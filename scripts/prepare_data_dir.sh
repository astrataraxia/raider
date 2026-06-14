#!/usr/bin/env bash
# 서버 호스트 즐겨찾기 데이터 디렉터리를 비루트 컨테이너 사용자가 쓸 수 있게 준비한다.
set -euo pipefail

data_path="${1:-./data}"
image="${2:-${RAIDER_IMAGE:-raider:local}}"

mkdir -p "$data_path"
absolute_path="$(cd "$data_path" && pwd)"
docker run --rm --user root --volume "$absolute_path:/data" --entrypoint chown "$image" -R app:app /data
