# Handoff – Волна 1: SPEC-00 C#-контур разработки (27.10.2026)

## Сделано
- A1 3e60e6a скелет репозитория, заглушки `scripts/*.sh`, `.gitignore` (архитектор)
- A2 77b09b0 `DesktopDrift.Core.asmdef` (noEngineReferences), `core-tests/*.csproj` (`net8.0` + `RollForward=Major` – на маке только рантайм .NET 10), `scripts/test.sh`
- A3 0d63e4c дословный перенос `physics.js` → `Core/Physics/*` (`CarPhysics.StepCar`, `CoreMath.Hypot` – одна реализация, `Scoring.IsDrifting`), два golden-теста из `physics.test.js`
- A4 eb9ee8b трасса паритета `tools/parity-trace.mjs` → `tools/parity-trace.json` (1200 кадров; профиль Bismark и константы читаются из `config.js`, стаб `Path2D`), `MiniJson` + `ParityTraceTests`, `scripts/trace.sh`
- A5 88ce1e8 отчёт части A (`artifacts/report-spec00-A.md`, локально), CURRENT
- B1 8a2fb8a Unity 6000.3.27f1 URP-проект через `unity projects new` (шаблон urp-blank; пакеты ровно по спеке + `com.unity.pipeline` 0.9.1-exp.1); четыре asmdef (перенесены из B2: первому импорту нужен Core.Tests как тестовая сборка)
- B2 c10727e `Editor/Setup/ProjectSetup.Apply` (идемпотентно: IL2CPP, ARM64, minSdk 26, targetSdk auto, landscape, Input System new only, Linear, URP-ассерты; productName/companyName/bundle id – плейсхолдеры), `scripts/compile.sh` (живой редактор → `unity recompile`, иначе `unity run` + лог)
- B3 3e53199 `scripts/unity-tests.sh` поверх `unity test` (NUnit + JUnit в `artifacts/`)
- B4 9048503 Build Profile Android, `scripts/build.sh android|mac` поверх `unity build`, отчёт `artifacts/build/report.md`
- B5 09ab3ef + B5b 3bc3f1a `.mcp.json` (project scope), `tools/mcp-smoke.mjs`, `docs/tooling/unity-cli-skill.md`, `docs/tooling/unity-mcp.md` (GUI и headless-резидент редактора, имена команд Pipeline, находки)
- B6 d1f892a `scripts/capture.sh` (Pipeline: `scene open` → `editor set_auto_tick` → `editor.playmode start` → `capture game_view --source camera` → `stop`; PNG в `artifacts/capture/`, яркость через ffmpeg; без редактора сам поднимает headless и закрывает)
- B7 cc3a558 `docs/tooling/README.md` (карта скрипт ↔ команда ↔ запасной путь), раздел «Команды» в `CLAUDE.md`
- гейты архитектора 11d2200, 438111a и следующий за B7: пересериализация URP-ассетов редактором, `.gitignore` для `.utmp/`, `UnityCliTemp-*`, `Assets/Captures/`
- 28.10 67ec166 ответы владельца: `ProjectSetup` – автоповорот Portrait (D-19) + `EditorUtility.SetDirty` PlayerSettings (без него сеттеры не попадали в `ProjectSettings.asset`); `build.sh` – коммит и грязь дерева в отчёте, mac без пометки долга; `capture.sh` – `CAPTURE_SOURCE`; `compile.sh` – повтор `unity recompile` до 6×5 с; ссылки на D-19 в SPEC-00/02/06/09; D-19/D-20, ответы на вопросы, CURRENT, `docs/tooling`

## Открыто
- Ответы владельца на вопросы волны получены 28.10 и записаны: D-19 (ориентация – гипотеза портрет, решение на гейте SPEC-02; `ProjectSetup` разрешает Portrait + оба Landscape) и D-20 (№0–7: Software Terms приняты, плейсхолдеры до SPEC-09, глобальные конфиги не применяются, mac-сборка проверена, dev-APK без подписи, `--allow-dirty-build` с provenance, CLI обновляется на границе волн). Открытым остаётся только сам PR
- Unity CLI beta.14 вышла 28.10 (`unity terms accept/status`, коды `*_BLOCKED_BY_DIALOG_SOFTWARE_TERMS`) – обновление шагом 0 Волны 2 по D-20 №7
- Пустая сборка `DesktopDrift.Game` даёт предупреждение «will not be compiled» до SPEC-02; поэтому живой путь `compile.sh` без `--strict`
- GUI-путь снимка (`source=screen`) не проверен – GUI-редактора нет; принят headless `source=camera`

