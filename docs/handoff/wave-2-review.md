# Волна 2 – ревью шага 9 (SPEC-01, порт ядра)

Ревьюер: построчная сверка JS (DesktopDrift @ 5556140) ↔ C# (`unity/Assets/Core`), 28.10.2026.
Вопрос: где C# может повести себя иначе, чем JS, так что нынешние тесты этого НЕ поймают.
Принятые расхождения из SPEC-01 «Находки гейта» (п. 1–9) не повторяются.

**Вердикт: расхождений уровня блокер: 0.** Исправить – 9, заметок – 11.

## 1. JS → C#, статус

| JS | C# | Статус | Замечание |
|---|---|---|---|
| physics.js | Physics/CarPhysics.cs | ✓ | порядок операций, все константы, `pow`, ветка `speed > 40`, циклы wrap – дословно |
| collision.js | Collision/Collision.cs | ✓ | 4 стены, 1.3/1.4/0.8/−0.4/0.9/0.85/−0.3, `knocked`-skip, round – дословно; `\|\| 1` → З2 |
| scoring.js | Scoring/Scoring.cs | ✓ | все константы, `== null` → `double?` |
| economy.js | Rules/Economy.cs | ✓ | `\|\| 0` → `Num(NaN)`, `isOnePps` через JsRound (→ З1) |
| cola.js | Rules/Cola.cs | ✓ | дословно |
| game-engine.js startGame/frame/finish/award | Race/RaceRules.cs | ⚠ | ветки и порядок совпадают; цвета/тексты flash совпадают все 9; И1, И2, И3, И8 |
| state.js | Race/RaceState.cs | ✓ | дефолты S_DEFAULTS совпадают; новый `RaceState` на `Start()` = `resetState()` |
| store.js | Save/SaveSchema.cs, SaveState.cs | ⚠ | `_merge`, LEDGER_MAX 50, идемпотентность, clamp 0, `markCleared` – верно; caps по индексу → И2; З10, З11 |
| ach-sync.js | Save/AchievementSync.cs | ✓ | цикл до неподвижной точки дословно; строки в caps теряются → И2 |
| achievements.js | Meta/Achievements.cs | ✓ | 10 проверок сверены буквально (clean-sweep, slalom-saint, bulldozer, untouchable, one-pps, cola-collector, night-owl, absolute-ddk, ddk-*, лестницы drift×60/hoard=wallet); `_norm`, `evaluate` верны |
| track-util.js | Track/TrackUtil.cs | ✓ | inner-gap, spacing 160, окно 24, argmax `>` (первый выигрывает), `reverseTrack` делит cones/props/collectibles как `{...T}` |
| tire-seed.js | Track/TireSeed.cs | ✓ | дословно |
| track-factory.js | Track/TrackBuilder.cs | ⚠ | порядок документа (line+path), regex proxy и `resolveKey` верны; `toFixed(3)` → И4; З9 |
| neon.js | Meta/Neon.cs | ⚠ | логика верна; печать hue через рантаймный `"R"` → И5 |
| car-stats.js | Meta/CarStats.cs | ⚠ | `toFixed` эмуляция → И4 |

## 2. Находки

### Блокер
Нет. Каждая найденная разница либо требует ошибки хоста, либо данных, которых сейчас нет в игре.

### Исправить

**И1. После финиша `Step` продолжает симуляцию.** JS после финиша отменяет rAF (game-engine.js:596, 641) – больше кадров нет.
C#: `S.RaceFinished` пишется (RaceRules.cs:638), но `Step` его не читает (RaceRules.cs:364–369, RaceState.cs:91). Если хост
вызовет `Step` ещё раз, круги пойдут дальше, второй финиш заплатит второй раз. → В начале `Step`: `if (S.RaceFinished) return _ev;` и тест
«Step после Finished ничего не меняет и не платит».

**И2. Крышки сохраняются по индексу, а не по `capId`.** JS читает `c.capId ?? i`, `"x,y"`, `i` и пишет `capId`
(game-engine.js:99–101, 201; store.js:203–208). C#: читает и пишет индекс (RaceRules.cs:94–98, 254); `CollectedCaps`
приводит строку `"x,y"` к `(int)Num(o)` = **0**, то есть помечает collectible №0 собранным (SaveState.cs:139–146). Ещё
`AchievementSync.cs:60–71` и `RaceRules.cs:314–321` выбрасывают строковые элементы, и тогда soda-pop/cola-collector не видят крышки в формате 2D.
Последствия: формат сейва отличается от 2D; правка SVG сдвигает индексы, и отметка «собрано» переходит на другой предмет; сейв в формате 2D
(загружается по находке 8) ломается. → Хранить `Collectible.CapId` (строки), восстанавливать тремя ключами, как в JS, а в контексте
`Caps: Dictionary<string, List<object?>>` считать `Count`.

