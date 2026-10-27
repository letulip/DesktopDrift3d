# Целевая архитектура DesktopDrift3d

Документ фиксирует решения, которые спеки считают данностью. Изменение – через `01-decisions.md`.

## §1 · Стек

- **Unity 6.3 LTS, 6000.3.27f1** (установлена 27.10.2026, `/Applications/Unity/Hub/Editor/6000.3.27f1`), URP, IL2CPP, ARM64; Input System (новый), UI Toolkit (runtime),
  Unity Test Framework (NUnit), Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`).
  Addressables – только если SPEC-09 покажет проблему размера. Cinemachine – не по умолчанию
  (камера своя, детерминированная, с математикой в Core; см. SPEC-02 §5).
- **dotnet SDK** (есть на маке) для `dotnet test` по Core без Unity.
- **Node** – для экспорта данных из JS-модулей 2D-игры и трассы паритета.
- **Blender** (есть) – скрипты приёма моделей (Python), запуск `blender -b --python`.
- **Unity CLI 1.0.0-beta.13** (официальный, `~/.unity/bin/unity`, PATH через `~/.unity/env`): `test`,
  `build`, `run`, `recompile`, `command` в живой редактор, официальный MCP-сервер редактора (`unity mcp`).
  Все скрипты `scripts/*.sh` – тонкие обёртки над ним; сторонние MCP-пакеты не используются (D-18).

## §2 · Репозиторий и папки

```
DesktopDrift3d/
  README.md  CLAUDE.md  .gitignore
  docs/                      пакет архитектора (этот документ и спеки)
  unity/                     Unity-проект (создаётся в SPEC-00)
    Assets/
      Core/                  DesktopDrift.Core.asmdef (noEngineReferences) – чистая логика
      Core.Tests/            DesktopDrift.Core.Tests.asmdef (Editor, NUnit) – чистые тесты
      Game/                  DesktopDrift.Game.asmdef – MonoBehaviour-слой (машина, цикл, камера, ввод, следы, сборщики)
      Game.Tests/            PlayMode-тесты (минимум: сцена грузится, заезд проходит)
      Editor/                DesktopDrift.Editor.asmdef – импортёры (SVG, glTF, данные), сборка, валидаторы, захват кадра
      Content/               ScriptableObject-ассеты: Cars/, Tracks/, Items/, Shop/, Achievements/, Palettes/
      Art/                   Models/, Materials/, Textures/, Prefabs/ (машины, предметы, стол)
      UI/                    UXML/, USS/, Fonts/, Icons/
      Audio/                 Sfx/ (запечённые), drift.wav
      Scenes/                Boot.unity, Game.unity
      Settings/              URP-ассеты, InputActions
    Packages/manifest.json   ProjectSettings/
  core-tests/                DesktopDrift.Core.Tests.csproj – dotnet-проект, включает те же .cs из unity/Assets/Core и Core.Tests
  tools/                     export-data.mjs, parity-trace.mjs, bake-sfx.mjs, blender/*.py
  scripts/                   test.sh trace.sh compile.sh unity-tests.sh build.sh capture.sh
  artifacts/                 (gitignored) сборки, кадры, логи, отчёты
```

Один источник исходников: `.cs` живут только в `unity/Assets/…`; `core-tests/*.csproj` подключает
их через `<Compile Include="../unity/Assets/Core/**/*.cs" />` (и `Core.Tests`). Тесты в
`Core.Tests` – только NUnit-атрибуты (`[Test]`, `[TestCase]`), никаких `[UnityTest]`, чтобы
компилироваться в обоих мирах.

## §3 · Границы сборок

| Сборка | Ссылается на | Содержит |
|---|---|---|
| `DesktopDrift.Core` | ничего (ни UnityEngine, ни Newtonsoft) | физика, коллизии, скоринг, крышки, экономика, ачивки, неон-математика, рейтинги машин, геометрия трасс (парсер SVG-пути, Chaikin, ближайшая точка, конусы, реверс, посев шин), схема сейва и миграции над простым деревом (`SaveNode`: словарь/список/число/строка/bool), математика камеры |
| `DesktopDrift.Game` | Core, UnityEngine, Input System, UI Toolkit | компоненты сцены, адаптеры (JSON ↔ SaveNode через Newtonsoft), платформенный слой, аудио, хаптика |
| `DesktopDrift.Editor` | Core, Game, UnityEditor | импортёры, генераторы ассетов, сборка, валидаторы, захват |

Правило: если кусок логики можно проверить числом – он в Core. Если ему нужен `Transform` –
он в Game, но тонкий: берёт числа из Core и раскладывает их по объектам.

## §4 · Единицы, координаты, время

- **1 игровая единица = 1 мм.** Core считает в единицах 2D-игры (`TRACK_HALF = 100`,
  `CONE_R = 9`, `len` машины 78 и т. д.) в `double`. В метры переводит только Game:
  `const float UnitsToMeters = 0.001f`.
- **Плоскость.** 2D `(x, y)` → Unity `(X, 0, Z)` с `Z = -y`: `ToWorld(x, y) = new Vector3(x, 0, -y) * UnitsToMeters`.
  При камере сверху (вниз по −Y, «верх экрана» = +Z) картинка совпадает с 2D без зеркала.
- **Курс.** 2D-угол `a` (радианы, `fwd = (cos a, sin a)`) → Unity `yawDeg = a * Rad2Deg + 90`
  (`Quaternion.Euler(0, yawDeg, 0)`; проверка: `transform.forward == (cos a, 0, -sin a)`).
  Эта формула – в одном месте (`Game/WorldMapping.cs`) и в одном тесте.
