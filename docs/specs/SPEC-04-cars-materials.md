# SPEC-04 – Машины: приём моделей владельца, система материалов и косметики, гараж-поворотник

> Решение D-09: владелец приносит две первые модели (Bismark, Panda); агенты доводят их до кондиции
> и строят пайплайн, по которому остальные шесть принимаются одним агентом на машину. Решение D-13:
> в 1.0 косметика = цвета кузова, финиши, следы, неон solid + один layout; стекло/обводки/настроения
> (D-04) – 1.1, но якоря закладываются сейчас.

## Цель

`CarDefinition` + префаб на каждую принятую машину: части, колёса, материалы, якоря неона и следов,
применение «look» из сейва (цвет, финиш, след, неон); гараж показывает машину на поворотнике.

## Вход

SPEC-02 (сцена, `CarView`), данные `cars.json` (SPEC-01). От владельца – две модели по чек-листу §2.
Источники для чтения: `js/finish.js` (пресеты), `js/neon.js`, `js/neon-draw.js` (геометрия зон между
арками), `js/palette.js`, `js/car-preview.js` (как рисуется превью), `docs/plans/neon.md` §зоны.

## Шаги и диспетчеризация

| № | Шаг | Диспетчеризация | Выход |
|---|---|---|---|
| 1 | Чек-лист приёма + валидатор Blender | ⟨Importer · general-purpose · sonnet · high · ≤50⟩ | `docs/specs/car-intake-checklist.md`, `tools/blender/validate_car.py`, `normalize_car.py` |
| 2 | Приём двух моделей владельца | ⟨Importer · general-purpose · sonnet · high · ≤60⟩ | `Art/Models/Cars/{bismark,panda}.glb` нормализованные, отчёт |
| 3 | `CarPrefabBuilder` | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | `Editor/Importers/CarPrefabBuilder.cs`, `Art/Prefabs/Cars/*.prefab`, `Content/Cars/*.asset` |
| 4 | Система материалов: финиши, цвета, стекло | ⟨Builder · general-purpose · opus · high · ≤60⟩ | `Art/Materials/Car/*`, `Game/Car/CarLook.cs`, Shader Graph pearl |
| 5 | Неон: зоны и свечение | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | `Game/Car/NeonRig.cs`, зоны из `NeonMath.ZoneColors` |
| 6 | Следы и look из сейва | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | цвет следов, `CarLook.Apply(garage.cars[idx])` |
| 7 | Гараж-поворотник | ⟨Builder · general-purpose · sonnet · medium · ≤50⟩ | `Game/Garage/Turntable.cs`, камера и свет гаража, `RenderTexture` для UI |
| 8 | Статы машин для UI | ⟨Builder · general-purpose · haiku · low · ≤20⟩ | `CarStats` → проценты полос spd/hdl/acc по `cars.json` |
| 9 | Процедура приёма машин 3–8 | ⟨Importer · general-purpose · sonnet · high · ≤60 на машину⟩ + владелец (фил) | по одной машине за агента, вне критического пути 1.0 |

### 1 · Чек-лист приёма (для владельца) и валидатор

Чек-лист (`car-intake-checklist.md`), что должно быть в модели:
- файл `.blend` + экспорт `.glb` (glTF 2.0, +Y up, −Z forward по glTF; в Blender «−Z forward, +Y up» при экспорте);
- масштаб: длина кузова = `len` машины из `cars.json` в миллиметрах (Bismark/Panda – взять число), единицы
  сцены Blender – миллиметры или метры с правильным `unit scale`; валидатор сам приводит к `len`;
- пивот в центре днища (середина колёсной базы, уровень земли), нос по +X 2D ≡ `forward`;
- отдельные меши с именами `Body`, `Glass`, `Wheel_FL`, `Wheel_FR`, `Wheel_RL`, `Wheel_RR`, опционально
  `Lights`, `Details`; у колёс пивот в их центре; треугольников: кузов ≤ 3000, всего ≤ 6000;
- материалы: один на `Body` (цвет-плейсхолдер `#D9D9D9`, он перекрашивается), `Glass`, `Wheel`, `Dark`;
  текстур нет (плоские цвета), нормали сглажены по группам (flat low-poly).
`validate_car.py` (`blender -b model.blend --python validate_car.py`): печатает таблицу проверок с
PASS/FAIL и числами; `normalize_car.py` – чинит что можно автоматически (применить трансформы, пивот,
масштаб по `len`, имена по эвристике «самый большой меш = Body», экспорт glb) и пишет что починено.

### 2 · Приём

Для каждой из двух моделей: валидатор → нормализация → повторный валидатор → glb в `Art/Models/Cars/`,
импорт-настройки Unity (scale 0.001 если модель в мм, `Read/Write` off, mesh compression off для
точности, `Generate Lightmap UVs` off). Отчёт владельцу: что было не так, что исправлено автоматически,
что надо исправить в Blender (например, разбить колёса).

### 3 · Префаб и `CarDefinition`