**И3. Машина берётся из двух источников.** JS берёт `M = CARS[S.carModel]` из гаража для физики, CR и nose
(game-engine.js:344, 409–410, 424–425). C#: `drive/len/wid` приходят через конструктор (RaceRules.cs:56–67), `CarModel` считается отдельно
(103–106) при `RaceOptions.CarCount = 1` по умолчанию (RaceOptions.cs:26). Если хост забудет CarCount, будет `CarModel = 0` и в результате
окажется `Look` машины 0; если передаст чужой профиль, физика разойдётся с выбором в гараже. → `RaceRules` принимает ростер CARS и сам
разрешает P/len/wid по зажатому `carIndex`; `CarCount` убрать.

**И4. Обе эмуляции `toFixed` расходятся с JS; проверено в node.** `2.675.toFixed(2)` = 2.67, а путь JsRound даёт 2.68;
`1.2345.toFixed(3)` = 1.234, а обе эмуляции дают 1.235; `(-0.0005).toFixed(3)` = −0.001, а JsRound даёт 0. Места: CarStats.cs:37–42
(car-stats.js:36–37) и TrackBuilder.cs:141–142 (track-factory.js:125). Сейчас не достигается: рейтинги целые, atan2 на ничью не попадает,
паритет прошёл. Но расхождение спит и проснётся при дробном рейтинге. → Один `CoreMath.JsToFixed(x, d)` на точном разложении double
(BigInteger мантисса·2^e, знак отдельно, ничья вверх по модулю) + векторный тест из node; тогда исключение `Math.Round` уходит из гейта g1.

**И5. Числа печатаются форматами рантайма `"R"`/`"F2"`.** Места: RaceRules.cs:135–138 и 521 (`LAP x.xx s`), Neon.cs:142 (hue),
`Achievements.FmtCount`, `TrackUtil.JsNumber` (304–307). На dotnet `"R"` = кратчайшая запись = JS. На Mono Unity 6000.3 (BCL .NET Framework)
`"R"` = G15→G17, а `"F2"` округляет от 15 значащих цифр: hue `7.000000000000001` (rainbow, t = 0.1) там печатается как `7.0000000000000009`.
**не проверено** на рантайме Unity/IL2CPP. → `CoreMath.JsNumberToString` (первая точность 1..17, которая даёт round-trip) и
`JsToFixed` из И4 для LAP; векторные тесты прогнать в EditMode и на IL2CPP-сборке.

**И6. Сценарные тесты доказывают меньше, чем пишет спека (находка 9).** Wall (ParityScenarioTests.cs:123–149) нигде не утверждает касание
стены («358/514 кадров») и касание в round-прогоне. Props (179) проверяет только `crashes > 0`, а не «4 предмета задеты». Из трёх заявленных
покрытий утверждается только конусы (255). → Считать кадры с `wallHit > 0` по каждой форме и сверять с числом из JSON; считать различные
задетые props и требовать 4.

**И7. Финальный сейв в реплее сверяется частично.** RaceReplayTests.cs (блок `final`) сверяет wallet, ledger, records и ID разблокированных
достижений. Не сверяются: `stats` (runs, driftSecs, cleared, tiresSwept, trophies, perpetuals, caps), прогресс лестниц
(drift-*/races-*/hoard-*), `flashColor` и цвета событий. Все поля `frameFields` и все ключи `final` покрыты, неизвестное поле кадра падает
(подтверждено). Неизвестный ключ `final` молча пропускается. → Харнес пишет весь `snapshot()`, тест сравнивает дерево целиком; цвет flash
добавить в `events`.

**И8. Ветки, которых нет ни в одной трассе.** Нет zen, reversed, крышки-пончика (`capsCollected = 10` в green-study – это шины, крышек 0),
1-PPS, бонуса unbroken, существующего рекорда (обе трассы стартуют с пустых records, поэтому всегда идёт путь «новый рекорд»), песочницы
`TOTAL_LAPS = 0` (`lapScores.shift`), стойла `NO PROGRESS!`, round-стола в гонке. Построчное сравнение кода выше расхождений там не нашло,
но гейт 1e-6 эти ветки не держит; что из них покрывает `RaceRulesTests` – **не проверено**. → Ещё 3 трассы харнеса: green-study
zen+reversed; овал с laps = 0 и скриптовым пончиком вокруг крышки; второй заезд на том же сейве (рекорд не новый, first-clear не повторяется).

