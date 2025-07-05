#!/bin/bash

# Check for required argument
if [ -z "$1" ]; then
    echo "Usage: $0 <fungible_json_path>"
    exit 1
fi

FUNGIBLE_JSON="$1"
CSV_FILE="addresses.csv"

# Check if CSV exists
if [ ! -f "$CSV_FILE" ]; then
    echo "❌ Error: CSV file '$CSV_FILE' not found."
    exit 1
fi

# Convert CSV to JSON array: {"Address": "..."}, only keep rows starting with "P"
CSV_JSON=$(mktemp)
grep '^P' "$CSV_FILE" | jq -R '{Address: .}' | jq -s '.' > "$CSV_JSON"

# Filter out addresses that don't start with "S" and are not in the CSV
jq --slurpfile known "$CSV_JSON" '
  map(select(.Address | startswith("P"))) |
  map(select(.Address as $a | ($known[] | map(.Address)) | index($a) | not))
' "$FUNGIBLE_JSON" > _missing.json

# Unique missing addresses
unique_addresses=$(jq -r '.[].Address' _missing.json | sort -u)
unique_count=$(echo "$unique_addresses" | wc -l)

# Save missing addresses to CSV
echo "$unique_addresses" > addresses-missing-v1.csv

# Save all addresses (from CSV + missing) to addresses-all.csv
cat "$CSV_FILE" addresses-missing-v1.csv | sort -u > addresses-all.csv

# Unique token symbols
unique_tokens=$(jq -r '.[].TokenSymbol' _missing.json | sort -u)

# Output
echo "Missing unique addresses: $unique_count"
echo
echo "List of missing addresses:"
echo "$unique_addresses"

echo
echo "Tokens found for missing addresses:"
echo "$unique_tokens"

# Cleanup
rm _missing.json "$CSV_JSON"