Из `cars.json` + glb: `CarDefinition` (id, name, price, ratings, drive, len, vw, vh, flip, prefab);
префаб: корень с `CarView`, `Body`, `Glass`, колёса с `WheelVisual` (вращение по пройденному пути
`speed·dt / r`, поворот передних по `steerSmooth·maxSteerDeg`), якоря: `NeonZones` (6 точек: три пары
вдоль днища между арками – геометрия как в `neon-draw.js`: сегменты между колёсными арками),
`TrailAnchors` (2, у задних колёс), `WindshieldAnchor` (центр лобового, для 1.1). Пересборка идемпотентна.

### 4 · Материалы

`CarLook`: `bodyColor` (палитра 20 из `palette.json` + свободный hex как в 2D), `finish ∈ {matte, metallic,
pearl, chrome}` → пресеты URP Lit (`_Smoothness`, `_Metallic`; pearl – Shader Graph с fresnel-сдвигом
оттенка; chrome – metallic 1 + smoothness 0.95 + reflection probe гаража/трассы), `glass` – прозрачный
материал с дефолтным цветом (`#222222` как в 2D), `outline` – параметр зарезервирован (1.1), `Dark` –
общий. Все материалы – инстансы через `MaterialPropertyBlock`, не `renderer.material` (утечки).
Визуальная приёмка: 4 кадра `capture.sh` (по финишу) на поворотнике.

### 5 · Неон

`NeonRig`: 6 эмиссивных полос (тонкие квады под днищем по якорям) + один аддитивный квад свечения на
земле (мягкая маска), цвета каждый кадр из `NeonMath.ZoneColors(neon, t)` (Core) – layouts `solid` и
`longitudinal` в 1.0, `front-mid-rear`/`per-zone` и анимации – код уже умеет (математика общая), в UI
не продаются до 1.1. Bloom URP только от эмиссии неона (порог), на мобильном – аддитивный квад без bloom.

### 6 · Следы и look из сейва

`CarLook.Apply(look)` – из `garage.cars[carIndex]` (SPEC-07 даёт `SaveStore`; здесь – интерфейс
`ILookSource`): цвет кузова, финиш, цвет следа (каталог `trail-*`), неон. Дефолты – как в 2D
(`defaultNeon(colour)`).

### 7 · Гараж-поворотник

Отдельная область сцены (вне стола), подиум, 3-точечный свет, камера рендерит в `RenderTexture`
для экрана гаража (SPEC-06); вращение по инерции от свайпа/перетаскивания; смена машины – кроссфейд.

### 9 · Процедура приёма машин 3–8 (повторяемая)

Бриф одного агента на одну машину: вход – `.blend/.glb` от владельца (или из открытого набора по D-03,
лицензия в credits), шаги 1→2→3→6, кадры с поворотника, отчёт; гейт – владелец смотрит кадры и подписывает.
Критический путь 1.0 этого не ждёт: SPEC-06/07 работают с тем числом машин, что принято; в магазине
показываются только принятые (ownership-гейт `CAR_GATING_ENABLED` остаётся как в 2D – выключен).

## Артефакты

`car-intake-checklist.md`, `tools/blender/*.py`, `Art/Models/Cars/*.glb`, `Art/Prefabs/Cars/*`,
`Content/Cars/*.asset`, `Art/Materials/Car/*`, Shader Graph pearl, `Game/Car/*`, `Game/Garage/*`.

## Вне объёма

UI гаража и магазина (SPEC-06), покупка и сохранение look (SPEC-07), настроения/тинты/обводки (1.1),
остальные шесть машин до гейта этого этапа.

## Acceptance-критерии

- Валидатор на обеих моделях PASS; glb открываются в Unity без предупреждений масштаба.
- Префабы собираются из CLI (`-executeMethod CarPrefabBuilder.BuildAll`) идемпотентно.
- 4 финиша × 2 машины – кадры в `artifacts/cars/`; неон solid + longitudinal виден на кадре.
- В заезде машина заменяет кубоид SPEC-02, колёса крутятся и поворачивают, следы от задних колёс.
- Владелец подписал обе машины (запись в `01-decisions.md`).

## Гейт архитектора

Смотрю кадры и логи валидатора; проверяю, что физика не тронута (трасса паритета зелёная), что
материалы через `MaterialPropertyBlock`, что якоря неона совпадают с геометрией `neon-draw.js`
(позиции зон относительно арок); проверяю повторяемость процедуры §9 на «третьей машине» из CC0-набора
(сухой прогон без приёма владельцем).

## Риски

- Модели владельца приедут не по чек-листу – нормализатор чинит типовое, остальное возвращается с
  конкретным списком; не переделывать модель агентом «как понял».
- Pearl/chrome на мобильном без reflection probe выглядят плоско – probe запекается в сцене трассы.
- Тонкие неоновые квады z-fighting со столом – смещение и `renderQueue`.

## Связи

D-03, D-04, D-09, D-13, O-07; SPEC-02 (`CarView`), SPEC-06 (гараж, модификация), SPEC-07 (look в сейве).

## Находки гейта

(пусто)
