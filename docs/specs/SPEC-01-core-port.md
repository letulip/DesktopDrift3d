# SPEC-01 – Перенос чистой логики 1:1 с тестами и трассами паритета

> Все формулы и таблицы 2D-игры становятся C#-кодом и JSON без единой перепечатанной цифры.
> Тесты 2D-игры (42 файла) – спецификация; расхождение с JS = дефект переноса, не «улучшение».

## Цель

`DesktopDrift.Core` содержит физику (SPEC-00), скоринг, коллизии, крышки, экономику, ачивки,
неон-математику, рейтинги машин, геометрию трасс, посев шин, реверс, схему сейва с миграциями,
кодек профиля и правила заезда (`RaceRules`), всё покрыто NUnit-тестами с ожиданиями из JS;
данные экспортированы в JSON.

## Вход

- SPEC-00 часть A (контур dotnet) закрыт.
- Источники (только чтение, диапазоны строк в брифах): `js/scoring.js`, `js/economy.js`,
  `js/cola.js`, `js/collision.js`, `js/track-util.js`, `js/tire-seed.js`, `js/reverse-track.js`
  (или где лежит `reverseTrack` – найти `grep -rn "reverseTrack" js/`), `js/achievements.js`,
  `js/car-stats.js`, `js/neon.js`, `js/shop-catalog.js`, `js/store.js`,
  `js/wallet-history.js`, `js/game-engine.js`, `js/config.js`, `js/cars-data.js`, `js/items.js`,
  `js/track-registry.js`, `js/palette.js`, `js/sound-params.js`, `js/collectibles.js`; тесты `tests/*.test.js`.

## Шаги и диспетчеризация

| № | Шаг | Диспетчеризация | Выход |
|---|---|---|---|
| 1 | Экспорт данных из JS | ⟨Importer · general-purpose · sonnet · high · ≤50⟩ | `tools/export-data.mjs`, `unity/Assets/Content/_json/*.json` |
| 2 | `scoring.js`, `economy.js`, `cola.js` | ⟨Builder · general-purpose · sonnet · medium · ≤50⟩ | `Core/Rules/{Scoring,Economy,Cola}.cs` + тесты |
| 3 | `collision.js` | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | `Core/Collision/Collision.cs` + тесты |
| 4 | `track-util.js`, `tire-seed.js`, реверс | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | `Core/Track/{SvgPath,Chaikin,TrackGeometry,TireSeed,ReverseTrack}.cs` + тесты |
| 5 | `achievements.js`, `car-stats.js`, `neon.js`, каталог магазина | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | `Core/Meta/{Achievements,CarStats,NeonMath,ShopCatalog}.cs` + тесты |
| 6 | `store.js` (схема, слияние) + `wallet-history.js` | ⟨Builder · general-purpose · sonnet · high · ≤50⟩ | `Core/Save/{SaveNode,SaveSchema,Migrations,WalletHistory}.cs` + тесты |
| 7 | Трассы паритета: стены, предметы, конусы, скоринг | ⟨Builder · general-purpose · sonnet · high · ≤50⟩ | `tools/parity-trace.mjs --scenario wall|props|cones|score`, тесты |
| 8 | `RaceRules`: правила заезда из `game-engine.js` | ⟨Builder · general-purpose · opus · high · ≤70⟩ | `Core/Rules/RaceRules.cs`, `RaceState.cs` + тесты |
| 9 | Ревью переноса | ⟨Reviewer · general-purpose · opus · high · ≤40⟩ | `artifacts/review-spec01.md` |

### 1 · Экспорт данных

`tools/export-data.mjs` импортирует модули 2D-игры (`import … from '<abs>/DesktopDrift/js/…'`;
модули с `document`/`fetch` на верхнем уровне – только те, где это безопасно: `cars-data.js`,
`items.js`, `track-registry.js`, `shop-catalog.js`, `achievements.js` (каталог), `palette.js`,
`sound-params.js`, `economy.js`, `collectibles.js`, `neon.js` константы). Трек-модули тянут SVG через
`fetch` – их НЕ импортировать; `laps`/`tires` взять из `track-registry.js` и из текста модулей
(`grep -n "laps:\|tires:" js/track-*.js`). Выход: `cars.json` (CARS с `_drive`, `price`, `ratings`,
`len`, `vw`, `vh`, `flip`, `details` можно опустить), `items.json`, `tracks.json`, `shop.json`,
`achievements.json` (id, name, icon, reward, hidden, тип лестницы – как в каталоге), `palette.json`,
`sfx.json`, `economy.json` (`DDK_PPS`, `CAP_TIRE_VALUE`, первоклир +20, трофей +5 – найти в
`game-engine.js`/`economy.js`), `neon.json` (LAYOUTS, ANIMS, NEON_ZONES). Детерминированная сортировка
ключей, `\n` в конце, коммитится. Каждый JSON несёт `"_source": {"file": …, "commit": …}`.

