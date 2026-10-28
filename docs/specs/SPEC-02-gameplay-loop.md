# SPEC-02 – Игровой цикл, режимы, камера (C1–C3), ввод, следы, HUD-заглушка на серой трассе

> Первый этап, который владелец может ПОИГРАТЬ. Гейт – не тесты, а заезд на телефоне: управление
> как в 2D, камера по решению D-08. Всё, что здесь строится, живёт на серой параметрической трассе
> (овал из `track-oval.js`) и переезжает на настоящие трассы без правок в SPEC-03.

## Поправки после Волны 2 (28.10.2026, архитектор)

SPEC-01 перенёс в Core больше, чем эта спека предполагала при написании, поэтому часть выходов уже есть:
- `Core/Track/OvalTrack.cs` существует (SPEC-01 шаг 8a) – шаг 1 его НЕ пишет, а только строит сцену над ним.
- `Core/Race/{RaceRules,RaceState,RaceOptions,RaceEvent,RaceResult}.cs` существуют и доказаны реплеями пяти следов
  настоящего движка (`tools/engine-trace-*.json`) – шаг 3 (`RaceLoop`) становится тонким: `FixedUpdate` → `RaceRules.Step`
  → диспетчер `RaceEvent`; правила НЕ переписывать. Контракты хоста – `03-architecture.md` §12 (dt = 1/60, пауза без
  `Step`, `hourOfDay`, `NowMs`, `random`, машина по `carIndex`, крышки по `capId`).
- Сейв есть (`Core/Save/SaveState.cs`), поэтому «заглушка `ICapStore` в памяти» из §3 не нужна: `RaceLoop` берёт
  `SaveState` из `SaveSchema.Load` (файл – SPEC-07; до него – загрузка пустого сейва, `OnChanged` пишется в лог).
- Новый пункт в шаге 3 (ревью Волны 2, И3): `Core/Meta/CarCatalog.cs` – `CarDefinition` (id, name, drive, len, wid,
  vw, vh, flip, price) из `Content/_json/cars.json` (+ ratings/feel из `car-registry.json`), чтобы `RaceLoop`
  передавал в `RaceRules` машину по `garage.carIndex`, а не руками.
- `SteerInput` – порт `resolveSteer` из `js/input.js` (10 строк): результат только −1/0/1; геймпад в 1.1 (D-16).
- Ориентация: D-19 – PoC крутится в Portrait и обоих Landscape, камера и HUD проектируются «портрет первым»;
  выбор на гейте этого этапа.
- Бюджет шага 3 снижается до ⟨opus · high · ≤50⟩, шага 1 – до ≤50.

## Цель

Сцена `Game.unity`: машина-плейсхолдер едет по серому овалу по правилам `RaceRules`, с вводом
(клавиатура, тач-половины), чейз-камерой C2 с настраиваемыми параметрами,
следами, шинами и крышками, зонами дрифта, минимальным HUD, паузой, выходом и результатом. Три
режима: Time Attack, Sandbox, Zen. «Race again» без перезагрузки сцены.

## Вход

SPEC-00 (контур с Unity), SPEC-01 (`RaceRules`, `Collision`, `TrackGeometry`). Источники для чтения:
`js/game-engine.js` (структура `frame()`), `js/input.js` (10 строк), `js/render.js` строки камеры
(`camOffY`, мобильный `0.65`), `js/haptics.js`, `css/sandbox.css` только список элементов HUD.

## Шаги и диспетчеризация

