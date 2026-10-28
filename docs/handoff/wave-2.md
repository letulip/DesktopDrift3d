# Handoff – Волна 2: SPEC-01 перенос Core 1:1 (28.10.2026)

## Сделано
- Шаг 0 8a01063: Unity CLI beta.14 (D-20 №7), полный контур зелёный, навык CLI обновлён
- 1 6d28078: `tools/export-data.mjs` → 15 JSON-таблиц в `unity/Assets/Content/_json/` (ничего не перепечатано)
- 2 b0cc03a: `Scoring` (полный), `Economy`, `Cola` + 36 тестов и сверка констант с JSON
- 3 d9a49fb: `Collision` + `TableRect/Prop/Cone` + 22 теста
- 4a 5d5e5fc: `TrackUtil`, `TireSeed`, `TrackData` + 37 тестов; 4b 2f7c2d6: эталоны трасс через linkedom + `TrackBuilder.FromSvg`, паритет 12/12
- 5a f75c762: `Achievements` (62 определения, каталог = `achievements.json`) + 35 тестов; 5b 685d743: `CarStats`, `Neon`, `ShopCatalog`, `CarOrder`, `Economy.CarOwned` + 36
- 6 4475fc2: `Json`, `SaveSchema` (версия 1, D-17), `SaveState` (весь API `store.js`), `AchievementSync` + 41 тест
- 7a 6f91061 / 7b 41fd5bf: сценарные трассы wall/props/cones + харнес настоящего `game-engine.js` (следы oval, green-study)
- 8a 3bf5c35 / 8b 045b410: `OvalTrack`, `RaceState/Options/Event/Result`, `RaceRules` – реплей следов покадрово
- 9 765a854: ревью (`wave-2-review.md`): блокеров 0, исправить 9, заметок 11
- Раунд правок A 6490ce2 (харнес v2: ещё 3 следа, stats/achievementsMap/save в final, цвета вспышек); B 6a8a92c, 5078664, 14401b2 (все девять И закрыты)
- Архитектор: 6c2b7b8 V8-hypot, 4a27bb9/f6ca58b часы леджера инжектированы, 225c675 контракты хоста §12, dcecfb8 находки гейта Unity, .meta-коммиты 3e9ad5d/232f73d/b014d66

## Открыто
- SPEC-02: каталог `CarDefinition` в Core, чтобы `RaceRules` сам брал машину по `carIndex` (ревью И3; сейчас – контракт хоста §12 п.5)
- `Json.Serialize` печатает нецелые числа через `"R"` – round-trip есть, с JS не побайтно (заметка ревью; на Mono не падает)
- `NO PROGRESS!`, трофей 1-PPS, Perpetual Motion, круглый стол – покрыты юнит-тестами, следом движка нет
- Бит-в-бит с JS не обещается (IL2CPP может свернуть FMA); закон – допуск 1e-6 (SPEC-01 находка 1)
- Тесты паритета трасс читают SVG из `/Users/letulip/Projects/Claude/DesktopDrift` (или `DESKTOPDRIFT_DIR`); без репо – падение, обход `DD3D_SKIP_2D_PARITY=1`
- Вопросы владельцу: `docs/handoff/wave-2-questions.md` (3 наблюдения, решений не требуют)

## Файлы
- `unity/Assets/Core/{Physics,Collision,Scoring,Rules,Track,Meta,Save,Race}/` – 60+ файлов чистой логики (без UnityEngine, DateTime, Random, float)
- `unity/Assets/Core.Tests/` – 268 NUnit-тестов (те же в dotnet и Unity EditMode)
- `unity/Assets/Content/_json/*.json` – экспорт таблиц 2D-игры с `_source`
- `tools/{export-data,export-tracks,parity-trace,engine-harness,js-refs}.mjs`, `tools/tracks-ref/`, `tools/engine-trace-*.json`, `tools/{jsfixed,neon}-ref.json`, `tools/package.json` (linkedom 0.18.14)
- `scripts/{export-data,export-tracks,trace,engine-trace}.sh` – новые гейты контура (контракт `*_EXIT`)
- `docs/handoff/wave-2-review.md`, `wave-2-questions.md`; `docs/03-architecture.md` §12; SPEC-01 «Находки гейта» 1–12

## Гейты
- test.sh: 268/268 за 2 с · unity-tests.sh: 268/268 за 11 с (Mono) · compile.sh: 0/0 · build.sh android: 35 МБ, 111 с (холодная после правок Core)
- trace.sh: physics 1.8e-12 · wall 1.5e-12 · props 1.2e-11 · cones 1.4e-12 (все ≤ 1e-6, 4 файла воспроизводимы)
- export-tracks.sh: 12/12 паритет `TrackBuilder` ↔ JS, перегенерация без диффа · engine-trace.sh: 5 следов воспроизводимы, реплей покадрово точен, полный сейв совпадает
- ревью: блокеров 0; все 9 «исправить» закрыты и перепроверены гейтом

## Бюджет
- агентов: 13 (11 шагов + ревью + 2 раунда правок) · ходов агентов: ≈208 из ~500 по таблице спеки · сверх бюджета: никто · гейты и точечные правки – архитектор в родительской сессии
