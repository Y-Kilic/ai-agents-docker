#!/bin/bash
set -e
ID=$1
GOAL=$2
LOOPS=$3
BASE_IMG="$HOME/.cache/worldseed/ubuntu-base.img"
WORK="/tmp/worldseed-vm/$ID"
mkdir -p "$WORK"
if [ ! -f "$BASE_IMG" ]; then
  mkdir -p "$(dirname "$BASE_IMG")"
  wget -qO "$BASE_IMG" https://cloud-images.ubuntu.com/minimal/releases/jammy/release/ubuntu-22.04-minimal-cloudimg-amd64.img
fi
OVERLAY="$WORK/overlay.img"
qemu-img create -f qcow2 -b "$BASE_IMG" "$OVERLAY" >/dev/null
cat > "$WORK/user-data" <<DATA
#cloud-config
runcmd:
 - apt-get update
 - DEBIAN_FRONTEND=noninteractive apt-get install -y dotnet-sdk-8.0
 - mkdir /repo
 - mount -t 9p -o trans=virtio hostshare /repo
 - cd /repo/src/Agent.Runtime
 - OPENAI_API_KEY="$OPENAI_API_KEY" AGENT_ID="$ID" ORCHESTRATOR_URL="$ORCHESTRATOR_URL" LOOP_COUNT="$LOOPS" KEEP_ALIVE=1 dotnet run --project Agent.Runtime.csproj -- "$GOAL"
DATA
cloud-localds "$WORK/cloud.img" "$WORK/user-data"
exec qemu-system-x86_64 -m 1024 -smp 1 -nographic \
  -drive file="$OVERLAY",format=qcow2 \
  -fsdev local,security_model=passthrough,id=fsdev0,path=$(pwd) \
  -device virtio-9p-pci,fsdev=fsdev0,mount_tag=hostshare \
  -cdrom "$WORK/cloud.img" \
  -serial mon:stdio