**И9. Паритет трасс молча игнорируется.** TrackBuilderParityTests.cs:21: без `DESKTOPDRIFT_DIR` тест делает `Assert.Ignore`, и
`scripts/test.sh` остаётся зелёным без проверки 12/12. → `Assert.Fail`, кроме явного `ALLOW_NO_2D=1`, или положить SVG в `tools/tracks-ref/`.

### Заметки

- З1. `JsRound = floor(x+0.5)` ≠ `Math.round` при 0.49999999999999994 (1 вместо 0) и при нечётных ≥ 2^52 (CoreMath.cs:39–42). → `r = floor(x); x − r >= 0.5 ? r + 1 : r`.
- З2. `|| 1`: Collision.cs:151–152 и 186–187 проверяют только `== 0`, без NaN. NaN там недостижим, но HitConeAt/OffsetEdges/TireSeed проверяют NaN – разнобой. → Один хелпер `CoreMath.OrOne`.
- З3. В `RunStats` нет `instanceId/trackId/reversed/ddk/capsThisRun` (game-engine.js:286–295 ↔ RaceRules.cs:324–338). Сейчас их не читает ни одна проверка; будущая проверка молча получит 0.
- З4. Константы движка перепечатаны руками (RaceRules.cs:21–33, включая `NmBand = 42`), хотя их экспортируют engine.json/config.json (находка 4). → Тест «константы = JSON».
- З5. Дубли: `Strings()` ×3 (RaceRules, SaveState, AchievementSync), конвертация caps ×2, `StatNum`/`Num` ×3.
- З6. Гейт g4: OvalTrack.cs:23–28, 60–63, 119 – числа без построчного комментария-источника.
- З7. Контракты хоста не записаны в Core: (а) пауза – JS обновляет `last` в каждом кадре паузы, поэтому первый dt после паузы мал, хост должен сбрасывать свой dt; (б) `hourOfDay` – локальный час (харнес использует UTC); (в) `SaveState.NowMs` по умолчанию 0, в ledger попадёт `t = 0`. → Записать в шапку RaceRules и в SPEC-02.
- З8. `S.TotalLaps = _t.Laps != 0 ? …` (RaceRules.cs:80) смешивает «нет поля» и 0; JS `T.laps ?? opts.laps`. Таких трасс нет. → `TrackData.Laps: int?`.
- З9. `TrackBuilder.Num` – `TryParse` всей строки, а JS `parseFloat` берёт префикс (`"12px"`: JS 12, C# NaN). Figma так не пишет.
- З10. `SaveSchema.Load` ловит только `FormatException` (SaveSchema.cs:100); любое другое исключение `Json.Parse` роняет загрузку, а в JS битый сейв даёт дефолты. → `catch (Exception)`.
- З11. `SaveSchema.Migrate`: версия вне диапазона int даёт from = 0 (SaveSchema.cs:86–88), JS берёт версию как есть. Миграций нет, вреда нет.

## 3. Инварианты (сырые счёты, zsh)

- g1 `UnityEngine|DateTime|System.Random|new Random|float |Math.Round(` в Core: **2** – TrackBuilder.cs:142 (`Math.Round` AwayFromZero, принят в находке 6, но см. И4) и комментарий SaveState.cs:25 (`DateTimeOffset` для слоя Unity). Часов и RNG в Core нет.
- g2 `Within(` без 1e-6/1e-9/1e-5/1e-12: **27** – все `Within(Eps)`/`Within(Tol)`, где константы 1e-5/1e-6/1e-9; слабых допусков нет.
- g3 `Assert.Ignore|[Ignore|Inconclusive`: **1** – TrackBuilderParityTests.cs:21 (→ И9).
- g4 числа ≥ 3 цифр без комментария-источника: **50** строк; по сути только OvalTrack.cs (З6), остальное – комментарии, таблицы лестниц и `minSpacing = 160`/`speed / 160` из дословно портированных файлов.

## 4. Не проверено

- Поведение `"R"`/`"F2"` на рантайме Unity/IL2CPP (И5) – вывод по известной семантике BCL .NET Framework, без прогона.
- `RaceRulesTests.cs` и остальные юнит-тесты построчно не сверялись (И8). `resolveKey` в JS не открывался: паритет 12/12 его покрывает.
