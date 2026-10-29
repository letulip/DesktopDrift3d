# DesktopDrift3d – обязательный контекст сессии

Держи bootstrap коротким. Сначала [docs/CURRENT.md](docs/CURRENT.md) (где проект сейчас),
потом **одна** спека из `docs/specs/`, которую тебе назвали. Не обходи `docs/` целиком.

## Что это

Порт живой 2D-игры Desktop Drift (`/Users/letulip/Projects/Claude/DesktopDrift`, только чтение)
на Unity 6, low-poly 3D, 1.0 native-first (AAB Google Play), платная, без рекламы.
Карта пакета – [README.md](README.md); архитектура – [docs/03-architecture.md](docs/03-architecture.md);
процесс и роли – [docs/02-process.md](docs/02-process.md); решения – [docs/01-decisions.md](docs/01-decisions.md).

## Неразрушаемые инварианты

- **Паритет с 2D – гейт, а не пожелание.** `stepCar`, коллизии, скоринг, экономика, ачивки,
  миграции сейва переносятся дословно; порядок операций в физике не менять; трасса паритета
  (`tools/parity-trace.mjs` ↔ NUnit) сходится с допуском 1e-6 на кадр.
- **Core не знает про Unity.** В `unity/Assets/Core/` нет ссылок на `UnityEngine`
  (`noEngineReferences: true`); всё там тестируется `dotnet test` за секунды.
- **Единицы.** 1 игровая единица = 1 мм; Core считает в игровых единицах и `double`, как JS;
  переводит в метры только слой представления (`UnitsToMeters = 0.001`).
- **Детерминизм.** Физика в `FixedUpdate` при 60 Гц с `dt = 1/60`; рендер интерполирует.
  Никакого `Random` и `DateTime.Now` в Core.
- **Данные не перепечатываются.** Таблицы (машины, предметы, трассы, магазин, ачивки, палитры)
  экспортируются из JS-модулей скриптом в JSON и импортируются в ScriptableObject.
- **Строки интерфейса – те же, что в 2D-игре.** Менять вординг может только владелец.

## Законы (нарушение = откат)

1. **Никогда не пушить и не коммитить в `main`.** Ветка этапа `feat/spec-NN-<slug>`, PR
   открывает и мержит владелец. Перед каждым коммитом – `git branch --show-current`.
   **Одна ветка на волну, полный цикл в ней** (D-24, `docs/02-process.md` §6): работа → гейт архитектора →
   плейтест владельца → правки → PR → мерж; вторую ветку на ту же волну не заводить.
2. **Код, комментарии, строки – по-английски.** Документы пакета – по-русски.
3. **Минимальный дифф, ровно по спеке.** Расширение объёма – только через вопрос архитектору.
4. **Токен-дисциплина** ([02-process.md](docs/02-process.md) §3–§5): модель и усилие выбраны
   на шаг, агенты последовательно и тонкие, бюджет ходов – стоп-условие, гейты в родителе.
5. **Роли не смешивать:** Builder реализует спеку, Reviewer делает отдельное ревью, архитектор
   задаёт и принимает гейты, владелец подписывает фил и внешние шаги.
6. **Перед каждым коммитом:** `scripts/test.sh` зелёный (после SPEC-00 – плюс
   `scripts/unity-tests.sh` для Unity-слоя); новая чистая логика – с тестом в том же коммите.

## Команды (контур SPEC-00)

```bash
scripts/test.sh          # dotnet test по Core + Core.Tests, секунды (TEST_EXIT)
scripts/trace.sh         # трасса паритета из JS → tools/parity-trace.json (TRACE_EXIT)
scripts/compile.sh       # живой редактор → unity recompile, иначе unity run + лог (COMPILE_EXIT)
scripts/unity-tests.sh   # Unity EditMode-тесты через unity test, NUnit+JUnit в artifacts/ (UNITY_TESTS_EXIT); UNITY_TESTS_MODE=PlayMode – сценные тесты
scripts/build.sh android # APK через unity build --profile Android + artifacts/build/report.md (BUILD_EXIT); mac – настольная сборка
scripts/capture.sh       # кадр из play mode через Pipeline → artifacts/capture/*.png (CAPTURE_EXIT); DD_STEER=trace CAPTURE_DELAY=12 – кадр заезда; DD_MODE=ta|zen|sandbox DD_TRACK=<id> DD_REVERSED=1 DD_STEER=auto – любая трасса на ходу, напр. `DD_TRACK=dining-oak DD_STEER=auto CAPTURE_DELAY=12 scripts/capture.sh`
node tools/mcp-smoke.mjs # одна операция через официальный MCP редактора (MCP_EXIT)
```

Скрипты – тонкие обёртки над официальным **Unity CLI** 1.0.0-beta.13 (`~/.unity/bin/unity`; в скриптах
`source ~/.unity/env`): `unity test`, `unity build`, `unity run`, `unity recompile`, `unity command`,
`unity mcp`. Первая диагностика – `unity doctor`, затем `unity status --project-path "$PWD/unity"`: один
редактор на проект, `unity run`/`test`/`build` при открытом редакторе не запускать. Карта скрипт ↔ команда ↔
запасной путь – [docs/tooling/README.md](docs/tooling/README.md); редактор (GUI или headless-резидент) и MCP – [docs/tooling/unity-mcp.md](docs/tooling/unity-mcp.md).
Большие логи Unity не читать целиком: `grep -n "error CS\|Exception" … | head -40`.
