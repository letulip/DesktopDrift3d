# SPEC-03 – Пайплайн трасс: SVG → TrackAsset → префаб; 6 трасс серыми; реверс; валидатор

> Трассы 2D-игры авторятся в Figma как SVG (`track_path` + proxy-линии `ITEM_*`, `ITEM_COLA_CAP`),
> это остаётся источником истины. Импортёр повторяет `makeTrack` числом (паритет с эталонами
> `tools/tracks-ref/` из SPEC-01) и строит 3D-представление: лента, стол, предметы-плейсхолдеры.

## Цель

Все 6 трасс × 2 направления + овал играются в сцене `Game.unity` через `TrackAsset` и префабы,
с чекпоинтами, конусами, крышками, шинами и цветовыми темами; валидатор зазоров запускается тестом.

## Вход

SPEC-01 (`TrackGeometry`, `tracks-ref`), SPEC-02 (сцена, `RaceLoop`). Источники: `DesktopDrift/tracks/*.svg`
(6 файлов + `track_themes.json`, `track_items_placement.md` – правила), `js/track-factory.js` (весь, 151 строка),
`js/track-registry.js`, `js/render.js` строки отрисовки стола/ленты/флага/чекпоинта, `tools/check-item-clearance.js`.

## Шаги и диспетчеризация

| № | Шаг | Диспетчеризация | Выход |
|---|---|---|---|
| 1 | `TrackImporter` (SVG → `TrackAsset`) | ⟨Importer · general-purpose · sonnet · high · ≤60⟩ | `Editor/Importers/TrackImporter.cs`, `Content/Tracks/<id>.asset` |
| 2 | Паритет импортёра с эталонами | ⟨Importer · general-purpose · sonnet · high · ≤50⟩ | `Core.Tests/TrackImportParityTests.cs` (EditMode) |
| 3 | Меши: лента, стол, пол, стартовый флаг | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | `Game/Track/TrackMeshBuilder.cs`, материалы-заглушки |
| 4 | Префаб трассы | ⟨Builder · general-purpose · sonnet · medium · ≤50⟩ | `Editor/Importers/TrackPrefabBuilder.cs`, `Art/Prefabs/Tracks/<id>.prefab` |
| 5 | Реверс на лету | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `TrackRuntime.Load(id, dir)`, `instanceId` `:rev` |
| 6 | Валидатор зазоров как тест | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `Editor/Tests/ClearanceTests.cs` |
| 7 | Превью трасс | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `Editor/Importers/TrackThumbnail.cs` → `Content/Tracks/<id>-thumb.png` (+ `-rev`) |
| 8 | Все трассы в сцене, проверка проезда | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | PlayMode-тест «центр-линия проходит чекпоинты» на 12 инстансах |
| 9 | Темы цветов | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `TrackTheme` в ассете, применение к материалам |

### 1 · Импортёр

`[MenuItem]` + вызываемый из CLI `TrackImporter.ImportAll()`: читает `DesktopDrift/tracks/<id>.svg` (путь к
2D-репо – в `ProjectSettings`/`tools/config.json`, НЕ копировать SVG в проект: источник один), парсит XML
(`System.Xml`), берёт `viewBox` → origin, путь `#track_path` (или первый `path` – как в `makeTrack`, цитата в
брифе), `scale 0.25`, `Chaikin×4`, `outer/inner` по `TRACK_HALF`, конусы `PlaceCones`, чекпоинты (правило `K`
и `CP_R` из фабрики), proxy-линии `ITEM_*` → props (midpoint, угол, резолв id со срезанием `_N`),
`ITEM_COLA_CAP` → collectibles, посев шин `TireSeed` по `tires` из `track-registry`, `TABLE` =
`(maxX+250)*2 × (maxY+250)*2` rect, `startPos/startAngle`, `laps`, `theme`. Пишет `TrackAsset`.

### 2 · Паритет

Для каждого id сравнить с `tools/tracks-ref/<id>.json`: число точек центра, первые/последние 5 точек,
сумма длин, позиции и углы props (1e-6), чекпоинты, конусы (число и первые 5), TABLE, старт. Любое
расхождение = дефект импортёра, не «погрешность».

### 3 · Меши