### 2 · Scoring, Economy, Cola

Константы с комментарием-источником: `DRIFT_MIN_SLIP 60`, `DRIFT_MIN_SPEED 90`, `QUALITY_SLIP_REF 160`,
`QUALITY_SPEED_REF 260`, `QUALITY_MAX 1.4`, `MULT_GAIN_PER_S 0.14`, `MULT_TRANSITION_BONUS 0.3`,
`MULT_NEARMISS_BONUS 0.28`, `MULT_MAX 8`, `SLIP_SIGN_THRESHOLD 50`, `COMBO_RATE 0.0015`; функции
`IsDrifting`, `DriftQuality`, `ComboMult`, `ComboGain`, `SlipSign`, `PointsPerSecond`, `IsNewPpsRecord`
(семантика «null или undefined = нет рекорда» → `double?`). Economy: `StarsForPps` (1 звезда/100 PPS,
cap 5), `FinishPayout = 2 + 2*stars`, `DDK_PPS 600`, `IsDDK`, флаг `CAR_GATING_ENABLED`. Cola:
`AngDelta`, `CapProgress`, `StepSweep`. Тесты: `scoring.test.js`, `economy.test.js`, `cola.test.js` → 1:1.

### 3 · Collision

`FinishDot(car, c0, cos, sin)`, `CrossedFinish(prevDot, dot)`, `NearMiss(car, cones, props, TABLE, CONE_R, CR, NM_BAND)`,
`AdvanceCheckpoint(nextCp, carX, carY, checkpoints, CP_R)`, `ResolveWall(car, TABLE, CR, hx, hy, nose, bodyPts)`,
`ResolveProps(car, props, CR, bodyPts)`, `StepKnockedCone(c, props, CONE_R, dt, fAdj)`. Типы:
`TableRect {w, h, shape}`, `Prop {x, y, ang, hl, r, kind, _cos, _sin}` (кэш как в `prepProp`), `Cone {x, y, vx, vy, knocked…}` –
поля брать из JS (`grep -n "cones.push\|c\.\w*" js/track-util.js js/collision.js`). `tests/collision.test.js` 1:1.

### 4 · Геометрия трасс

`SvgPath.Parse(d)` (поддерживаемые команды – ровно те, что в `parseSvgPath`), `Chaikin.Smooth(points, iterations=4)`,
`TrackGeometry.ToGame(scale=0.25, origin из viewBox)`, `NearestCenter`, `PlaceCones(outer, inner)`, `PrepProp`,
`TireSeed.Seed(center, n)` (ключ стабильного индекса `t0…`), `ReverseTrack(track)` + `InstanceId(trackId, dir)` (`:rev`).
Тесты `track-util.test.js`, `tire-seed.test.js`, `reverse-track.test.js`, `track-surface.test.js` 1:1.
Эталон: `tools/export-tracks.mjs` – Node-скрипт, который исполняет `makeTrack` на SVG (нужен `fetch` файла:
подменить `globalThis.fetch` чтением с диска и `DOMParser` через `jsdom` или `@xmldom/xmldom` – devDependency
в `tools/package.json`) и пишет `tools/tracks-ref/<id>.json` (center/outer/inner/cones/props/checkpoints/TABLE/start).
Эти эталоны использует SPEC-03 для паритета импортёра.

### 5 · Meta

