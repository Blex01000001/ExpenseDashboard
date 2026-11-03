#!/bin/bash
# 雲端（Claude Code on the web）新環境開機時安裝 .NET 10 SDK 並還原套件，讓 dotnet build/test 直接可用。
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

cd "${CLAUDE_PROJECT_DIR:-.}"

# .NET 10 SDK：dot.net 安裝腳本在雲端被擋，用 Ubuntu 套件。已安裝就跳過（檢查很便宜，每次都跑）。
if ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  export DEBIAN_FRONTEND=noninteractive
  apt-get update -q
  apt-get install -y -q dotnet-sdk-10.0
fi

# 套件還原：只有 csproj／工具清單／本腳本變了才重跑。雜湊檔放在被 git 忽略的 obj/。
hashes=src/ExpenseDashboard.Web/obj/.session-start.sha256
if ! sha256sum --check --status "$hashes" 2>/dev/null; then
  rm -f "$hashes"
  dotnet tool restore
  dotnet restore ExpenseDashboard.slnx
  sha256sum .claude/hooks/session-start.sh dotnet-tools.json \
    src/*/*.csproj tests/*/*.csproj > "$hashes"
fi

{
  echo 'export DOTNET_CLI_TELEMETRY_OPTOUT=1'
  echo 'export DOTNET_NOLOGO=1'
} >> "${CLAUDE_ENV_FILE:-/dev/null}"
