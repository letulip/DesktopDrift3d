# Установка Unity для DesktopDrift3d (Волна 0, задача владельца)

> Выполнено 27.10.2026: `6000.3.27f1` с SDK (android-34…37), NDK, OpenJDK. Ниже – как это делалось, на случай переустановки.

Мак: Apple M4 (arm64), macOS 26, на 27.10.2026 свободно 72 ГБ. Нужно около 15 ГБ.

## Что ставим

| Что | Версия | Зачем |
|---|---|---|
| Unity Hub | текущая, сборка для Apple silicon | менеджер версий редактора и модулей, лицензия |
| Unity Editor | **Unity 6.3 LTS (6000.3.x)** – в Hub помечена как LTS / Recommended в линейке Unity 6 | сам движок; alpha/beta/6.4+ не брать |
| Модуль Android Build Support | идёт с редактором | сборка APK/AAB |
| └ OpenJDK | ставится как подмодуль (JDK 17 для Unity 6) | Gradle-сборка Android |
| └ Android SDK & NDK Tools | подмодуль (NDK r27c для Unity 6) | компиляция IL2CPP под ARM64 |
| (по желанию) Mac Build Support (IL2CPP) | подмодуль | запасная сборка `build.sh mac`, пока нет Android |
| (позже, для iOS) iOS Build Support | подмодуль | SPEC-09 приложение A |

Лицензия: **Unity Personal**, бесплатная (доход и финансирование до $200k в год), выдаётся в Hub
после входа в Unity ID. Сплэш Unity на Unity 6 Personal отключаем.

## Где

1. Unity Hub: https://unity.com/download → «Download for Mac» (Apple silicon). Открыть `.dmg`,
   перетащить Hub в Applications, запустить.
2. Войти в Unity ID (или создать). Hub сам предложит активировать Personal-лицензию:
   Preferences → Licenses → Add license → Get a free personal license.
3. Installs → Install Editor → вкладка Official releases → выбрать **Unity 6.3 LTS** (6000.3.x) →
   Install.
4. В окне «Add modules» отметить:
   - ✅ **Android Build Support** – раскрыть стрелку и убедиться, что отмечены
     ✅ **OpenJDK** и ✅ **Android SDK & NDK Tools**;
   - ✅ Mac Build Support (IL2CPP) – по желанию;
   - iOS Build Support – не сейчас.
   Continue → принять лицензионные соглашения Android SDK/NDK → Install. Это 20–40 минут.
5. Путь установки оставить по умолчанию: `/Applications/Unity/Hub/Editor/6000.3.x/Unity.app` –
   на него рассчитаны скрипты SPEC-00.
6. Проверка: в терминале
   ```bash
   ls /Applications/Unity/Hub/Editor
   ```
   должна показать папку `6000.3.…`. Больше ничего открывать не нужно, редактор агенты запускают из CLI.

Официальные страницы: установка Android-зависимостей через Hub –
https://docs.unity3d.com/Manual/android-install-dependencies.html ; таблица версий JDK/SDK/NDK для
Unity 6 – https://docs.unity3d.com/Manual/android-supported-dependency-versions.html.

## Телефоны (Poco X6, Motorola G8 Plus)

- Настройки → О телефоне → 7 раз нажать «Номер сборки» → Для разработчиков → включить
  «Отладка по USB» (и «Установка через USB» на Xiaomi/Poco).
- `adb` приходит вместе с модулем SDK:
  `/Applications/Unity/Hub/Editor/<версия>/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb`;
  первая команда `adb devices` покажет на телефоне запрос доверия – разрешить.

## Unity CLI (установлен 27.10, 1.0.0-beta.13)

Официальный CLI Unity: `~/.unity/bin/unity`, в PATH через строку `. ~/.unity/env` в `~/.zshrc`.
Проверка: `unity doctor` (видит редактор 6000.3.27f1), `unity version`. Команды, на которых стоит контур
SPEC-00: `unity test` (EditMode/PlayMode с NUnit-отчётом), `unity build` (цели, профили, `--execute-method`),
`unity run` (batch-запуск редактора с пробросом флагов после `--`), `unity recompile` (живой редактор),
`unity command`/`unity list` (команды Pipeline-пакета в живом редакторе), `unity mcp` (официальный
MCP-сервер редактора), `unity open/close/status`. Вход в аккаунт (`unity auth login`) для этого не нужен.
Обновление `unity self-update` – только по решению архитектора (бета). Навык для агентов:
`unity skill show`; плагин Unity в Claude Code: `unity setup claude` – по желанию владельца.

## Что может спросить при первой сборке

Unity один раз просит принять лицензию Android SDK (окно в редакторе или
`Preferences → External Tools`). Если сборка из CLI упадёт с текстом про «SDK licenses», это оно:
открыть редактор один раз, принять, закрыть. Агент скажет, когда это понадобится.