| № | Шаг | Диспетчеризация | Выход |
|---|---|---|---|
| 1 | Серый овал + `WorldMapping` + вид машины | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | `Core/Track/OvalTrack.cs`, `Game/WorldMapping.cs`, `Game/CarView.cs`, сцена |
| 2 | Ввод | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `Settings/DesktopDrift.inputactions`, `Game/Input/SteerInput.cs` (порт `resolveSteer`) |
| 3 | Оркестратор заезда | ⟨Builder · general-purpose · opus · high · ≤80⟩ | `Game/Race/RaceLoop.cs` (FixedUpdate → `RaceRules.Step` → события), `Reset()` |
| 4 | Режимы | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `RaceOptions {laps, zen, initItems}`; TA/Sandbox/Zen |
| 5 | Камера C1 (базовая, без поворота) | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `Game/Camera/CameraRig.cs`, `CameraPreset` (ScriptableObject) |
| 6 | Камера C2 (чейз по скорости) | ⟨Builder · general-purpose · opus · xhigh · ≤80⟩ | `Core/Camera/ChaseCameraMath.cs` + тесты, `CameraRig` режим C2 |
| 7 | Оверлей тюнинга | ⟨Builder · general-purpose · sonnet · medium · ≤50⟩ | `Game/Debug/TuningOverlay.cs`, сохранение пресета в JSON |
| 8 | Камера C3 (полировка) | ⟨Builder · general-purpose · opus · high · ≤60⟩ | FOV от скорости, смещение внутрь дуги, мобильная дистанция, переходы |
| 9 | Следы | ⟨Builder · general-purpose · opus · high · ≤70⟩ | `Game/Fx/SkidMarks.cs` (меш-полосы, пул, лимит) |
| 10 | Сборщики, зоны, near-miss | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | `Game/Race/{Tires,ColaCaps,DriftZones}.cs`, шейдер радиальной заливки крышки |
| 11 | HUD-заглушка и оверлеи | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | `UI/Hud.uxml`, пауза, подтверждение выхода, результат (минимальные) |
| 12 | Сборка для владельца и раунд правок | ⟨Gate-runner · general-purpose · sonnet · medium · ≤30⟩ + владелец | APK, чек-лист фила, `artifacts/feel-round-1.md` |

### 1 · Серый овал и отображение

`OvalTrack` – порт формулы `centerAt(a)`/`tangentAt` (300 сэмплов, `TRACK_HALF 100`, конусы `placeCones`,
props из `track-oval.js` как капсулы-плейсхолдеры). `WorldMapping`: `ToWorld`, `YawDeg`, `UnitsToMeters`
(формулы из `03-architecture.md` §4; тест: `forward` после `YawDeg(a)` равен `(cos a, 0, -sin a)`).
`CarView`: кубоид `len × vw/vh` в масштабе, интерполяция между двумя последними состояниями Core
(`Time.fixedTime`/`Time.time`). Лента трассы – простейший меш из `outer/inner` (временный, SPEC-03 заменит).

### 2 · Ввод

Input System: `Steer` (ось −1..1: стрелки/A-D; геймпад – 1.1 по D-16), `Pause`, `Confirm`, `Back`.
Тач: порт `resolveSteer` из `input.js` – половины экрана, несколько пальцев, приоритет как в JS; на
Unity – через `Touchscreen.current.touches`, без UI-блокировки под HUD (HUD `pickingMode: Ignore`).
Результат шага – `steerTarget ∈ {−1, 0, 1}` как в JS (не аналоговый).

### 3 · Оркестратор

`RaceLoop.FixedUpdate`: `steer = SteerInput.Resolve()` → `RaceRules.Step(state, steer, dt=1/60)` → список
`RaceEvent` → диспетчер: `Flash(text,color)`, `Haptic(pattern)`, `Sfx(id)`, `GameplayStart/Stop`,
`HappyMoment`, `LapDone`, `Finished(result)`, `CapCollected`, `TireCollected`, `ConeHit`, `NearMiss`,
`DriftStart/End(banked)`. Порядок внутри шага – как `frame()` в JS (цитата порядка в брифе: resolveSteer →
stepCar → cone hit + knocked → walls/props → nearestCenter → scoring → finish/checkpoint). `Reset()`
пересоздаёт `RaceState`, конусы, шины, крышки (одноразовость крышек из сейва – SPEC-07; здесь заглушка
`ICapStore` в памяти) – «Race again» и «Restart» без перезагрузки сцены (в 2D это делал `location.reload()`).
`maximumDeltaTime = 0.05` как страховка.