## Файлы
- `scripts/{test,trace,compile,unity-tests,build,capture}.sh` – гейты контура, последняя строка `<NAME>_EXIT`
- `core-tests/DesktopDrift.Core.Tests.csproj` – dotnet-проект над `unity/Assets/Core` + `Core.Tests`
- `unity/Assets/Core/**` – чистая логика (`CarPhysics`, `CoreMath`, `Scoring`); `unity/Assets/Core.Tests/**` – NUnit (Physics, ParityTrace, MiniJson, Smoke)
- `unity/Assets/Editor/Setup/ProjectSetup.cs` – конфигурация проекта из CLI
- `unity/Assets/Settings/Build Profiles/Android.asset` – профиль для `unity build`
- `tools/parity-trace.mjs`, `tools/parity-trace.json` – трасса паритета; `tools/mcp-smoke.mjs` – проверка MCP
- `.mcp.json` – официальный Unity MCP на проект (Claude Code, project scope)
- `docs/tooling/{README,unity-mcp,unity-cli-skill}.md` – карта контура, редактор/MCP, навык CLI
- `docs/handoff/wave-1-questions.md` – вопросы владельцу одним списком

## Гейты
- test.sh: 5/5 зелёные, 1 с тёплый (10 с холодный) · trace.sh: 0 с, два запуска побайтно равны (sha256 9c4f4660…3604) · паритет: max |Δ| x 2.7e-12, y 4.5e-13, angle 6.7e-16, vx 6.3e-13, vy 4.0e-13 (худший кадр 750, допуск 1e-6)
- compile.sh: 0 ошибок, 0 предупреждений CS, 5–6 с тёплый headless (первый холодный импорт 54 с), живой `unity recompile` 0 с · unity-tests.sh: 5/5, 6–8 с · build.sh android: APK 33 МБ (34 546 514 Б), холодная 222 с, инкрементальная 19–21 с, только arm64, `Unity.Pipeline` в плеере нет · capture.sh: 1920×1080, яркость 0.518, кадры идут, 5 с (11 с с подъёмом headless-редактора) · `unity status` видит проект (headless-редактор ready) · MCP: 166 инструментов, вызов `eval` прошёл из сессии архитектора · ProjectSetup идемпотентен (второй запуск без диффа)
- 28.10 (после ответов владельца): build.sh mac 114 МБ, 65 с, exit 0 (universal x86_64+arm64, Mono) · ProjectSetup: автоповорот Portrait применён, третий запуск без диффа · GUI-путь после принятия Software Terms: `unity open` → ready за 6 с; живой `compile.sh` 0 ошибок (первый вызов сразу после открытия – код 6 без ошибок CS, редактор ещё компилировал → в скрипт добавлен повтор); `capture.sh` из GUI: камера 1920×1080/0.518, экран (`CAPTURE_SOURCE=screen`) 1920×1080/0.522; без фокуса окна `frameCount` в GUI не растёт (2→2) – покадровые проверки на headless-резиденте; `unity close` graceful · test.sh 5/5 за 2 с · unity-tests.sh 5/5 за 7 с

- 28.10, приёмка на `main` после мержа PR #1 (f0267c7): все шесть скриптов + MCP зелёные из свежего чекаута (test 5/5 2 с · trace sha равны · паритет 2.7e-12 · compile 0/0 11 с · EditMode 5/5 6 с · ProjectSetup без диффа · APK 33 МБ 26 с arm64 · кадр 0.518 · MCP eval ок); SPEC-00 ⬛

## Бюджет
- агентов: 12 (A2–A5, B1–B7; B5 в два захода из-за зависшего GUI-редактора) · ходов всего: ≈77 из 455 по таблице спеки · сверх бюджета: никто; A1 и все гейты – архитектор