`Achievements.Evaluate(ctx, unlocked)` → `{unlocked:[…], progress:[…]}` над `AchievementContext` (поля = `ctx`
в `game-engine.js`/`achievements.js`; перечислить в брифе из `grep -n "ctx\.\w*" js/achievements.js`);
каталог из JSON, лестницы «масштабируются с числом трасс» – как в JS. `CarStats`: `SpeedRating`, `HandlingRating`,
`AccRating`, `DriveForRatings` с максимумами `SPEED_MAX_KMH 15`, `STEER_MAX 5`, `LOWTURN_MAX 0.5`, `THRUST_MAX 900`.
`NeonMath`: `LAYOUTS`, `ANIMS`, `LayoutColours`, `ZoneColors(neon, t)`, `DefaultNeon`. `ShopCatalog`: проверка
уникальности id и категорий (`finish-*`, `trail-*`, `glass-*`, `outline-*`, `emo-*`, `neon-layout-*`, `neon-anim-*`).
Тесты `achievements.test.js`, `car-stats.test.js`, `neon.test.js`, `shop-catalog.test.js`, `car-order.test.js`,
`car-ownership.test.js` 1:1.

### 6 · Сейв (решение D-17: без импорта из веба)

`SaveNode` – минимальное дерево (объект/список/число/строка/bool/null) без зависимостей; `SaveSchema.Defaults()` =
форма `store.defaults()` 2D v4, объявленная версией 1 схемы 3D-издания (`SaveVersion = 1`); `Load(saved)` = глубокое
слияние поверх defaults (массивы заменяются целиком), сброс только при неразборчивых данных; `Migrations` – пустой
append-only реестр с тестом «версия больше известной → не трогать, вернуть defaults и флаг backup». Миграции 2D
v2–v4 и `profile-io.js` НЕ переносятся. API `Records`, `Garage`, `Wallet/Ledger`, `Owned/OwnedCars`, `Stats` (`caps`,
`tires`, `tiresSwept`, `trophies`, `cleared`, `runs`, `driftSecs`) как у `store.js`. `WalletHistory` как в
`wallet-history.js`. Тесты: `store.test.js`, `store-load.test.js`, `store-replace.test.js` (части про слияние и
defaults), `wallet-history.test.js`, `ach-sync.test.js` 1:1; `store-neon-migrate.test.js` и `profile-io.test.js` –
в списке исключений отчёта.

### 7 · Трассы паритета

Сценарии в `tools/parity-trace.mjs --scenario`: `wall` (старт у края `TABLE`, курс на стену, `resolveWall` каждый кадр),
`props` (3–4 капсулы на пути, `resolveProps`), `cones` (конус на пути, `hitConeAt`/`stepKnockedCone` – найти в
`game-engine.js`/`collision.js`), `score` (800 кадров базового ввода + накопление `comboGain`/`multBuild` по правилам
`frame()`: сравнивать `score`, `multBuild`, `transitions` на каждом кадре). Допуск 1e-6; для `score` – 1e-6 относительный.

### 8 · RaceRules (единственный шаг с суждением)

`game-engine.js` читается целиком (660 строк, оправдано). Выделить в Core всё, что не трогает DOM/canvas:
обратный отсчёт (3 → GO, `cdBeep`), круги/чекпоинты (`nextCp`, `K`, `CP_R`, `TOTAL_LAPS`, `lapStarted`, `lapTime`,
`bestLap`, `lapScores`, `lapScoreStart`), банк/сгорание комбо (банк на чистом выходе из дрифта, сгорание на
краше/вылете, конус −100), переходы и near-miss бонусы, стоп дрифт-зоны (`ZONE_ADV`, `driftZoneRef/Timer/driftZoned`),
крышки (`updateCaps`: кольцо `[CAP_INNER_R, CAP_OUTER_R]`, `CAP_LOOPS 2`, `CAP_DECAY`, `CAP_TIRE_VALUE 15`), шины
(`dist < r + TIRE_CR`), финиш (`totalScore`, `totalTime`, `pps`, звёзды, выплата, первоклир, трофей 1-PPS, сборка
`ctx` ачивок), `S` → `RaceState`. Побочные эффекты (хаптика, HUD, звук, платформа) – СОБЫТИЯ `RaceEvent` в списке
результата шага, Game их исполняет. Тесты: на каждое правило сценарный тест с числами, выведенными из JS-кода
(в брифе – цитаты соответствующих строк). Это самый дорогой шаг переноса; Opus.

### 9 · Ревью

Свежая сессия/агент: построчное сравнение `physics`, `collision`, `scoring`, `RaceRules` с JS; проверка, что
ни одна константа не перепечатана руками там, где есть JSON; отчёт с таблицей «JS → C#, статус, замечание».

