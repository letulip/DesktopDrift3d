# SPEC-09 – Android: сборка AAB, политики Play, проверка аккаунта, закрытый тест, матрица устройств; приложение по iOS

> Решения D-02, D-10, D-11. Здесь нет «фич» – здесь всё, что отделяет работающую игру от игры в
> сторе. Самое длинное по календарю – 14 дней закрытого теста, поэтому план набора тестеров
> стартует ДО закрытия SPEC-06/07.

## Цель

Подписанный AAB проходит проверки Play Console; аккаунт готов к публикации; закрытый тест (если
требуется) идёт с 12+ тестерами; на 2–3 устройствах пройден чек-лист; политики и анкеты заполнены.

## Вход

Все этапы до SPEC-08 закрыты (для финального AAB); для подготовки аккаунта и тестеров – достаточно
играбельной сборки SPEC-02+. Владелец: аккаунт Play, keystore, телефоны.

## Шаги и диспетчеризация

| № | Шаг | Диспетчеризация | Выход |
|---|---|---|---|
| 1 | Настройки сборки релиза | ⟨Builder · general-purpose · sonnet · high · ≤50⟩ | package name, версия из `version.json`, IL2CPP ARM64, target/min API, landscape, 16 KB, стриппинг, иконка |
| 2 | Проверка аккаунта Play | владелец (по инструкции агента) | дата создания аккаунта; вывод: гейт закрытого теста да/нет |
| 3 | Keystore и подпись | ⟨Builder · general-purpose · sonnet · medium · ≤30⟩ + владелец | `scripts/make-keystore.sh` (запускает владелец, пароль у него), `build.sh aab` читает `DD_KEYSTORE_*` из окружения |
| 4 | Подписанный AAB и отчёт | ⟨Gate-runner · general-purpose · sonnet · medium · ≤40⟩ | `artifacts/build/android/DesktopDrift.aab`, размер, `bundletool validate` (если доступен) |
| 5 | Пакет политик | ⟨Promoter · general-purpose · sonnet · medium · ≤40⟩ | ответы Data safety («данные не собираются»), анкета возрастного рейтинга, страница privacy policy (на GitHub Pages 2D-игры – текст готовит агент, публикует владелец), target audience |
| 6 | План закрытого теста и набор тестеров | ⟨Promoter · general-purpose · sonnet · medium · ≤40⟩ + владелец | `docs/release/closed-test-plan.md`: каналы, тексты, Google Group, календарь 14 дней, журнал фидбэка, ответы на вопросы Google |
| 7 | Матрица устройств | владелец + ⟨Gate-runner · general-purpose · sonnet · medium · ≤40⟩ | `docs/release/device-checklist.md` заполнен по 2–3 устройствам |
| 8 | Гигиена логов | ⟨Builder · general-purpose · haiku · low · ≤20⟩ | `scripts/logcat.sh` (фильтр Unity), без аналитических SDK (D-10) |
| 9 | Приложение A: iOS | после Android 1.0; ⟨Builder · general-purpose · sonnet · high · ≤60⟩ + владелец | модуль iOS, Xcode-проект из CLI, подпись, TestFlight, privacy manifest |

### 1 · Релизные настройки

`version.json` `{ "version": "1.0.0", "code": 1 }` → `PlayerSettings.bundleVersion`/`versionCode`
(код растёт на каждую сборку скриптом). Package name – владелец (предложение `com.letulip.desktopdrift`,
менять потом нельзя). `targetSdkVersion` = требование Play на дату (на 2026 – API 35+; проверить
страницу Play при сборке), `minSdk 26` (O-05), только ARM64 (меньше размер; x86 не нужен), `Managed
Stripping High` + `link.xml` для Newtonsoft, `Strip Engine Code`, сжатие LZ4, landscape (обе ориентации
landscape), иконки адаптивные (из `icons/` 2D-игры), splash своя (Unity-сплэш выключен на Unity 6 Personal).
16 KB page size – флаг Unity 6 включён по умолчанию, проверить в отчёте сборки.

### 2 · Аккаунт Play (владелец)

Play Console → Account details: дата создания. Создан до 13.11.2023 → гейта нет, шаг 6 сокращается до
«внутреннее тестирование по желанию». Позже → гейт: 12 тестеров, 14 дней подряд, затем заявка на
продакшн с ответами о наборе и фидбэке. Регистрация $25 (разово), верификация личности – по
требованию консоли. Агент даёт пошаговую инструкцию с картинками-не-нужны, владелец отвечает одной строкой.

