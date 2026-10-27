# Unity MCP и CLI (проект уровня репозитория)

Статус: конфиг и скрипт написаны; живая проверка НЕ выполнена (см. «Находки»).

## Что настроено
- `.mcp.json` в корне репо: сервер `unity-editor-mcp` = `unity mcp --project-path "$PWD/unity"` (через `sh -c`, подгружает `~/.unity/env`).
- Claude Code подхватывает файл при запуске в корне репо; проектные MCP-серверы пользователь одобряет при первом использовании.
- `tools/mcp-smoke.mjs` – дымовой тест MCP (Node, без зависимостей): initialize, tools/list, один tools/call. Дефолт инструмента (`script_eval`, код `return UnityEngine.Application.unityVersion;`) НЕ сверен с живым списком – при первом запуске поправить `--tool`/`--args` и константы.
- `docs/tooling/unity-cli-skill.md` – навык CLI (`unity skill show`) для цитирования в брифах.

## Открыть / закрыть редактор
```
unity open unity
unity status --until-ready --project-path "$PWD/unity" --timeout 600 --format json
unity close unity --timeout 60
```
Перед каждым вызовом: `source ~/.unity/env; export UNITY_NO_BANNER=1 UNITY_NON_INTERACTIVE=1 UNITY_NO_PAGER=1`.

## Проверка
```
unity command --project-path "$PWD/unity"                      # список команд
unity command --project-path "$PWD/unity" --query eval --detail full   # схема
node tools/mcp-smoke.mjs                                         # MCP: MCP_TOOLS=n, MCP_CALL_OK, MCP_EXIT=0
```
Точный синтаксис `script eval` не проверен (редактор не загрузился).

## Имена команд Pipeline 0.9.1
Команды сгруппированы (`editor.playmode start`, `script eval`, `capture game_view`, `editor set_auto_tick`); старые плоские имена (`editor_play`, `eval`, `capture_game_view`, `set_autotick`) – устаревшие алиасы.

## Только по решению владельца (не применено)
Глобальная запись для Claude Code (`unity mcp configure claude-code --dry-run`):
```
Running: claude mcp add --scope user --transport stdio unity-editor-mcp unity mcp -- --project-path /Users/letulip/Projects/Claude/DesktopDrift3d/unity
```
Запись для Claude Desktop (`unity mcp configure claude --dry-run`):
```
Would write to: /Users/letulip/Library/Application Support/Claude/claude_desktop_config.json
{
  "mcpServers": {
    "unity": {
      "command": "/Users/letulip/.unity/bin/unity",
      "args": [
        "mcp",
        "--project-path",
        "/Users/letulip/Projects/Claude/DesktopDrift3d/unity"
      ]
    }
  }
}
```
- `unity skill install claude-code` – пишет навык в `~/.claude/skills/unity-cli` (глобально).
- `unity setup claude` – ставит плагин Unity с навыками в Claude Code (глобально).

## Находки
- `unity open unity` (20:58 WITA) запустил редактор 6000.3.27f1, но за 600 с `unity status --until-ready` не увидел инстансов: процесс (PID 43582) крутился на ~99% CPU, Editor.log остановился на лицензировании (Licensing Background thread has ended), проект не загрузился. `unity close` вернул Closed=false, method=none; процесс завис, убить его без одобрения не удалось.
- `unity open` пишет «"unity" was not found in the Hub project registry, opening as a file path» – безвредно.
- `unity open` блокирует вызов дольше 120 с.