## Артефакты

`Core/**` модули, `Core.Tests/**`, `tools/export-data.mjs`, `tools/export-tracks.mjs`, `tools/tracks-ref/*.json`,
`Content/_json/*.json`, `artifacts/review-spec01.md`.

## Вне объёма

Рендер, камера, ввод, Unity-компоненты, импортёры в ScriptableObject (SPEC-03/04/07), i18n.

## Acceptance-критерии

- `scripts/test.sh` зелёный; число тестов ≥ числу `test(` в JS-файлах-источниках (посчитать `grep -c "^test(\|^  test(" tests/*.test.js`).
- Все сценарии паритета ≤ 1e-6; `score` ≤ 1e-6 относительно.
- `grep -rn "[0-9]\{3,\}" unity/Assets/Core --include=*.cs | grep -v "// .*\.js"` показывает только числа с
  комментарием-источником.
- Ревью без открытых «расхождение».

## Гейт архитектора

Прогоняю `test.sh` и `trace.sh` по всем сценариям; выборочно сверяю 5 функций с JS глазами; проверяю,
что `RaceRules` не содержит ссылок на Unity и что события покрывают все побочные эффекты `frame()`
(список из брифа шага 8 против `grep -n "flash(\|haptics\.\|sfx\.\|gameplayStart\|happyMoment" js/game-engine.js`).

## Риски

- `game-engine.js` переплетает правила и DOM: выделение потребует итерации; поэтому Opus и тесты на правила.
- JS-тесты местами проверяют строки/DOM (`ach-toast`, `sw-*`, `route`) – их НЕ переносить, список исключений в отчёте.
- Соблазн перенести миграции 2D «на всякий случай» – запрещено D-17: мёртвый код без потребителя.

## Связи

SPEC-00 (контур), SPEC-02 (потребитель `RaceRules`), SPEC-03 (`tracks-ref`), SPEC-07 (сейв), решения D-14, O-10.

## Находки гейта

Волна 2, по ходу (28.10.2026):

1. **`CoreMath.Hypot` переписан по алгоритму V8** (`MathHypot` в `src/builtins/math.tq`: NaN/∞-правила,
   масштабирование по максимуму, сумма квадратов с компенсацией Кахана, `sqrt·max`). Причина: наивный
   `sqrt(x²+y²)` расходится с `Math.hypot` на ulp, и на вырожденном тесте `sampleCheckpointsByCorner`
   (равные кривизны) argmax выбирал другие точки (`[0,7,16,22,35]` вместо JS `[0,12,22,35]`); с V8-алгоритмом
   ожидание JS выполняется, трасса паритета физики 2.7e-12 → 1.8e-12 (остаток – `pow`/`sin`/`cos` рантаймов).
   Закон остаётся допуском 1e-6: IL2CPP/clang может свернуть `n·n − c` в FMA, бит-в-бит не обещается.
2. `ParseSvgPath` строже JS: неизвестная команда (в том числе строчные `m l h v`) → `FormatException`;
   JS молча портил точки. Для Figma-экспорта (только `M L H V Z`) поведение совпадает.
3. Шаг 4 разделён: 4a – геометрия (`TrackUtil`, `TireSeed`, `TrackData`, 37 тестов), 4b – эталоны трасс
   через настоящий DOM (`linkedom`) + `TrackBuilder.FromSvg` в Core (System.Xml) с тестом паритета по 6
   трассам (перенесено сюда из SPEC-03 §1–2: парсер трасс живёт в Core, SPEC-03 только оборачивает его).
4. Экспорт данных (шаг 1): `cars.json` хранит `drive` (= `_drive`, с дефолтами `driftDrag`/`rollFriction`) и
   `driveRaw`; `price`/`ratings` живут в `car-registry.json` (стартовые bismark/panda там отсутствуют);
   `engine.json` даёт 9 констант замыкания `game-engine.js` (включая `MULT_RESET_GRACE 1.2`, `ZONE_STALL 3`).
5. Шаг 2: JS `Math.round` ≠ C# `Math.Round` (банковское) – введён `CoreMath.JsRound = floor(x+0.5)`;
   `grep Math.Round` в Core запрещён гейтом.

