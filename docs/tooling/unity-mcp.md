# Unity MCP и CLI (проект уровня репозитория)

Статус: проверено вживую на headless-резиденте (27.10) и на GUI-редакторе (28.10, после принятия владельцем Software
Terms) – 6000.3.27f1, Pipeline 0.9.1-exp.1.

## Что настроено
- `.mcp.json` в корне репо: сервер `unity-editor-mcp` = `unity mcp --project-path "$PWD/unity"` (через `sh -c`, подгружает `~/.unity/env`).
- Claude Code подхватывает файл при запуске в корне репо; проектные MCP-серверы пользователь одобряет при первом использовании.
- `tools/mcp-smoke.mjs` – дымовой тест MCP (Node, без зависимостей): initialize, tools/list, один tools/call. Дефолт – инструмент `eval` (`return UnityEngine.Application.unityVersion;`); `--tool/--args` переопределяют.
- `docs/tooling/unity-cli-skill.md` – навык CLI (`unity skill show`) для цитирования в брифах.

## Открыть / закрыть редактор
Перед каждым вызовом: `source ~/.unity/env; export UNITY_NO_BANNER=1 UNITY_NON_INTERACTIVE=1 UNITY_NO_PAGER=1`.

(а) GUI (Software Terms приняты владельцем 28.10; до этого `unity open` висел в их модальном окне):
```
unity open unity
unity status --until-ready --project-path "$PWD/unity" --timeout 600
unity close unity --timeout 60
```
(б) Резидентный headless-редактор (без GUI, диалога условий нет):
```
nohup "/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity" -batchmode -projectPath "$PWD/unity" -logFile artifacts/headless-editor.log >/dev/null 2>&1 & disown
unity status --project-path "$PWD/unity"     # готовность (редактор в списке, state ready, порт 7800)
unity pipeline list                          # Server Reachable true
unity close unity --timeout 60               # остановка (или --force)
```
Не запускать `unity run/test/build` при живом редакторе – столкновение.

## Проверка
```
unity status --project-path "$PWD/unity" --format json
unity command --project-path "$PWD/unity" script eval -- --code "return UnityEngine.Application.unityVersion;"   # 6000.3.27f1
node tools/mcp-smoke.mjs 2>&1 | grep -v "^\[unity mcp\]"
scripts/compile.sh 2>&1 | tail -4        # живая ветка: unity recompile, COMPILE_WARNINGS=0
```
Вывод MCP: `MCP_SERVER=unity-mcp 1.0.0-beta.13 protocol=2025-06-18`, `MCP_TOOLS=166`, `MCP_CALL_OK eval`, `MCP_EXIT=0`. Инструмент MCP называется `eval`, не `script_eval`.

## Имена команд Pipeline 0.9.1
| Команда | Пример |
|---|---|
| `script eval` | `script eval -- --code "return 1+1;"` |
| `scene open` | `scene open -- --path Assets/Scenes/SampleScene.unity` |
| `editor set_auto_tick` | `editor set_auto_tick` |
| `editor.playmode start\|stop` | `editor.playmode start` |
| `editor status` | `editor status` |
| `capture game_view` | `capture game_view -- --source camera --width 1920 --height 1080 --save_path Captures/x.png` |

Все – через `unity command --project-path "$PWD/unity" <имя> …`. Старые плоские имена (`eval`, `editor_play`, `capture_game_view`, `set_autotick`) – устаревшие алиасы.

## Глобальные записи – разрешены владельцем 28.10, не применены решением архитектора (D-20 №2–3)
Причины: проектный `.mcp.json` покрывает все сессии Claude Code в репозитории и его worktree; глобальная запись
привязана к пути одного проекта и поднимала бы сервер в каждой сессии любого проекта; Claude Desktop получил бы 166
схем инструментов в каждый чат. Команды – на случай, если Unity понадобится из обычного чата.

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
- `unity setup claude` = `claude plugin marketplace add Unity-Technologies/unity-agent-plugin --scope user` +
  `claude plugin install unity@unity-agent-plugin --scope user` – бета-плагин в user-scope, во всех сессиях Claude Code на маке.

## Находки
- Зависание GUI-запуска: `unity open unity` стоял в модальном окне «Unity Software Terms» (стек `SoftwareTermsWindow::EnsureTheUserHasAcceptedSoftwareTerms`), которое принять может только владелец; процесс крутился на ~99% CPU, проект не грузился, `unity status --until-ready` не видел инстансов. Диагностика: `sample <pid>`. Закрыто: `unity close unity --force`. Batchmode окно не показывает.
- `unity status` видит и batch-редактор (state ready, порт 7800).
- После `editor.playmode start` сервер Pipeline недоступен ~3 с (domain reload) – ждать и повторять.
- `capture game_view --save_path` всегда разрешается внутри `Assets/`, даже для абсолютного пути.
- `unity open` пишет «"unity" was not found in the Hub project registry, opening as a file path» – безвредно; блокирует вызов дольше 120 с.
- `COMPILE_WARNINGS` в `scripts/compile.sh` берётся из поля `warnings` JSON, а не подсчётом строк.
- GUI-путь (28.10): `unity open unity` → `unity status --until-ready` ready за 6 с; `unity recompile` сразу после этого
  возвращает код 6 без ошибок CS (первая компиляция ещё идёт), через ~20 с – `status: up_to_date`; `compile.sh` повторяет
  до 6×5 с. `capture game_view --source screen` в GUI работает (1920×1080). В GUI без фокуса окна play mode почти не тикает
  (`frameCount` 2→2 между двумя чтениями, `editor set_auto_tick` не спасает) – снимки корректны, покадровые проверки делать
  на headless-резиденте. `unity close unity` закрывает GUI graceful.
- UI в кадре (29.10): UI Toolkit не попадает в камеру, нужен GUI-редактор и `CAPTURE_SOURCE=screen`. Dev-хуки `DD_*`
  читаются из окружения процесса редактора, поэтому GUI запускать бинарником с ними (`unity open` окружение не
  гарантирует):
  ```
  DD_STAND=ta nohup "/Applications/Unity/Hub/Editor/6000.3.27f1/Unity.app/Contents/MacOS/Unity" -projectPath "$PWD/unity" -logFile "$PWD/artifacts/gui-editor.log" >/dev/null 2>&1 & disown
  unity status --until-ready --project-path "$PWD/unity" --timeout 600
  unity command --project-path "$PWD/unity" script eval -- --code 'UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1080, 2400, "PocoX6"); return 0;'
  CAPTURE_SOURCE=screen CAPTURE_W=1080 CAPTURE_H=2400 scripts/capture.sh
  ```
  Без смены разрешения Game View `--width/--height` просто растягивают его кадр.
- 29.10: `capture.sh` сразу после `compile.sh` закрыл GUI-редактор: домен перезагружался, `unity status` его не видел,
  скрипт поднял второй редактор и в конце закрыл «свой» через `unity close unity`. Исправлено: редактор узнаётся по
  процессу, закрывается только запущенный скриптом и только живой.
