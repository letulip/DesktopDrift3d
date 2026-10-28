# SPEC-00 – C#-контур разработки (репозиторий, Core под `dotnet test`, Unity из CLI, сборка, MCP, кадры)

> Решение D-06: контур строится первым, до любого геймплея. Пока он не работает, никакой другой
> этап не стартует: без него агенты не могут проверить свою работу иначе, чем глазами владельца.
> Часть A не требует Unity; часть B идёт следом в той же волне – Unity 6000.3.27f1 и официальный
> Unity CLI установлены 27.10.2026. Все Unity-операции идут через CLI, не через самодельные
> batchmode-флаги (решение D-18).

## Цель

Правка в Core проходит путь «код → `dotnet test` зелёный → (при Unity) EditMode зелёный →
сборка из CLI → кадр из play mode» без единого клика в редакторе. Доказательство контура –
дословный перенос `physics.js` с трассой паритета 1e-6.

## Вход

- Папка `DesktopDrift3d` с пакетом документов (есть). `DesktopDrift` – только чтение.
- Часть B: Unity 6000.3.27f1 (`/Applications/Unity/Hub/Editor/6000.3.27f1`) с Android Build Support
  (SDK android-34…37, NDK, OpenJDK) и **официальный Unity CLI 1.0.0-beta.13** (`~/.unity/bin/unity`,
  PATH через `source ~/.unity/env`; `unity doctor` видит редактор). Есть Xcode, Blender, dotnet.
  `unity auth` не выполнен – для test/build/run не требуется.
- Исходники: `DesktopDrift/js/physics.js` (66 строк, весь), `js/config.js` строки с `PHYS_HZ`,
  `GRIP_WOBBLE`, `STEER_WOBBLE` и `CARS[0]` (Bismark, поле `_drive`), `tests/physics.test.js`.

## Шаги и диспетчеризация

Обозначение: ⟨роль · тип · модель · усилие · бюджет ходов⟩. Шаги строго по очереди.

| № | Шаг | Диспетчеризация | Выход |
|---|---|---|---|
| A1 | Скелет репозитория и ветка | ⟨Builder · general-purpose · sonnet · medium · ≤25⟩ | ветка `feat/spec-00-contour`, папки по `03-architecture.md` §2, `scripts/*.sh` заглушки с `exit 1` |
| A2 | Core и тесты под dotnet | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `unity/Assets/Core/DesktopDrift.Core.asmdef`, `core-tests/DesktopDrift.Core.Tests.csproj`, `scripts/test.sh` |
| A3 | Перенос `physics.js` | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | `Core/Physics/*.cs`, `Core.Tests/PhysicsTests.cs` зелёные |
| A4 | Трасса паритета | ⟨Builder · general-purpose · sonnet · high · ≤50⟩ | `tools/parity-trace.mjs`, `tools/parity-trace.json`, `Core.Tests/ParityTraceTests.cs`, `scripts/trace.sh` |
| A5 | Отчёт части A и карточка | ⟨Builder · general-purpose · haiku · low · ≤15⟩ | `artifacts/report-spec00-A.md`, обновление `docs/CURRENT.md` |
| B1 | Unity-проект через CLI | ⟨Builder · general-purpose · sonnet · high · ≤50⟩ | `unity/` из шаблона URP (`unity templates`), пакеты, `ProjectSetup.Apply()` через `unity run` |
| B2 | Сборки asmdef и компиляция | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | четыре asmdef, `scripts/compile.sh` поверх `unity run` / `unity recompile` → `COMPILE_EXIT=0` |
| B3 | EditMode-тесты из CLI | ⟨Builder · general-purpose · sonnet · medium · ≤30⟩ | `scripts/unity-tests.sh` поверх `unity test` → NUnit XML + код выхода; те же тесты, что в A3/A4, зелёные в Unity |
| B4 | Сборка из CLI | ⟨Builder · general-purpose · sonnet · high · ≤50⟩ | `scripts/build.sh android|mac` поверх `unity build`, отчёт размера и времени |
| B5 | Официальный MCP-мост и навык агента | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `unity mcp` настроен на проект, `docs/tooling/unity-mcp.md`, навык CLI сохранён в `docs/tooling/unity-cli-skill.md` |
| B6 | Кадр из play mode | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `scripts/capture.sh` поверх `unity command` (Pipeline) или `unity run` + RenderTexture → `artifacts/capture/*.png` |
| B7 | Документация контура | ⟨Builder · general-purpose · haiku · low · ≤15⟩ | `docs/tooling/README.md`: карта скриптов ↔ команд CLI, версия CLI; раздел «Команды» в `CLAUDE.md` актуален |