### 4 · Режимы

`RaceOptions`: TA (`laps` из трассы, HUD полный), Sandbox (`laps=0` бесконечно, без предметов, овал),
Zen (`zen=true`: без левой/правой панели HUD, предметы включены). Выбор – параметром запуска сцены
(`RaceLaunch` статический контекст до появления экранов в SPEC-06).

### 5 · Камера C1

Высокая, курс фиксирован (мир не вращается), наклон 60°, следует за машиной со смещением ~10 % экрана
вниз (как `camOffY` в 2D), дистанция по пресету; мобильная дистанция ×(1/0.65). Пресет –
`CameraPreset` ScriptableObject (все числа). C1 остаётся выбираемой и после C2 – запасной режим и
референс читаемости.

### 6 · Камера C2 – чейз по вектору скорости (решение D-08)

Математика в Core (`ChaseCameraMath.Step(in CamState s, in CarKinematics k, in CameraParams p, double dt) → CamState`),
тестируемая числом:
- целевой курс `yawTarget` = направление ВЕКТОРА СКОРОСТИ (`atan2(vy, vx)`), при `speed < p.minSpeedForVelocityYaw`
  плавно переходит к оси машины (`angle`) весом `w = clamp(speed/minSpeed)`;
- курс камеры `yaw` догоняет `yawTarget` экспоненциально с постоянной `p.yawLag` и ограничением
  угловой скорости `p.maxYawRate` (защита от рывков при перекладке);
- позиция = `car − dir(yaw) · p.distance + up · p.height`, точка взгляда = `car + dir(yawTarget) · p.lookAhead · speedNorm`;
- в дрифте (`|vS| > DRIFT_MIN_SLIP`) машина разворачивается ВНУТРИ кадра: ось машины отклонена от курса
  камеры на угол скольжения – игрок видит борт и куда едет (требование владельца);
- тесты: непрерывность (нет скачка > `maxYawRate·dt`), сходимость к прямой езде, поведение при остановке,
  симметрия лево/право.
Game-обёртка кладёт результат на `Camera.main` с интерполяцией. Дефолтные параметры – стартовые
значения (distance ≈ 6–8 длин машины, height ≈ 0.6·distance, pitch ≈ 45–55°, yawLag ≈ 0.35 с,
maxYawRate ≈ 180°/с, lookAhead ≈ 1.5 длины на максимальной скорости), дальше – оверлей тюнинга и владелец.

### 7 · Оверлей тюнинга

Только в dev-сборке (`DEVELOPMENT_BUILD` или define `DD_TUNING`): слайдеры для всех полей `CameraParams`
и выбор C1/C2; «Save» пишет JSON в `persistentDataPath/camera-tuning.json`, загружается при старте;
«Export» копирует JSON в буфер – владелец присылает его агенту, агент переносит в `CameraPreset`
(шаг 12). Это замена глазу агента: тюнинг делает владелец, не Claude.

### 8 · Камера C3

FOV от скорости (`fovMin..fovMax` по `speed/maxSpeed`), боковое смещение внутрь дуги по знаку `vS`
(`p.driftOffset`), мобильная дистанция (`p.mobileDistanceScale`, аналог 0.65), плавный въезд камеры на
старте (отсчёт) и орбита на результате, тряска на конусе (малая, отключаемая). Всё – параметры пресета.

### 9 · Следы

Меш-полосы на каждом заднем колесе: при `drifting` добавляется сегмент (квад по следу колеса на высоте
`+1 мм`), альфа от `|vS|` (как уровни в 2D), пул сегментов с жёстким лимитом (`p.maxSegments`, дефолт 1500 –
число из 2D-бэтчинга), один материал, один `Mesh` на колесо с `MarkDynamic`, обновление только при
добавлении. Бюджет: ≤ 2 draw-call на следы, ≤ 0.3 мс на мобильном. Цвет – из каталога следов (SPEC-04/07).

