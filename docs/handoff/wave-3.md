# Handoff – Волна 3: SPEC-02 игровой цикл, режимы, камера C1–C3, ввод, следы, HUD-заглушка (28.10.2026)

Статус: ⬛ закрыта 29.10 по полному циклу: работа + гейт архитектора (PR #3, `main` 35b71ff) → плейтест владельца на Poco X6 («для первого прототипа это вообще топ») → раунд правок 1 (PR #4, `main` c827a00) → закрывающие документы и правило D-24 (PR #5). Ошибка процесса: раунд ушёл во вторую ветку `feat/spec-02-feel-round-1`, а пуш в смерженную `feat/spec-02-gameplay-loop` воссоздал её с дублями коммитов (ee7e0bd, 8bcb186) – конфликт PR #5; разрешён слиянием `main` в ветку волны (6a8e138), код ветки = `main`.

## Сделано
- 1 240dda2: `Game/WorldMapping` + тест, `CarView` (кубоид, интерполяция), `TrackView` (стол, лента, конусы, props), сцена `Game.unity` из `Editor/Scene/GameSceneBuilder` (идемпотентно), `Game.Tests/EditMode`
- 2 66ead6a (+48dc7f8 гейт): `Settings/DesktopDrift.inputactions` (SteerLeft/SteerRight/Pause/Confirm/Back), `SteerInput` – чистый порт `resolveSteer` + 10 тестов, `SteerReader` (тач-половины, мышь, клавиши) без состояния
- 3 2b1a6c7: `RaceLoop` (FixedUpdate → `RaceRules.Step(steer, 1/60)` → события; пауза флагом; `Reset`/`StopRace`), `Core/Meta/CarCatalog` + `ContentJson` + 6 тестов, `RaceLaunch`, PlayMode-тесты (круг с `Finished`, паритет через `RaceLoop`), TimeManager 1/60 и 0.05, `unity-tests.sh` PlayMode
- 4 6e43972: режимы Time Attack / Sandbox / Zen → `RaceOptions` как в следах движка + 5 тестов
- 5 768740c (+05544ff): камера C1 (`CameraRig`, `CameraPreset` → `Content/Camera/Default.asset`), `capture.sh CAPTURE_DELAY`
- 6 171f586 (+df0de77 гейт): C2 – `Core/Camera/ChaseCameraMath` (курс по вектору скорости, лаг, лимит, вес по скорости) + 8 тестов; `CameraRig` C2 с интерполяцией; хуки `DD_CAMERA_MODE`, `DD_STEER=trace`; стоп-кадр на паузе; плоские props
- 7 4865f61: оверлей тюнинга (dev, `DD_TUNING`): слайдеры `CameraParams`, C1/C2, режимы, Restart, Save/Export JSON (`persistentDataPath/camera-tuning.json`), fps; `UI/PanelSettings.asset` 1080×1920
- 8 15ddc45: C3 – FOV от скорости, смещение внутрь дуги (с лагом), мобильная дистанция, интро на отсчёте, облёт после финиша, тряска на конусе; `CamPhase`; 8 тестов
- 9 2222858: следы – две меш-полосы (пул 750 сегментов на колесо, 1 материал, свой unlit-шейдер), условие/альфа/лимит дословно 2D, 15 тестов
- 10 8065b0d (+29f8ce0 гейт): `Tires`, `ColaCaps` (шейдер радиальной заливки, pop), `Cones` (вид = Core-позиция, сбитый лежит), `DriftZones` (кольцо под машиной: дрифт/near-miss/конус), 4 теста
- 11 ec9f0e9 (+793a12a): HUD-заглушка UI Toolkit со строками 2D дословно, миникарта `Painter2D`, пауза/подтверждение выхода/результат/стенд режимов, `HudFormat` + 16 тестов, сценный тест HUD
- 12: APK владельцу `artifacts/feel/DesktopDrift-spec02-793a12a.apk` (35 МБ (37 156 078 Б), 72 с), чек-лист `artifacts/feel-checklist.md`, заготовка `artifacts/feel-round-1.md`
- Архитектор: вопросы `docs/handoff/wave-3-questions.md`, находки гейта SPEC-02 1–15, тулинг-доки
- Раунд правок 1 (29.10) 02a661e + 01ceb77 + d479f5b (в `main` через PR #4; дубли ee7e0bd/8bcb186 в ветке волны, слиты в 6a8e138): HUD на позициях 2D `sandbox.css` (класс `narrow` = портрет, PlayMode-тест), кольцо дрифта удалено; конусы – бэклог (вопрос 7); APK round1 в `artifacts/feel/`

## Открыто
- PR #5 (`feat/spec-02-gameplay-loop`: только документы и D-24, код = `main`) – мержит владелец; Волна 4 (SPEC-03) стартует после мержа одной веткой по полному циклу
- Бэклог из фила: конусы «тяжёлые» – визуальный кувырок сбитого конуса в Game (вопрос 7 `wave-3-questions.md`), физика не меняется (D-14); вид HUD по `sandbox.css` на телефоне владелец увидит в следующей сборке (headless – только тест раскладки); fps на Poco X6 не назван
- Headless не проверены: UI-клики/тач по кнопкам (риск: `EventSystem`), вид HUD, глифы, экран результата, стенд режимов, Zen-скрытие панелей, 0.3 мс на следы (SPEC-02 находка 13)
- `capture.sh` без хука на кадр отсчёта (интро камеры проверено подручным захватом)
- Цвет следов из каталога – SPEC-04/07; одноразовость крышек между сессиями – SPEC-07 (сейчас `SaveState` в памяти); настоящие трассы/модели/UI/звук – SPEC-03/04/06/08
- Вопросы владельцу: `docs/handoff/wave-3-questions.md` (3 вопроса, 3 наблюдения)

## Файлы
- `unity/Assets/Game/**` – 20 файлов: `WorldMapping`, `CarView`, `Track/TrackView`, `Input/{ISteerSource,SteerInput,SteerReader,TraceSteerSource}`, `Race/{RaceLoop,RaceLaunch,Tires,ColaCaps,Cones,DriftZones}`, `Camera/{CameraRig,CameraPreset}`, `Debug/{TuningOverlay,TuningFile}`, `Fx/SkidMarks`, `Hud/{HudController,HudFormat}`
- `unity/Assets/Core/Camera/{CameraParams,CarKinematics,CamState,CamPhase,ChaseCameraMath}.cs`, `Core/Meta/{CarCatalog,ContentJson}.cs` – чистая логика с тестами (dotnet + EditMode)
- `unity/Assets/Editor/Scene/GameSceneBuilder.cs` – генератор сцены (единственный источник `Game.unity`); `Editor/Setup/ProjectSetup.cs` – TimeManager, define `DD_TUNING`
- `unity/Assets/Game.Tests/{EditMode,PlayMode}/` – 2 тестовые сборки; `UI/{Hud.uxml,Hud.uss,PanelSettings.asset}`; `Art/Shaders/{SkidMark,CapRadialFill,ZoneRing}.shader`; `Content/Camera/Default.asset`
- `scripts/unity-tests.sh` (`UNITY_TESTS_MODE`), `scripts/capture.sh` (`CAPTURE_DELAY`); `docs/tooling/README.md` – хуки

## Гейты
- test.sh: 290/290 за 2 с · unity-tests.sh EditMode: 355/355 за 9 с · PlayMode: 4/4 за 34 с · compile.sh: 0 ошибок/0 предупреждений · build.sh android: 35 МБ, 72 с (первая сборка 793a12a) / 41 с (round1)
- паритет через `RaceLoop`: побитово с `RaceRules`, ≤ 1e-6 к следу движка (2530 кадров); сценный круг: `Finished` на шаге 1328
- кадры `capture.sh`: C1 (20261028-204216), C2 (20261028-205325, борт виден в повороте), C3 (20261028-210452), следы (20261028-211123), дрифт-кольцо (20261028-211905) – `artifacts/capture/`
- законы: `Core/Physics`, `Core/Race` без изменений (D-14), `Time.timeScale` не используется, `RaceLoop` – только диспетчер, строки HUD дословно 2D (grep)

- 29.10, гейт архитектора на e5d3b1b из свежего чекаута: dotnet 290 · EditMode 356 · PlayMode 3 · compile 0/0 · APK 35 МБ/24 с · кадры C2/следов сессии просмотрены · статика по SPEC-02 (находка 16) – принято; открыт только фил владельца

- 29.10, закрытие волны: гейт на коде `main` (ветка после слияния 6a8e138, дерево кода = `main` c827a00): dotnet 290/290 · EditMode 355/355 · PlayMode 4/4 · compile 0/0 · APK 35 МБ/22 с (дерево чистое) – SPEC-02 ⬛

## Бюджет
- агентов: 13 (шаги 1–12 + раунд правок) · ходов агентов: ≈169 из ~670 по таблице · сверх бюджета: никто · гейты и точечные правки (6 коммитов гейта + документы) – архитектор в родительской сессии
