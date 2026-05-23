```
  ╔═╗╦  ╔═╗╔╦╗
  ║  ║  ╠═  ║
  ╚═╝╩═╝╚═╝ ╩
```

*One binary. Every prompt. JSON out. Go home.*

{{CLET_TABLE}}

## Global Options

| Option | Description |
|--------|-------------|
| `--initial`, `-i` `<value>` | Pre-populate the input with a value |
| `--title`, `--prompt`, `-t`, `-p` `<text>` | Custom title for the prompt window |
| `--json`, `-j` | Emit result as a JSON envelope |
| `--timeout` `<duration>` | Auto-cancel after duration (e.g. `30s`, `1m`, `500ms`) |
| `--fullscreen`, `-f` | Force fullscreen rendering (default for viewers) |
| `--output`, `-o` `<path>` | Write result to a file instead of stdout |

## JSON Output

When `--json` is passed, results are wrapped in a versioned envelope:

```json
{ "schemaVersion": 1, "status": "ok", "value": "selected text" }
```

Status is one of: `ok`, `cancelled`, `error`, `no-result`.

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | No result |
| 2 | Usage error |
| 65 | Validation error |
| 74 | I/O error |
| 130 | Cancelled |

## Usage

```
clet <alias> [positional...] [options]
clet list [--json]
clet help <alias>
clet --help
clet --version
```

[Help on help](clet:help:help)

{{VERSION}} - [clet on GitHub](https://github.com/gui-cs/clet) - By [@tig](https://github.com/tig)
