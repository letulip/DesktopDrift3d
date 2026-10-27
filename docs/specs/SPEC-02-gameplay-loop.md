# SPEC-02 – Игровой цикл, режимы, камера (C1–C3), ввод, следы, HUD-заглушка на серой трассе

> Первый этап, который владелец может ПОИГРАТЬ. Гейт – не тесты, а заезд на телефоне: управление
> как в 2D, камера по решению D-08. Всё, что здесь строится, живёт на серой параметрической трассе
> (овал из `track-oval.js`) и переезжает на настоящие трассы без правок в SPEC-03.

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

D-08, D-14, O-01, O-04, O-08; SPEC-03 (заменяет овал), SPEC-06 (заменяет HUD), SPEC-07 (`ICapStore`, рекорды).

## Находки гейта

(пусто)