### 10 · Сборщики, зоны, near-miss

Шины: триггер радиуса `r + TIRE_CR` (из Core), респаун каждый заезд, счётчик в HUD. Крышки: диск с
шейдером радиальной заливки (`_Sweep` 0..1 из `RaceState.caps[i].sweep`), «pop» при сборе; одноразовость –
через `ICapStore`. Зоны дрифта и near-miss: события из `RaceRules`, здесь только визуальный ответ
(вспышка, цвет). Конусы: сбитый конус едет по `stepKnockedCone` (вид = Core-позиция).

### 11 · HUD-заглушка

UI Toolkit документ `Hud.uxml`: `timePanel`, `lapCounter` («1/3»), `score`, `combo` (множитель), `flash`,
`count` (отсчёт), `hint`, `mini` (миникарта: полилиния центра на `RenderTexture` 256², точка машины,
север вверху как в 2D), `menuBtn`, `pauseBtn`. Пауза (стоп `FixedUpdate` через `Time.timeScale=0` НЕ
использовать – камера и UI должны жить; вместо этого флаг `paused` в `RaceLoop`), подтверждение выхода,
результат (score, bestLap, lapScores, pps, звёзды, «NEW RECORD», кнопки Race again / Menu). Вординг –
строки 2D-игры дословно. Красота – SPEC-06.

### 12 · Сборка и раунд фила

`scripts/build.sh android` (debug APK) → владелец ставит на телефон. Чек-лист в `artifacts/feel-checklist.md`:
тач-половины (один/два пальца), перекладки, видимость бортов и траектории в дрифте, читаемость конусов
и предметов впереди, near-miss ощущается, следы, HUD читаем, 60 fps (счётчик в оверлее), «Race again».
Владелец возвращает JSON пресета камеры и список замечаний → один агент-раунд правок (fix-round).

## Артефакты

Сцена `Game.unity`, `Core/Camera/*`, `Core/Track/OvalTrack.cs`, `Game/**` по шагам, `UI/Hud.uxml`,
`Content/Camera/Default.asset`, `artifacts/feel-round-1.md`, APK.

## Вне объёма

Настоящие трассы и предметы (SPEC-03/05), модели машин и косметика (SPEC-04), сейв и экономика (SPEC-07),
звук (SPEC-08), финальный UI (SPEC-06).

## Acceptance-критерии

- EditMode: `ChaseCameraMath` тесты зелёные; `WorldMapping` тест зелёный; `RaceLoop` PlayMode-тест:
  скриптованный ввод проезжает 1 круг овала, событие `LapDone` пришло, `Finished` при `laps=1`.
- Паритет сохраняется: трасса SPEC-00 проходит через `RaceLoop` (состояние после N фиксированных
  шагов равно Core-трассе).
- На контрольном телефоне: ≥ 60 fps на овале, тач работает, нет рывков камеры при перекладке
  (владелец), следы ≤ лимита.
- Владелец подписал камеру C2 (или выбрал C1) и управление – запись в `01-decisions.md`.
- Владелец выбрал ориентацию (портрет – гипотеза D-19; PoC-сборка крутится в Portrait и оба Landscape) – запись
  в `01-decisions.md`, после неё `ProjectSetup` оставляет одну ориентацию, SPEC-06 §12 и SPEC-09 §1 правятся.

## Гейт архитектора

Прогон тестов; просмотр `capture.sh` кадров на овале (C1 и C2); проверка, что `RaceLoop` не содержит
правил (только диспетчер событий), что `Time.timeScale` не трогается, что HUD использует строки 2D;
чтение `feel-round-1.md` и JSON пресета владельца → решение по O-01 (перетюн – нет без отдельного решения).

## Риски