- **Время.** Физика в `FixedUpdate`, `Time.fixedDeltaTime = 1/60`, dt всегда ровно 1/60 (как в
  трассе паритета); JS-ограничение `dt ≤ 0.05` сохраняется как страховка при лагах
  (`maximumDeltaTime`). Рендер интерполирует между двумя последними состояниями Core.
- **Детерминизм.** В Core нет `Random`, `DateTime`, `float`-арифметики; все «шумы» (wobble)
  зависят только от `physT`, как в JS.

## §5 · Данные: из JS в ScriptableObject без перепечатки

`tools/export-data.mjs` (Node, ESM) импортирует модули 2D-игры по абсолютному пути и пишет
`unity/Assets/Content/_json/*.json`: `cars.json` (из `js/config.js` CARS + `cars-data.js`),
`items.json` (`js/items.js`), `tracks.json` (`js/track-registry.js` + `laps`/`tires` из
трек-модулей), `shop.json` (`js/shop-catalog.js`), `achievements.json` (`js/achievements.js`
каталог), `palette.json` (`js/palette.js`), `sfx.json` (`js/sound-params.js`), `economy.json`
(константы). Editor-импортёр (`Editor/Importers/DataImporter.cs`) превращает JSON в
ScriptableObject-ассеты; JSON коммитится (для диффа), ассеты – тоже (Unity их читает).
Изменился источник → перезапуск экспорта → дифф виден.

## §6 · Контент-типы (ScriptableObject)

- `CarDefinition`: id, name, price, ratings, drive (все поля `_drive`), len/vw/vh, model prefab,
  material slots, якоря: 6 зон неона, 2 точки следов, лобовое (на 1.1).
- `TrackAsset`: id, name, desc, laps, theme (цвета), TABLE (w, h, shape), центр/внешняя/внутренняя
  полилинии (после Chaikin), чекпоинты, конусы, props (item id, x, y, ang), collectibles (крышки),
  tires (посев), startPos/startAngle, reverse-вариант строится на лету (`:rev`).
- `ItemDefinition`: id, hl, r, kind, prefab, fallback colour.
- `ShopItem`, `AchievementDefinition`, `PalettePreset`, `NeonPreset`, `SfxDefinition`.

## §7 · Сцены и жизненный цикл

Одна игровая сцена `Game.unity` + `Boot.unity` (загрузка сейва, платформенный init). Экраны
меню/гаража/настроек – UI Toolkit документы поверх той же сцены (гараж использует 3D-поворотник
в сцене). Переходы – `ScreenRouter` со стеком; «Race again» пересоздаёт состояние заезда, а не
сцену (в 2D это делал `location.reload()` – здесь нужен явный `Reset`, см. SPEC-02 §9).

## §8 · Сейв

Схема = форма `store.defaults()` 2D-игры (v4), принятая как **версия 1 схемы 3D-издания**;
миграции 2D (v2–v4) не переносятся – импорта веб-профиля нет (D-17); новые миграции append-only
от версии 1, сейв неизвестной версии не уничтожается (бэкап + defaults + лог). Хранение:
JSON-файл в `Application.persistentDataPath` + резервная копия предыдущей версии. Рекорды –
ключ как в 2D в MVP, per-car – 1.1 (D-16).

## §9 · Представление: материалы и свет

Flat-shaded low-poly, один направленный свет, мягкие тени на ПК и запечённые/без теней на
мобильном (настройка качества по устройству), bloom только под неон, цветовые пресеты трасс из
`theme`. Косметика как материалы: финиши (matte/metallic/pearl/chrome) – пресеты URP Lit (+
маленький Shader Graph для pearl), обводка – inverted hull (1.1), стекло – прозрачный материал
(тинты 1.1), неон – 6 эмиссивных зон + аддитивный квад свечения под машиной, цвета из
`zoneColors(neon, t)` (Core), следы – TrailRenderer/меш-полосы цветом из каталога.

## §10 · Тест-стратегия

| Слой | Инструмент | Что доказывает |
|---|---|---|
| Core | `dotnet test` (секунды), те же тесты в Unity EditMode | формулы и данные 1:1 с JS; трасса паритета 1e-6 |
| Game | PlayMode-тесты (минимум), скриншоты из batchmode (RenderTexture, не `ScreenCapture`) | сцена грузится, заезд проходит до результата, кадр рендерится |
| Устройство | владелец + чек-лист + `adb logcat` | 60 fps, тач, хаптика, сейв, фил |
| Сборка | `scripts/build.sh` с отчётом | размер AAB, время, стриппинг |

## §11 · Конвенции кода

- Namespace `DesktopDrift.Core`, `DesktopDrift.Game`, `DesktopDrift.Editor`; один класс – один
  файл; имена из JS сохраняются (`StepCar`, `ResolveWall`, `ComboGain`), константы – как в JS
  (`DRIFT_MIN_SLIP` → `DriftMinSlip` с комментарием-источником `// scoring.js: DRIFT_MIN_SLIP`).
- Каждый перенесённый файл начинается с заголовка: источник, версия/коммит DesktopDrift,
  дата, «VERBATIM PORT – do not reorder».
- Строки интерфейса – из 2D-игры дословно, в одной таблице `UI/Strings.cs` (на будущее i18n).
- Коммиты: `spec-NN step k: <что>` + `Co-Authored-By` по правилам сессии.
