# docs/tooling – контур разработки через Unity CLI (SPEC-00)

Версии: Unity **6000.3.27f1** · Unity CLI **1.0.0-beta.14** (обновлён 28.10.2026 шагом 0 Волны 2 по D-20 №7 с beta.13; полный прогон шести скриптов и MCP зелёный, флаги скриптов не менялись; новое в beta.14: `unity terms status/accept`, `unity mcp --tools/--exclude-tools`, коды `OPEN/STATUS_BLOCKED_BY_DIALOG_*`, `unity open --until-ready`) ·
Pipeline-пакет `com.unity.pipeline` **0.9.1-exp.1** · dotnet SDK 10.0.402 (установлен только рантайм .NET 10,
поэтому `core-tests` – `net8.0` + `RollForward=Major`) · Node v26 · ffmpeg (яркость кадра).

Первая диагностика – `unity doctor` (видит редактор), затем `unity status --project-path "$PWD/unity"`
(открыт ли редактор на проекте). Во всех скриптах: `source ~/.unity/env; export UNITY_NO_BANNER=1
UNITY_NON_INTERACTIVE=1 UNITY_NO_PAGER=1`. Контракт скриптов: `set -euo pipefail`, последняя строка –
`<NAME>_EXIT=<код>`, большие логи только грепать (`grep -n "error CS\|Exception" … | head -40`).

| Скрипт | Команда CLI | Запасной путь | Печатает |
|---|---|---|---|
| `scripts/test.sh` | `dotnet test core-tests -v q --nologo` (без Unity; те же `.cs`, что и в Unity) | – | `TEST_TIME`, `TEST_EXIT` |
| `scripts/trace.sh` | `node tools/parity-trace.mjs` – JS `stepCar` → `tools/parity-trace.json` (1200 кадров, sha256) | `DESKTOPDRIFT_DIR=…`, если 2D-игра лежит не в `/Users/letulip/Projects/Claude/DesktopDrift` | `TRACE_EXIT` |
| `scripts/compile.sh` | редактор открыт → `unity recompile --project-path unity --json` (повтор до 6×5 с, пока редактор докомпилирует после `unity open`); иначе `unity run unity --timeout 600 --no-tail -l artifacts/compile.log` и ошибки из лога (`error CS`) | `grep -n "error CS\|Exception" artifacts/compile.log` | `COMPILE_ERRORS`, `COMPILE_WARNINGS`, `COMPILE_TIME`, `COMPILE_EXIT` |
| `scripts/unity-tests.sh [фильтр]` | `unity test unity --mode EditMode --output artifacts/editmode.xml --report-format nunit,junit --junit-output artifacts/editmode-junit.xml --timeout 900 --json`; `UNITY_TESTS_MODE=PlayMode` (SPEC-02) – то же с `--mode PlayMode` и отчётами `artifacts/playmode*.xml` (сцена `Game`, ~30 с) | `--filter <Класс>` для одного класса; `--report-format both` CLI не принимает | `UNITY_TESTS_TOTAL`, `UNITY_TESTS_FAILED`, `UNITY_TESTS_TIME`, `UNITY_TESTS_EXIT` |
| `scripts/build.sh android\|mac` | `unity build unity --profile Android --output-path artifacts/build/android/DesktopDrift.apk --android-export-type apk --allow-dirty-build --no-tail --timeout 2400 -l artifacts/build-android.log`; `mac` – `--target StandaloneOSX`, настольная проверочная сборка (Mono, universal; 28.10: 65 с, 114 МБ), не релизная цель | профиль пересоздать: `unity build unity --create-profile Android` (Android – не zero-code цель) | `artifacts/build/report.md` (с коммитом и грязью дерева; рядом с артефактом CLI пишет `*.provenance.json`), `BUILD_GIT`, `BUILD_DIRTY`, `BUILD_SIZE`, `BUILD_TIME`, `BUILD_EXIT` |
| `scripts/capture.sh` | `unity command --project-path unity …`: `scene open` → `editor set_auto_tick` → `editor.playmode start` → (`CAPTURE_DELAY` секунд) → `capture game_view --source camera` → `editor.playmode stop`; PNG из `Assets/Captures/` переносится в `artifacts/capture/<ts>.png`, яркость через ffmpeg (> 0.05). Dev-хуки SPEC-02 (headless-редактор наследует окружение): `CAPTURE_SCENE` (дефолт `Assets/Scenes/Game.unity`), `CAPTURE_DELAY=12` – кадр на 12-й секунде заезда, `DD_STEER=trace` – руль из `tools/engine-trace-oval.json` (только редактор), `DD_CAMERA_MODE=C1\|C2` – режим камеры | нет редактора → скрипт сам поднимает headless-редактор (`-batchmode` без `-quit`), снимает кадр и закрывает его; `CAPTURE_SOURCE=screen` – снимок Game View (только GUI-редактор) | `CAPTURE_FILE`, `CAPTURE_SIZE`, `CAPTURE_MEAN`, `CAPTURE_FRAMES`, `CAPTURE_TIME`, `CAPTURE_EXIT` |
| `node tools/mcp-smoke.mjs` | `unity mcp --project-path unity` (stdio JSON-RPC): `initialize` → `tools/list` → `tools/call eval` | `--tool <имя> --args '<json>'` | `MCP_SERVER`, `MCP_TOOLS`, `MCP_CALL_OK`, `MCP_EXIT` |

Один редактор на проект: `unity run` / `unity test` / `unity build` при открытом редакторе падают на
блокировке проекта – сначала `unity status`. Открыть/закрыть редактор (GUI и headless-резидент), MCP и
имена команд Pipeline – [unity-mcp.md](unity-mcp.md); навык CLI для цитирования в брифах –
[unity-cli-skill.md](unity-cli-skill.md) (`unity skill show`); установка – [unity-install.md](unity-install.md).

Политика обновления CLI (D-20 №7): версия фиксируется на волну; обновление – только на границе волн решением
архитектора: `unity self-update --changelog` → `unity self-update --target <версия>` → при необходимости
`unity pipeline upgrade --project-path unity` (`command`/`status`/`mcp` зависят от версии Pipeline-пакета) → все шесть
скриптов и `tools/mcp-smoke.mjs` → обновить эту таблицу и `unity-cli-skill.md`; при поломке `unity self-update --rollback`.

Особенности beta.13, учтённые в скриптах: `unity run` сам добавляет `-batchmode`/`-quit` и отвергает их после
`--`; `unity projects new` отказывает, если папка существует (проект создан во временной папке, перенесены
`Assets/`, `Packages/`, `ProjectSettings/`); `unity templates create` – упаковка своего шаблона, а не создание
проекта; GUI-запуск `unity open` при первом старте ждёт, когда владелец примет Unity Software Terms (принято 28.10; beta.14
умеет `unity terms accept`); Pipeline `save_path` всегда разрешается под `Assets/`; `--allow-dirty-build` снимает защиту
от незакоммиченного дерева – коммит и грязь фиксируют отчёт сборки и `*.provenance.json`.