- Камера по скорости при остановке/реверсе может «крутиться» – вес `w` по скорости и `maxYawRate` обязательны, тест на остановку.
- Тач на Android через Input System и UI Toolkit конфликтует (UI перехватывает) – `pickingMode: Ignore` на всём HUD, кроме кнопок.
- Следы на мобильном – единственный потенциальный перф-риск этапа; лимит и измерение в шаге 12.
- Соблазн «улучшить» физику под камеру – запрещено D-14, вопрос O-01 только через владельца.

## Связи

D-08, D-14, D-19, O-01, O-04, O-08; SPEC-03 (заменяет овал), SPEC-06 (заменяет HUD), SPEC-07 (`ICapStore`, рекорды).

## Находки гейта

Волна 3, 28.10.2026, ветка `feat/spec-02-gameplay-loop` (гейт архитектора до фила владельца; пункты 14–15 – после него).

1. **TimeManager в Unity 6 хранит Fixed Timestep дробью** (`m_Count/m_Rate`), запись `floatValue` в `SerializedProperty` – тихий no-op; `ProjectSetup` пишет `m_Count = rate/60` (2352000/141120000) и `Maximum Allowed Timestep = 0.05`; `RaceLoop.Awake` дублирует (`Time.fixedDeltaTime`, `maximumDeltaTime`, `targetFrameRate = 60` – Android по умолчанию 30 fps).
2. **`dt` в `Step` – только `RaceLoop.FixedDt = 1.0/60` (double)**: `Time.fixedDeltaTime` – float 0.01666667 ≠ 1/60, паритет был бы не побитовым. Паритет через `RaceLoop`: побитово с прямым `RaceRules` и ≤ 1e-6 к следу движка на всех 2530 кадрах `engine-trace-oval`; сценный тест – `Finished` на шаге 1328 (в следе круг закрыт на 1327).
3. **Ввод**: композит `1DAxis` не выражает правило 2D «стрелка влево + D → 0» (решает больший модуль), поэтому `Steer` – два Button-действия `SteerLeft`/`SteerRight` (`IsPressed`), чистый порт `resolveSteer` без изменений; читатель без состояния (сброс указателей на паузе не нужен).
4. **Интерполяция на паузе/после финиша качалась** между двумя последними состояниями (`Time.fixedTime` идёт, `Push` нет): `CarView` и `CameraRig` держат последнее состояние, когда фиксированный шаг прошёл без `Push`.
5. **Props-плейсхолдеры**: капсула с `Hl = 0` давала пилюлю длиной 4R; теперь плоские «тарелки» со следом коллизии 2D (`scale.y = hl + r`, высота 0.25·r) – до моделей SPEC-03/05.
6. **Камера C2** (`Core/Camera`): «pitch» не параметр – наклон следует из `Height/Distance` и точки взгляда; сходимость при `YawLag 0.35` проверяется за 360 шагов; боковое смещение C3 идёт с тем же лагом (`CamState.Lateral`): прямое `vS` прыгало на 60 ед. за шаг и ломало границу непрерывности; облёт после финиша стартует из текущего `yaw`; интро проверено подручным захватом (масштаб ≈ 1.89 на 1-й с) – у `capture.sh` нет хука на отсчёт.
7. **UI Toolkit**: `picking-mode` – не свойство USS в 6000.3 (импортёр предупреждает), ставится из кода; рантайм-ввод UI идёт через project-wide actions – шаблонный `InputSystem_Actions.inputactions` с картой `UI` оставлен нарочно; клики/тач по кнопкам headless не проверяются – первая проверка на телефоне.
8. **Крышки**: `Cola.CapProgress` – однооборотный помощник правил, а рисунок 2D заливает диск за `CapLoops = 2` оборота (`render.js:521`) – формула вида в `ColaCaps`. `Collectible.Kind` крышки – `"cola"`, не `"cap"`.
9. Unity сам создал `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss` и `ProjectSettings/SceneTemplateSettings.json` – закоммичены; `Tire.mat` получил синхронизацию `_Color` от билдера сцены – закоммичена.
10. **Dev-хуки контура** (headless-редактор наследует окружение): `UNITY_TESTS_MODE=PlayMode`, `CAPTURE_DELAY=<с>`, `DD_STEER=trace` (только редактор, руль из `tools/engine-trace-oval.json`), `DD_CAMERA_MODE=C1|C2`; сцена `Game.unity` ГЕНЕРИРУЕТСЯ `Editor/Scene/GameSceneBuilder.Build` (идемпотентно), YAML руками не правится.
11. **Профиль сборки Android несёт define `DD_TUNING`** (`ProjectSetup`): dev-APK с оверлеем тюнинга и fps; релиз SPEC-09 define убирает.
12. **Следы**: 2 рендерера / 1 материал (≤ 2 draw-call); бюджет 0.3 мс не измерен до телефона. Миникарта – `Painter2D`, не `RenderTexture` (меньше машинерии).
13. **Что не проверено headless**: вид HUD (портрет/landscape, глифы ☰ ⏸ ▶ ★ 🛞 👑 ♾️), клики, экран результата, стенд режимов, скрытие панелей в Zen, тюнинг-панель, тач-половины на устройстве, fps – всё в чек-листе владельца `artifacts/feel-checklist.md`.
14. **Фил владельца 29.10 (Poco X6)**: «для первого прототипа это вообще топ». Раунд правок 1 (`docs/handoff/wave-3-feel-round-1.md`): кнопка меню стояла внизу под пальцем → HUD вернулся на позиции 2D `css/sandbox.css` (×3 панельных px, класс `narrow` = портрет); «пульсирующий кружочек» в дрифте (кольцо `DriftZones` шага 10, которого в 2D нет) удалён целиком; «конусы тяжёлые» – бэклог с вопросом (физика конуса – паритет D-14; первый шаг – визуальный кувырок в Game). JSON пресета не присылался, `Default.asset` без изменений; fps владелец не назвал (замечаний к плавности нет). Headless-проверка раскладки: PlayMode-тест переключает `narrow` и читает `resolvedStyle` (360/468, snapping ±1 px).
15. **Решения гейта**: D-21 – камера C2 принята (C1 в dev-оверлее), O-01 закрыт; D-22 – ориентация: портрет-гипотеза держится до настоящих трасс SPEC-03; D-23 – интерфейсы 2D переносятся как есть (SPEC-06 «Поправки после Волны 3»).
16. **Гейт архитектора на ветке (29.10.2026, e5d3b1b, свежий чекаут, до фила владельца).** test.sh 290/290 за 2 с;
    compile.sh 0/0 за 10 с; EditMode 356/356 за 8 с; PlayMode 3/3 за 32 с (круг овала с `Finished`, паритет через
    `RaceLoop` 2530 кадров ≤ 1e-6, сценный HUD); build.sh android 35 МБ за 24 с (дерево чистое); capture.sh C2 на 12-й
    секунде без `DD_STEER=trace` – машина едет прямо и сходит с овала (параметры захвата, не дефект); борт в повороте и
    следы подтверждены просмотром кадров сессии `20261028-205325` (C2) и `20261028-211123` (следы). Статика: `Core/{Physics,Race,Collision,Scoring,Rules,Save}`
    без изменений против `main`; инварианты Core 0 вхождений; `Time.timeScale` только в комментарии; `RaceRules.Step`
    вызывается в одном месте (`RaceLoop.FixedUpdate`, `FixedDt = 1.0/60` double, не на паузе и не после `Finished`,
    §12); `RaceLoop` без математики правил; 14 строк HUD найдены в 2D дословно; ориентация – автоповорот Portrait +
    оба Landscape (D-19); `SteerInput` – дословный `resolveSteer`. Вердикт архитектора: принято; остаётся гейт
    владельца на Poco X6 (`artifacts/feel-checklist.md`, 14 пунктов) → раунд правок → D-21/D-22 → мерж.

