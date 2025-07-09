#!/bin/sh
set -eu

SESSION_NAME="explorer_worker_session"
DIR="."
CMD="just rw0"
CRASH_DIR="$HOME/crashes"

mkdir -p "$CRASH_DIR"

if ! tmux has-session -t "$SESSION_NAME" 2>/dev/null; then
  tmux new-session -d -s "$SESSION_NAME" "
    cd \"$DIR\"
    CMD='$CMD'

    while true; do
      TIMESTAMP=\$(date +%Y%m%d%H%M%S)
      LOG_FILE=\"\$HOME/crashes/${SESSION_NAME}\${TIMESTAMP}.txt\"
      TMP_FILE=\"\$LOG_FILE.tmp\"

      echo \"[tmux] Starting: \$CMD\" | tee \"\$TMP_FILE\"

      # Run command with TTY support
      script -q -c \"\$CMD\" /dev/null 2>&1 | tee -a \"\$TMP_FILE\"
      EXIT_CODE=\$?
      echo \"[tmux] Process exited with code \$EXIT_CODE\" >> \"\$TMP_FILE\"

      ESC=\$(printf '\\033')
      BEL=\$(printf '\\007')

      # Strip ANSI/OSC/DECPAM escape sequences using POSIX-compatible sed
      tail -n 200 \"\$TMP_FILE\" \\
        | sed -e \"s/\${ESC}\\[[0-9;?]*[A-Za-z]//g\" \\
              -e \"s/\${ESC}]9;[0-9;]*;.*\${ESC}\\\\\\\\//g\" \\
              -e \"s/\${ESC}]9;[0-9;]*;.*\${BEL}//g\" \\
              -e \"s/\${ESC}=//g\" \\
        > \"\$LOG_FILE\"

      rm -f \"\$TMP_FILE\"

      echo \"[tmux] Saved last 200 lines (cleaned) to \$LOG_FILE\"
      echo \"Restarting in 3 seconds...\"
      sleep 3
    done
  "
  echo "✅ Started new tmux session: $SESSION_NAME"
else
  echo "ℹ️  Reusing existing tmux session: $SESSION_NAME"
fi

exec tmux attach -t "$SESSION_NAME"
