#!/bin/bash
set -euo pipefail

# This script stops a tmux session by sending SIGINT to a specific descendant process.

if [ $# -ne 2 ]; then
    echo "Usage: $0 <tmux_session_name> <process_name_substring>"
    exit 1
fi

SESSION="$1"
TARGET="$2"

# Check if the tmux session exists
if ! tmux has-session -t "$SESSION" 2>/dev/null; then
    echo "ℹ️ Tmux session '$SESSION' not found"
    exit 0
fi

# Get the PID of the pane running inside the tmux session
PANE_PID=$(tmux list-panes -t "$SESSION" -F "#{pane_pid}")
if [ -z "$PANE_PID" ]; then
    echo "❌ Failed to get pane PID"
    exit 1
fi

# Recursively find all descendant PIDs from the pane PID
ALL_DESCENDANTS=$(ps -eo pid=,ppid= | awk -v root="$PANE_PID" '
BEGIN { found[root] = 1 }
{
  pid = $1; ppid = $2
  if (found[ppid]) {
    found[pid] = 1
    print pid
  }
}
')

# Search for the first matching process by argument substring (most likely the .exe or service)
EXE_PID=$(ps -o pid=,args= --sort=start_time -p $(echo "$ALL_DESCENDANTS") \
  | awk -v name="$TARGET" '$0 ~ name { print $1; exit }')

if [ -z "$EXE_PID" ]; then
    echo "❌ Process matching '$TARGET' not found among descendants"
    exit 1
fi

echo "🛑 Sending SIGINT to PID $EXE_PID (match: '$TARGET')"
kill -INT "$EXE_PID"

echo "🧹 Killing tmux session '$SESSION'"
tmux kill-session -t "$SESSION"

echo "✅ Done"
