# SPEC-07 – Системы: сейв, экономика/магазин/ачивки/рекорды, платформенный слой, хаптика

> Core из SPEC-01 умеет всё это числом; здесь – связка с Unity: хранение, вызовы в нужных местах
> заезда (как в `game-engine.js`), платформенный слой без рекламы (D-10), хаптика Android.

## Цель

Прогресс сохраняется и переживает перезапуск; заезд платит шинами, пишет рекорды, выдаёт ачивки; магазин
продаёт и применяет косметику; платформенные хуки на местах; хаптика как в 2D.

## Вход

SPEC-01 (`SaveSchema`, `Migrations`, `Economy`, `Achievements`, `RaceRules`), SPEC-02
(`RaceLoop` события), SPEC-04 (`ILookSource`). Источники: `js/store.js` (API), `js/game-engine.js` строки `awardAchievements`, `addTires`, `capCollect`, `tireCollect`,
`js/race-results.js` (что показывается), `js/platform.js` (контракт), `js/haptics.js` (паттерны).

## Шаги и диспетчеризация

| № | Шаг | Диспетчеризация | Выход |
|---|---|---|---|
| 1 | `SaveStore` | ⟨Builder · general-purpose · sonnet · high · ≤50⟩ | `Game/Save/SaveStore.cs`: JSON ↔ `SaveNode` (Newtonsoft), load/merge/migrate, атомарная запись + бэкап |
| 2 | Версия схемы 3D-издания | ⟨Builder · general-purpose · sonnet · medium · ≤30⟩ | `SaveVersion = 1`, реестр миграций append-only (пустой), тест «неизвестная версия → бэкап + defaults» |
| 3 | Экономика в заезде | ⟨Builder · general-purpose · sonnet · high · ≤50⟩ | выплата по звёздам, первоклир +20, трофей +5, шины, крышки одноразово, `ledger` |
| 4 | Магазин | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `IShop`: покупка account-wide, look per-car, `buyCar`, валидация цен из `shop.json` |
| 5 | Ачивки | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `Evaluate` на финише и при покупке, награды в кошелёк, очередь тостов |
| 6 | Рекорды | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `records[trackInstance][mode]` = `{bestPPS, bestPPSTotal, bestPPSTime}`, `isNewPpsRecord` |
| 7 | Платформенный слой | ⟨Builder · general-purpose · haiku · medium · ≤25⟩ | `IPlatform` + `NullPlatform`, вызовы на тех же местах, что в 2D |
| 8 | Хаптика | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `Game/Platform/Haptics.cs` (Android `Vibrator`/`VibrationEffect` через `AndroidJavaObject`), паттерны из `haptics.js` |
| 9 | Проверка на устройстве | ⟨Gate-runner · general-purpose · sonnet · medium · ≤30⟩ + владелец | `artifacts/save-check.md` |

### 1 · SaveStore

Файл `persistentDataPath/save.json` (+ `save.prev.json` перед каждой записью), запись во временный файл
и `File.Move` (атомарно), загрузка: прочитать → `SaveSchema.Load` (слияние поверх defaults, миграции
по `version`) → в память; `Save()` после каждого изменяющего вызова (как `store.js` сохраняет сразу),
но не чаще раза в кадр (debounce в конце кадра). Любая ошибка чтения → бэкап и defaults, лог.

### 2 · Версия схемы (D-17)

`version: 1` в корне сейва; `Migrations.Apply(node)` – append-only реестр, сейчас пустой; версия больше
известной → файл не трогается, в память идут defaults, пишется лог и флаг для UI («сейв из более новой
версии»). Никакого кода совместимости с веб-сейвом.

### 3 · Экономика

В событиях `RaceLoop`: `Finished` → `addTires(finishPayout(pps))`, первоклир (`stats.cleared`), трофей
1-PPS (`stats.trophies`), `runs++`, `driftSecs +=`; `TireCollected` → `+value` (респаун каждый заезд,
`tiresSwept` при полном сборе); `CapCollected` → `capCollect(instanceId, i)` одноразово + `CAP_TIRE_VALUE`;
`ledger` с причинами как в `wallet-history.js`. Порядок и суммы – из `game-engine.js` (цитаты в брифе).

### 4 · Магазин

`shop.json` → `IShop.Buy(itemId)`: проверка баланса, `owned` (account-wide), `garage.cars[idx]` look
(per-car), `buyCar(carId)` → `ownedCars`; гейт владения выключен (как 2D `CAR_GATING_ENABLED=false`).

### 5 · Ачивки

`AchievementContext` собирается на финише (поля как в `awardAchievements`) и после покупки; результат →
`achievements[id]`, награды → кошелёк + `ledger`, очередь тостов (UI SPEC-06), `happyMoment()`.

### 6 · Рекорды

Ключ `records[instanceId][mode]` (`instanceId` с `:rev`, `mode` ∈ `timeattack`), поля `bestPPS`,
`bestPPSTotal`, `bestPPSTime`; `isNewPpsRecord` с семантикой null; Zen/Sandbox рекордов не пишут (как 2D).
Решение O-10: per-car – 1.1.

### 7 · Платформа

`IPlatform { Init(); GameplayStart(); GameplayStop(); CommercialBreak() → Task; HappyMoment(); }`,
`NullPlatform` – всё пусто, `CommercialBreak` сразу завершён. Точки вызова: `GameplayStart` после GO,
`GameplayStop` на финише/выходе, `CommercialBreak` перед «Race again» (слот остаётся, ничего не показывает),
`HappyMoment` на рекорде/ачивке. Реклама не подключается (D-10).

### 8 · Хаптика

Паттерны из `haptics.js` (конус, краш, сбор, рекорд – взять список) → `VibrationEffect.createWaveform`
(API 26+), настройка `settings.haptics` как гейт; на ПК – ничего.

### 9 · Проверка

Сценарий: заезд → выплата → закрыть приложение (swipe) → открыть → баланс и рекорд на месте; покупка финиша → виден в заезде; ачивка с тостом; выключение хаптики.

## Артефакты

`Game/Save/*`, `Game/Platform/*`, тесты (EditMode на `SaveStore` с временной папкой), `artifacts/save-check.md`.

## Вне объёма

Реклама, IAP, аналитика (D-10), импорт профиля из веба (D-17), экспорт/импорт сейва между устройствами (1.1),
облачный сейв, лидерборды, per-car рекорды (1.1).

## Acceptance-критерии

- EditMode: `SaveStore` round-trip, слияние поверх defaults на фикстурах из `store*.test.js` 2D,
  атомарность (имитация сбоя записи → бэкап цел), неизвестная версия → бэкап + defaults.
- Сценарий §9 пройден на устройстве.

## Гейт архитектора

Прогон тестов; проверка, что
все мутации идут через `SaveStore` (`grep -rn "File.Write" unity/Assets/Game` – только в `SaveStore`);
проверка точек вызова платформы по `grep` в `game-engine.js`/`race-results.js`.

## Риски

- Атомарная запись на Android: `File.Move` поверх существующего файла бросает исключение – использовать
  `File.Replace` или удаление + переименование с бэкапом; тест с временной папкой.

## Связи

D-10, O-10; SPEC-01 §6, SPEC-02 (события), SPEC-04 (`ILookSource`), SPEC-06 (экраны).

## Находки гейта

(пусто)
