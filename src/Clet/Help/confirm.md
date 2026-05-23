## Examples

```sh
# Yes/No confirmation:
clet confirm --prompt "Deploy to production?"

# --title and --prompt are interchangeable:
clet confirm --title "Delete 40k rows?"

# Default to yes:
clet confirm --initial "true" --title "Continue?"

# Use in a script:
if clet confirm --prompt "Apply patch?"; then
  git apply patch.diff
fi

# JSON output:
clet confirm --json --prompt "OK?"
# → {"schemaVersion":1,"status":"ok","value":true}
# → {"schemaVersion":1,"status":"ok","value":false}
```
