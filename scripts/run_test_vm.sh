#!/bin/bash
set -e
BASE_IMG="$HOME/.cache/worldseed/ubuntu-base.img"
WORK="/tmp/worldseed-vm-test"
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
 - bash
DATA
cloud-localds "$WORK/cloud.img" "$WORK/user-data"
exec qemu-system-x86_64 -m 1024 -smp 1 -nographic \
  -drive file="$OVERLAY",format=qcow2 \
  -cdrom "$WORK/cloud.img" \
  -serial mon:stdio