Лента: по центру с шириной `2·TRACK_HALF`, UV `u` вдоль длины (для будущей текстуры), `v` поперёк,
самопересечения (восьмёрки) допустимы – лента рисуется поверх стола с `+0.5 мм`. Стартовый флаг:
шахматка 2 ряда × `ceil(2·TRACK_HALF/10)` клеток чёрно-белая, всегда (как в 2D). Стол: прямоугольник или
эллипс (`TABLE.shape`), толщина 30 мм, кант 12 мм другим материалом; пол – плоскость `background`.
Чекпоинт: кольцо `CP_R` только для следующего (двойная обводка как в 2D). Материалы – серые заглушки,
темы в шаге 9, настоящие материалы – SPEC-05.

### 4 · Префаб

Из `TrackAsset`: объекты `Track`, `Table`, `Props/` (капсулы-плейсхолдеры размера `hl, r`, повёрнутые по
`ang`, с компонентом `PropRef(itemId)` – SPEC-05 заменит на модели), `Cones/`, `Caps/`, `Tires/`, `Checkpoints/`
(невидимые), `Spawn`. Префаб пересобирается идемпотентно (`PrefabUtility.SaveAsPrefabAsset`).

### 5 · Реверс

`ReverseTrack` из Core над `TrackAsset` в рантайме (не второй ассет): порядок точек, старт, чекпоинты,
индексы крышек стабильны по правилу 2D (порядок крышек не менять – сейв хранит индексы). `instanceId =
id + ":rev"`; превью зеркальное.

### 6 · Валидатор

Порт `check-item-clearance.js`: для каждого prop расстояние капсулы до центра ≥ `TRACK_HALF + margin`;
тест падает с именем трассы и предмета. Запускается в `unity-tests.sh`.

### 7 · Превью

Рендер полилинии центра (толщина `TRACK_GU*2*s` как в `renderTrackThumb`) в цветах темы в PNG 512²,
прямой и зеркальный; используется SPEC-06. Делается в Editor без сцены (`Texture2D` + растеризация линий
или камера над полилинией – проще камера).

### 8 · Проезд

PlayMode-тест: для каждого из 12 инстансов «ведём» машину по центру (телепорт по точкам с шагом
20 мм, `RaceRules.Step` с нулевым вводом не нужен – проверяем только `AdvanceCheckpoint`/финиш по
`crossedFinish`), ожидаем прохождение всех чекпоинтов и `LapDone`. Это дешёвая проверка геометрии, не
физики.

### 9 · Темы

`TrackTheme {background, table, tableEdge, track, skid, checkpoint, cone}` из `theme` модуля/`track_themes.json`;
применение в `TrackRuntime` к материалам стола/ленты/пола/следов/кольца; конус – фирменный
`#ff7a1a` всегда (правило из `track_themes.json`).

## Артефакты

`Content/Tracks/*.asset`, `*-thumb.png`, `Art/Prefabs/Tracks/*.prefab`, импортёры, тесты.

## Вне объёма

Модели предметов и материалы стола (SPEC-05), UI выбора трасс (SPEC-06), запись рекордов (SPEC-07).

## Acceptance-критерии

- `TrackImporter.ImportAll()` из CLI проходит; паритет 12/12 (6 прямых + реверс через Core) без расхождений.
- Валидатор зелёный на 6 трассах (как сейчас в 2D).
- PlayMode-проезд 12/12.
- `capture.sh` на каждой трассе – кадр с лентой, столом, конусами, капсулами.

## Гейт архитектора

Сверяю 2 трассы с 2D-игрой на глаз (кадр против скриншота 2D); проверяю, что SVG не скопированы,
что крышки индексируются как в 2D, что реверс – рантайм; читаю отчёт паритета.

## Риски

- `parseSvgPath` поддерживает ограниченный набор команд – импортёр обязан падать на неизвестной
  команде, не молчать.
- Самопересечения ленты (восьмёрки) и z-fighting – смещение `+0.5 мм` и `renderQueue`.
- Большая трасса (dining-oak: 35 предметов) – префаб тяжёлый, но статический батчинг решает.

## Связи

SPEC-01 §4, SPEC-02, SPEC-05, SPEC-06; `DesktopDrift/tracks/track_items_placement.md`.

## Находки гейта

(пусто)