### 3 · Keystore

`make-keystore.sh` вызывает `keytool` с параметрами (alias `desktopdrift`, RSA 2048, 10000 дней), пароль
вводит владелец; файл хранится ВНЕ репозитория (`~/.desktopdrift/release.keystore`), путь/алиас/пароли –
переменные окружения `DD_KEYSTORE_PATH/ALIAS/PASS/KEYPASS`, которые читает `Build.Aab()`. Потеря
keystore = невозможность обновлять приложение: агент напоминает о бэкапе (владелец решает где).
Альтернатива – Play App Signing (Google хранит ключ приложения, у владельца upload key) – рекомендуется.

### 6 · Закрытый тест и тестеры (худший случай, D-11)

Каналы, в порядке ожидаемой отдачи: (1) семья/друзья/одноклассники сына с Android; (2) подписчики
devlog YouTube и страницы itch/Newgrounds/Game Jolt – пост «Help test the 3D edition on Android» с
opt-in ссылкой (тексты готовит Promoter по образцу `docs/promo`); (3) сообщества взаимного тестирования
(r/AndroidClosedTesting и аналоги – взаимность: владелец тестирует чужие); цель 15 опт-инов для запаса.
Механика: Google Group или список e-mail в Play Console, трек «Closed testing», сборка с tuning-оверлеем
выключенным; журнал фидбэка (`docs/release/feedback-log.md`) – Google спрашивает, что получили и что
изменили. Параллельно APK кладётся на itch.io (Android-билды там разрешены) – фидбэк без гейта.
Календарь: старт теста сразу после SPEC-07 (экономика и сейв стабильны), 14 дней идут, пока закрываются
SPEC-06-полировка и SPEC-08.

### 7 · Устройства

Poco X6 (контрольный) + Motorola G8 Plus (слабый, Snapdragon 665, 2019) + ещё более слабый из имеющихся как
третий (D-16): чек-лист: установка,
первый запуск < 5 с, 60 fps на 3 трассах, 15 минут термального теста, сворачивание/возврат (сейв и звук),
входящий звонок (аудио-фокус), кнопка «назад», тач-половины, хаптика, память
(`adb shell dumpsys meminfo`), ANR нет.

### 9 · iOS (приложение A, после Android 1.0)

Модуль iOS Build Support, `Build.Ios()` → Xcode-проект → `xcodebuild` архив из CLI с автоматической
подписью (Apple Developer Program $99/год, владелец), privacy manifest (`PrivacyInfo.xcprivacy` – нет
сбора данных), TestFlight внутреннее тестирование, App Store review; ориентация, safe-area, haptics
через `UIImpactFeedbackGenerator` (плагин ≤ 60 строк). Оценка +1–2 недели.

## Артефакты

`version.json`, `scripts/{make-keystore,logcat}.sh`, `Build.Aab/Ios`, `docs/release/{closed-test-plan,device-checklist,feedback-log,policies}.md`,
текст privacy policy, AAB в `artifacts/`.

## Вне объёма

Листинг, цена, скриншоты (SPEC-10); реклама/IAP/аналитика (D-10); веб-сборка Unity (после 1.0).

## Acceptance-критерии

- AAB подписан, загружается в Play Console без ошибок, pre-launch report без крашей.
- Анкеты заполнены, privacy policy опубликована, рейтинг получен.
- Закрытый тест: ≥ 12 активных тестеров 14 дней (или гейта нет – зафиксировано в `01-decisions.md`).
- Чек-лист устройств заполнен без красных пунктов.

## Гейт архитектора

Проверяю отчёт сборки (размер, стриппинг, 16 KB, ARM64), что keystore и пароли не в репозитории
(`git log -p | grep -i keystore` пусто), что аналитических SDK нет (`manifest.json`, `Plugins/`),
журнал фидбэка и ответы на вопросы Google готовы.

## Риски

- Тестеров не набрать – itch.io-APK как запасной канал фидбэка, но гейт Play это не снимает; тогда
  Steam/itch как первый магазин, Play – позже (решение владельца).
- Требование target API поднимается ежегодно – проверять на дату сборки.
- Потеря keystore – Play App Signing рекомендован именно поэтому.

## Связи

D-02, D-10, D-11, O-05; SPEC-10; `00-analysis.md` §8 риск 3.

## Находки гейта

(пусто)
