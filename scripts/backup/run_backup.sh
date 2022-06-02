#!/bin/bash

export PGPASSWORD="masterkey"

DIR="$(cd "$(dirname "$0")" && pwd)"

$DIR/pg_backup.sh
$DIR/pg_backup_rotated.sh

unset PGPASSWORD
