# Handoff – Волна 3: SPEC-02 игровой цикл, режимы, камера C1–C3, ввод, следы, HUD-заглушка (28.10.2026)

Статус: 🟦 на гейте владельца – APK на Poco X6, чек-лист `artifacts/feel-checklist.md`; после ответа – раунд правок (fix-round), решения D-21/D-22, PR.

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
- Архитектор: вопросы `docs/handoff/wave-3-questions.md`, находки гейта SPEC-02 1–13, тулинг-доки

## Открыто
- Гейт владельца (телефон): камера C2 или C1, ориентация (D-19), тач, fps ≥ 60, замечания → `artifacts/feel-round-1.md`; затем fix-round одним агентом, пресет JSON → `Content/Camera/Default.asset`, D-21/D-22 в `01-decisions.md`, O-01 (SPEC-02 находки 14–15)
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
- test.sh: 290/290 за 2 с · unity-tests.sh EditMode: 356/356 за 7 с · PlayMode: 3/3 за 32 с · compile.sh: 0 ошибок/0 предупреждений · build.sh android: 35 МБ (37 156 078 Б), 72 с
- паритет через `RaceLoop`: побитово с `RaceRules`, ≤ 1e-6 к следу движка (2530 кадров); сценный круг: `Finished` на шаге 1328
- кадры `capture.sh`: C1 (20261028-204216), C2 (20261028-205325, борт виден в повороте), C3 (20261028-210452), следы (20261028-211123), дрифт-кольцо (20261028-211905) – `artifacts/capture/`
- законы: `Core/Physics`, `Core/Race` без изменений (D-14), `Time.timeScale` не используется, `RaceLoop` – только диспетчер, строки HUD дословно 2D (grep)

## Бюджет
- агентов: 12 (шаги 1–12) · ходов агентов: ≈157 из ~670 по таблице · сверх бюджета: никто · гейты и точечные правки (5 коммитов гейта) – архитектор в родительской сессии