### A1 · Скелет

`git checkout -b feat/spec-00-contour`; папки `unity/Assets/{Core,Core.Tests,Game,Game.Tests,Editor,Content,Art,UI,Audio,Scenes,Settings}`,
`core-tests/`, `tools/`, `scripts/`, `artifacts/.gitkeep`. Все `scripts/*.sh` – `#!/usr/bin/env bash`,
`set -euo pipefail`, печатают `<NAME>_EXIT=<код>` последней строкой (контракт для Gate-runner).
`.gitignore` дополнить: `unity/Library/ unity/Temp/ unity/Logs/ unity/UserSettings/ unity/Build/ core-tests/bin core-tests/obj artifacts/*`.

### A2 · Core под dotnet

- `DesktopDrift.Core.asmdef`: `{"name":"DesktopDrift.Core","noEngineReferences":true,"autoReferenced":true}` (пишется руками, Unity подхватит в B2).
- `core-tests/DesktopDrift.Core.Tests.csproj`: `net8.0`, NUnit 3 + `NUnit3TestAdapter` + `Microsoft.NET.Test.Sdk`;
  `<Compile Include="../unity/Assets/Core/**/*.cs" />` и `<Compile Include="../unity/Assets/Core.Tests/**/*.cs" />`;
  `.meta` не мешают (не .cs). `Nullable` enable, `LangVersion` 9 (Unity 6 = C# 9).
- `scripts/test.sh`: `dotnet test core-tests -v q --nologo 2>&1 | tail -15; echo TEST_EXIT=$?` с замером `time`.
- Проверка: пустой тест `SmokeTests.OnePlusOne` зелёный, время прогона в отчёте.

### A3 · Перенос `physics.js` (feel-critical)

Дословно, порядок операций не менять. Структуры в `Core/Physics/`:
`DriveProfile` (maxSpeed, thrust, steer, lowSpeedTurn, steerSmooth, selfAlign, grip, driftSteerBoost, rollFriction, driftDrag),
`PhysicsConstants` (PhysHz, GripWobble, SteerWobble), `CarState` (x, y, vx, vy, angle),
`StepMotion` (steerSmooth, physT), `StepResult` (drifting, speed, vS, fwd, side).
`CarPhysics.StepCar(CarState car, StepMotion sm, double steerTarget, DriveProfile p, PhysicsConstants k, double dt)` –
мутирует `car` и `sm`, возвращает `StepResult`; `Math.Pow`, `Math.Hypot` (есть в .NET 8 как `double.Hypot`;
в Unity (netstandard 2.1) – `Math.Sqrt(x*x+y*y)`: использовать ОДНУ реализацию `CoreMath.Hypot`, чтобы оба
мира считали одинаково). `isDrifting` – из `Scoring` (порт тех же двух констант уже здесь, остальной
`scoring.js` – SPEC-01). Каждый тест из `tests/physics.test.js` → `[Test]` с теми же числами и допусками.
Заголовок файла по `03-architecture.md` §11.

### A4 · Трасса паритета

`tools/parity-trace.mjs` (Node ESM): `import { stepCar } from '<abs>/DesktopDrift/js/physics.js'`,
константы и `CARS[0]._drive` из `config.js`. Начальное состояние `{x:0,y:0,vx:0,vy:0,angle:0}`,
`sm={steerSmooth:0,physT:0}`, `dt=1/60`, 1200 кадров. Ввод: блоки по 20 кадров, значение
`[0, 1, 1, 0, -1, -1, 0, 1, -1, 0][блок % 10]`. Выход `tools/parity-trace.json`:
`{ "source": {"commit": <sha DesktopDrift>, "car": "bismark"}, "drive": {...}, "k": {...}, "dt": 0.016666…,
"steer": [...], "frames": [[x,y,angle,vx,vy], ...] }` – профиль и константы ПЕРЕНОСЯТСЯ в файл, C#-тест
берёт их оттуда, ничего не перепечатывая. `ParityTraceTests`: читает JSON (`System.Text.Json` недоступен
в Unity – парсер ручной минимальный в тесте или `MiniJson` в `Core.Tests`; допустимо хранить трассу
ещё и как CSV для простоты), воспроизводит, `Assert.LessOrEqual(Math.Abs(Δ), 1e-6)` по каждой величине
каждого кадра; в отчёте – максимальная |Δ| по полям. `scripts/trace.sh` перегенерирует JSON и
печатает `TRACE_EXIT`.

### B1 · Unity-проект через CLI

Все скрипты начинаются с `source ~/.unity/env` и `export UNITY_NO_BANNER=1 UNITY_NON_INTERACTIVE=1 UNITY_NO_PAGER=1`.
`unity templates list --editor-version 6000.3.27f1` → шаблон URP 3D; `unity templates create …` (флаги
из `--help`) в `unity/`. Если шаблоны требуют входа в аккаунт – запасной путь:
`unity run unity -- -createProject "$PWD/unity" -quit`. Затем `Packages/manifest.json`:
`com.unity.render-pipelines.universal`, `com.unity.inputsystem`, `com.unity.test-framework`,
`com.unity.nuget.newtonsoft-json`, `com.unity.ugui`, плюс Pipeline-пакет Unity CLI (см. `unity pipeline --help`;
он регистрирует команды для `unity command`/`unity list`), ничего лишнего. `Editor/Setup/ProjectSetup.Apply()`
вызывается `unity run unity -l artifacts/setup.log -- -executeMethod DesktopDrift.Editor.ProjectSetup.Apply -quit`
(аргументы после `--` уходят редактору): URP-ассет в Graphics/Quality, `ScriptingBackend=IL2CPP`,
`TargetArchitectures=ARM64`, `minSdk=26`, `targetSdk` = максимальный установленный (android-37),
ориентация – автоповорот Portrait + LandscapeLeft/Right до решения на гейте SPEC-02 (D-19), Input System «new only», Linear, `ProjectVersion.txt` = 6000.3.27f1. Идемпотентно.

### B2 · asmdef и компиляция

`Core` (есть), `Core.Tests` (`"includePlatforms":["Editor"]`, `"references":["DesktopDrift.Core"]`,
`"optionalUnityReferences":["TestAssemblies"]`), `Game` (`references`: Core, `Unity.InputSystem`),
`Editor` (`includePlatforms":["Editor"]`, references Core, Game). `scripts/compile.sh`: без открытого
редактора – `unity run unity --timeout 600 -l artifacts/compile.log -- -quit`, затем
`grep -n "error CS\|Exception" artifacts/compile.log | head -40`; с открытым (`unity status` показывает
проект) – `unity recompile --project-path unity --strict --json`. Печатает `COMPILE_EXIT`.

### B3 · EditMode из CLI

`scripts/unity-tests.sh`: `unity test unity --mode EditMode --output artifacts/editmode.xml --report-format both
--junit-output artifacts/editmode-junit.xml --json`; парсит `total`/`failed` из XML и печатает
`UNITY_TESTS_EXIT`. `--filter <Class>` для одного класса; `--affected` – позже, когда тестов много. Те же
тест-файлы, что в dotnet, зелёные здесь (разница окружений – первый же сигнал).

### B4 · Сборка из CLI

`unity build --list-targets` → имена целей. `scripts/build.sh android`: `unity build unity --target Android
-o artifacts/build/android/DesktopDrift.apk -l artifacts/build-android.log` (встроенная сборка; при необходимости
`--create-profile Android` и `--profile`); `scripts/build.sh mac`: `--target StandaloneOSX` – только как
временная замена с пометкой «долг». Пользовательские шаги (подпись AAB, версия) – SPEC-09 через
`--execute-method DesktopDrift.Editor.Build.Android`. Отчёт `artifacts/build/report.md`: размер, время,
стриппинг, версия Unity и CLI. Печатает `BUILD_EXIT`.

### B5 · Официальный MCP-мост и навык агента

`unity mcp --help` → настроить официальный MCP-сервер редактора на проект (`--project-path unity`,
подкоманда `configure` – точные флаги из help); запись уровня проекта допустима, любое изменение
`~/.claude` или глобального конфига клиента – ТОЛЬКО показать владельцу. `unity skill show >
docs/tooling/unity-cli-skill.md` (навык CLI для цитирования в брифах агентов). `unity setup claude`
(плагин Unity с навыками в Claude Code) – решение владельца, не запускать самим. Проверка: при открытом
редакторе (`unity open unity` → `unity status`) одна операция через MCP и одна через `unity command`
(список: `unity command --project-path unity`), затем `unity close unity`. Описать в
`docs/tooling/unity-mcp.md`: что настроено, как открыть/закрыть редактор, как проверить.

### B6 · Кадр из play mode

Предпочтительно через живой редактор: `unity command --project-path unity --query capture` (и `play`) –
если Pipeline даёт команды входа в play mode и снимка, `scripts/capture.sh` оборачивает их. Иначе
headless-путь: `Editor/Capture/Capture.Frame()` через `unity run unity -- -executeMethod
DesktopDrift.Editor.Capture.Frame` – камера сцены в `RenderTexture` 1920×1080 → `ReadPixels` → PNG в
`artifacts/capture/<timestamp>.png`. `ScreenCapture.CaptureScreenshot` в batchmode не использовать.
`unity run --no-tail` и `--timeout` – обязательны в скриптах.

### B7 · Документация контура

`docs/tooling/README.md`: таблица «скрипт → команда CLI → запасной путь → что печатает», версия CLI
(`unity version`), `unity doctor` как первая диагностика, политика обновления CLI. Раздел «Команды» в
`CLAUDE.md` актуален.

## Артефакты

`scripts/{test,trace,compile,unity-tests,build,capture}.sh`, `core-tests/*.csproj`,
`unity/` проект, `Core/Physics/*`, `Core.Tests/{Physics,ParityTrace}Tests.cs`, `tools/parity-trace.*`,
`docs/tooling/{README,unity-mcp}.md`, `artifacts/report-spec00-{A,B}.md`.

## Вне объёма

Любая геймплейная логика кроме `physics.js`; экспорт данных (SPEC-01); камера; UI; подпись AAB.

## Acceptance-критерии

- `scripts/test.sh` → `TEST_EXIT=0`, ≥ все тесты из `physics.test.js` + паритет, время ≤ 30 с.
- `scripts/trace.sh` воспроизводим: два запуска дают побайтно равный JSON.
- Паритет: max |Δ| ≤ 1e-6 по x, y, angle, vx, vy на всех 1200 кадрах.
- (B) `compile.sh` → 0 ошибок; `unity-tests.sh` → те же тесты зелёные (NUnit XML от `unity test`);
  `build.sh android` → APK и отчёт (`mac` – только как помеченный долг); `capture.sh` → PNG не
  чёрный (средняя яркость > 0.05); `unity status` видит проект; одна операция через официальный
  MCP из сессии архитектора прошла.
- Отчёт с цифрами: время компиляции, время EditMode, время и размер сборки.

## Гейт архитектора

Запускаю все шесть скриптов сам в свежей сессии. Проверяю: Core не ссылается на UnityEngine
(`grep -r "using UnityEngine" unity/Assets/Core` пусто); hypot одна реализация; трасса паритета
хранит профиль в файле, а не в коде; скрипты печатают `*_EXIT`; идемпотентность `ProjectSetup`.
Вердикт и цифры → `CURRENT.md`, карточка волны.

## Риски

- Unity CLI в бете (1.0.0-beta.13): команды и флаги могут меняться – версия фиксируется в
  `docs/tooling/README.md`, `unity self-update` только решением архитектора; у каждого скрипта рядом
  описан запасной путь через `unity run unity -- <batchmode-флаги редактора>`.
- Часть команд (templates, cloud) может потребовать `unity auth login` – владельцу вход не обязателен;
  если `templates create` без входа недоступен, запасной путь – `-createProject` через `unity run`.
- `double.Hypot` vs `Math.Sqrt` даёт расхождение в последнем ulp → одна реализация (см. A3).
- Кадр из batchmode без окна может выйти чёрным: сначала путь через живой редактор (`unity open` +
  `unity command`), затем `unity run` без `-nographics`; если оба чёрные – долг, кадры снимает владелец.
- Pipeline-пакет CLI (регистрирует команды для `unity command`) должен остаться dev-only и не попасть
  в Player-сборку – проверить в отчёте B4.

## Связи

`03-architecture.md` §2–§4, §10; SPEC-01 (следующий); решения D-06, D-14.

## Находки гейта

Гейт пройден 27.10.2026 (ветка `feat/spec-00-contour`, карточка `docs/handoff/wave-1.md`). Цифры: test.sh 5/5 за 1 с;
trace.sh воспроизводим (sha256 9c4f4660…3604); паритет max |Δ| x 2.7e-12 · y 4.5e-13 · angle 6.7e-16 · vx 6.3e-13 ·
vy 4.0e-13; compile.sh 0 ошибок, 5–6 с (холодный импорт 54 с); unity-tests.sh 5/5 за 6–8 с; build.sh android APK 33 МБ,
холодная 222 с, инкрементальная 19–21 с; capture.sh 1920×1080, яркость 0.518; MCP 166 инструментов, `eval` прошёл;
`unity status` видит проект.

1. **GUI-редактор ждёт владельца.** `unity open unity` на этой машине висит (100% CPU, RSS 33 МБ): стек –
   `SoftwareTermsWindow::EnsureTheUserHasAcceptedSoftwareTerms` → модальное окно Software Terms при первом
   GUI-запуске; batchmode окно не показывает. Действие владельца: открыть Unity в GUI один раз, принять. Обход:
   headless-резидент (`Unity -batchmode -projectPath … -logFile …` без `-quit`) – его видят `unity status`,
   `unity command`, `unity mcp`; закрывается `unity close unity`. Описано в `docs/tooling/unity-mcp.md`.
2. CLI beta.13 ≠ спека: `unity templates create` – упаковка своего шаблона, проект создаётся `unity projects new`
   (отказывает, если папка существует → временная папка + rsync `Assets/Packages/ProjectSettings`; регистрирует
   проект в реестре Hub). `unity run` сам добавляет `-batchmode -quit` и отвергает их после `--`.
   `unity test --report-format both` → `nunit,junit`. Android – не zero-code цель: Build Profile
   (`unity build unity --create-profile Android`, затем `--profile Android`).
3. Диспетчеризация: asmdef перенесены из B2 в B1 (первый импорт должен видеть Core.Tests как тестовую сборку,
   иначе Safe Mode); B2 = ProjectSetup + compile.sh; B5 в два захода из-за п. 1.
4. compile.sh живой путь без `--strict`: пустая сборка `DesktopDrift.Game` даёт структурное предупреждение «will not
   be compiled» до SPEC-02. CS8632 в Core.Tests снят `#nullable enable` (Unity не включает nullable-контекст).
5. Pipeline 0.9.1-exp.1: имена команд сгруппированы (`script eval`, `scene open`, `editor.playmode start`,
   `capture game_view`), старые – алиасы; после `editor.playmode start` сервер недоступен ~3 с; `save_path` всегда
   под `Assets/` (capture.sh переносит PNG в artifacts/ и чистит `Assets/Captures`); runtime Pipeline в плеер не
   попадает без define-символа (в APK 0 строк `Unity.Pipeline`); headless `source=screen` недоступен – принят
   `source=camera`.
6. Окружение: только рантайм .NET 10 → csproj `net8.0` + `RollForward=Major`; `config.js` создаёт `Path2D` при
   загрузке → стаб в трассе; URP-ассеты шаблона пересериализуются редактором при первом открытии/сборке
   (закоммичены); Android-сборка оставляет `unity/.utmp/` (в .gitignore); лицензия Android SDK не спрашивалась.
7. Хвосты закрыты 28.10 ответами владельца (D-19, D-20): `build.sh mac` проверен (65 с, 114 МБ, universal Mono, долг
   снят; отчёт сборки получил коммит и грязь дерева, CLI пишет `*.provenance.json`); плейсхолдеры остаются до SPEC-09;
   глобальные конфиги не применяются; ориентация – автоповорот Portrait + оба Landscape до гейта SPEC-02. GUI-путь –
   п. 9.
8. `ProjectSetup`: сеттеры `PlayerSettings.*` сами не записали `ProjectSettings.asset` в batchmode (28.10, запуск 1:
   в логе `allowedAutorotateToPortrait False -> True`, файл без изменений, `AssetDatabase.SaveAssets()` вызывался);
   с явным `EditorUtility.SetDirty` синглтона PlayerSettings перед `SaveAssets` – записано, третий запуск без диффа.
   В B2 запись проходила (файл, видимо, был dirty после первого импорта) – идемпотентность тогда это скрыла.
9. GUI-путь проверен 28.10 после принятия Software Terms: `unity open` → ready за 6 с; живой `compile.sh` 0 ошибок (сразу
   после открытия `unity recompile` даёт код 6 без ошибок CS – редактор ещё компилирует; в скрипт добавлен повтор до
   6×5 с); `capture.sh` из GUI: камера 0.518 и экран (`CAPTURE_SOURCE=screen`) 0.522, 1920×1080; без фокуса окна
   `frameCount` в GUI не растёт (2→2) – покадровые проверки на headless-резиденте; `unity close` graceful.
10. **Приёмка архитектора на `main` (28.10.2026, после мержа PR #1, f0267c7).** Все шесть скриптов и MCP повторены
   из свежего чекаута: test.sh 5/5 за 2 с; trace.sh два запуска побайтно равны (sha256 9c4f4660…); паритет max |Δ|
   2.7e-12; compile.sh headless 0 ошибок, 0 предупреждений, 11 с; unity-tests.sh 5/5 за 6 с; ProjectSetup.Apply повторно –
   0 изменённых файлов; build.sh android 33 МБ за 26 с (коммит f0267c7, дерево чистое), в APK только `lib/arm64-v8a`,
   строк `Unity.Pipeline` в метаданных 0; capture.sh 1920×1080, яркость 0.518, кадры 22055→22594; резидент ready за 2 с,
   `script eval` и MCP `eval` вернули 6000.3.27f1, `unity close` graceful. Статика: в Core 0 ссылок на UnityEngine,
   `noEngineReferences: true`, одна реализация `CoreMath.Hypot`, порядок операций `StepCar` совпадает с `physics.js`,
   профиль машины и константы читаются из `parity-trace.json` (в C# не перепечатаны), все скрипты заканчиваются
   `<NAME>_EXIT`. Находка: после прогона гейтов редактор пересериализовал
   `unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` (как URP-ассеты в п. 6) – закоммичен в ветке
   Волны 2, чтобы дерево после гейтов оставалось чистым и `BUILD_DIRTY` не врал. Вердикт: SPEC-00 ⬛.
