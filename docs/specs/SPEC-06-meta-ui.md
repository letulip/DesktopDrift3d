# SPEC-06 – Мета-UI на UI Toolkit: дизайн-система, 8 экранов, HUD, оверлеи, тосты

> Самая недооцениваемая часть порта: 1 476 строк экранов + 1 937 строк CSS + 13 HTML 2D-игры
> становятся UXML/USS. Вординг – дословно из 2D (закон владельца: менять только по его слову).
> Дизайн – `DesktopDrift/DESIGN.md` и токены `css/base.css`.

## Цель

Каждый экран 2D-игры имеет 3D-двойника с теми же строками и логикой: меню, выбор трасс, Zen-выбор,
гараж, модификация, настройки, ачивки, «о игре» (вместо donate), плюс HUD, пауза, подтверждение
выхода, результат, тосты ачивок, история кошелька. Ориентация по D-19 (портрет – рабочая гипотеза, решение на гейте SPEC-02), safe-area, кнопка «назад» Android.

## Вход

SPEC-02 (HUD-заглушка, сцена), SPEC-04 (поворотник), SPEC-07 (данные: сейв, магазин, ачивки, рекорды –
интерфейсы; если SPEC-07 ещё не закрыт, экраны работают на `InMemoryStore`). Источники: `DESIGN.md`,
`css/base.css` (`:root`), `css/{menu,tracks,select,settings,achievements,wallet,ach-toast,sandbox}.css`,
`js/screens/*.js`, `*.html` (разметка и строки), `js/{pause,confirm-exit,race-results,ach-toast,wallet-history}.js`.

## Шаги и диспетчеризация

| № | Шаг | Диспетчеризация | Выход |
|---|---|---|---|
| 1 | Токены и шрифт | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | `UI/USS/tokens.uss` (из `:root`), `UI/Fonts/Unbounded-800.ttf` + Font Asset, тема |
| 2 | `ScreenRouter`, переходы, back | ⟨Builder · general-purpose · sonnet · high · ≤50⟩ | `Game/UI/ScreenRouter.cs`, стек, фейд, Android back, safe-area |
| 3 | Таблица строк | ⟨Importer · general-purpose · haiku · medium · ≤30⟩ | `Game/UI/Strings.cs` – все пользовательские строки 2D (grep HTML/JS), с источником |
| 4 | Меню | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | `UI/Menu.uxml/.uss`, плитки, статистика, кошелёк, ⚙ |
| 5 | Выбор трасс и Zen | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | карточки с превью (SPEC-03), рекорд «Score: … PPS (Total: …)», бейджи крышек/шин, гейт 3★ на реверс |
| 6 | Гараж | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | карусель над поворотником, полосы spd/hdl/acc, цена/покупка/владение, «Race!/Start Zen» |
| 7 | Модификация | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | цвета (20 + hex), финиши, следы, неон (цвет, solid/longitudinal), корзина, покупка |
| 8 | Настройки | ⟨Builder · general-purpose · sonnet · medium · ≤40⟩ | единицы, хаптика, звук, громкость, качество; экспорт/импорт сейва – 1.1 (D-17) |
| 9 | Ачивки и тосты | ⟨Builder · general-purpose · sonnet · medium · ≤50⟩ | страница с прогресс-барами, скрытые, тост как `ach-toast.js` |
| 10 | HUD финальный, пауза, выход, результат, история кошелька | ⟨Builder · general-purpose · sonnet · high · ≤60⟩ | замена заглушки SPEC-02 |
| 11 | «О игре» | ⟨Builder · general-purpose · haiku · low · ≤20⟩ | credits, ссылки на веб-игру и devlog; без donate (политики сторов) |
| 12 | Ревью на телефоне | ⟨Reviewer · general-purpose · opus · high · ≤40⟩ + владелец | `artifacts/ui-review.md`: размеры касаний, safe-area, читаемость |

### 1 · Токены

Каждая переменная `:root` из `base.css` → USS custom property с тем же именем (`--accent` и т. д.);
радиусы, тени, плёночное зерно (фон-текстура с низкой альфой), шрифт Unbounded 800 (OFL, конвертировать
woff2 → ttf скриптом `fonttools` или взять ttf из Google Fonts). Тёмная тема одна (как в 2D).

### 2 · Роутер

`ScreenRouter.Push/Pop/Replace` по именам экранов, стек, анимация появления (staggered как в меню 2D),
Android «назад» = `Pop` (на меню – сворачивание), `UIDocument` один, экраны – `VisualTreeAsset`. Safe-area
через `Screen.safeArea` → padding корня. Размер касания ≥ 48 dp.

### 3 · Строки

`grep -hoE ">[^<>{}]{2,}<|'[A-Z][^']{2,}'" *.html js/screens/*.js js/*.js` → ручной отбор (L1 с
проверкой списка архитектором): названия плиток, кнопок, подсказок, строк результата, ачивок (они уже в
JSON), настроек. Один файл `Strings.cs`, ключи по экранам, комментарий с источником.

### 4–11 · Экраны

Логика каждого экрана – порт соответствующего `js/screens/*.js` поверх интерфейсов SPEC-07:
`ISaveStore`, `IShop`, `IAchievements`, `IRecords`. Особенности: меню показывает средний PPS по
рекордам TA и число крышек; выбор трасс – `records[id].timeattack`, бейдж `N / M cap`, гейт реверса
по 3★ прямой; гараж – ownership-гейт выключен (`CAR_GATING_ENABLED` false, как в 2D), покупка машины
через `buyCar`; модификация – покупки account-wide, look per-car; настройки – `units`, `haptics`,
`soundEnabled`, `volume` (уровни low/med/high), ступень качества (новое, из SPEC-05); экспорт/импорт
сейва – 1.1 (D-17); результат – как `race-results.js` (score, bestLap, lapScores, pps, звёзды, NEW RECORD, выплата шин,
ачивки); история кошелька – `ledger` новейшие сверху.

### 12 · Ревью

На телефоне: все экраны, ориентация по D-19 (портрет – рабочая гипотеза, гейт SPEC-02), касания, safe-area на «чёлке»,
читаемость на светлых темах трасс (в 2D был баг `#hint` на светлых темах – проверить тень текста).

## Артефакты

`UI/**` (uxml/uss/шрифт), `Game/UI/**`, `Strings.cs`, `artifacts/ui-review.md`, кадры каждого экрана
в `artifacts/ui/`.

## Вне объёма

Share-card (1.1, O-02), настроения/тинты/обводки в магазине (1.1), локализация (строки собраны в
одну таблицу – этого достаточно), donate.

## Acceptance-критерии

- Таблица соответствия «экран 2D → экран 3D → строки» заполнена, нет строк «от себя».
- Кадры всех экранов в `artifacts/ui/` (desktop + 1080×2400 с safe-area).
- Навигация без тупиков (Explorer-проход по графу экранов), Android back везде.
- Владелец подписал ревью.

## Гейт архитектора

Прохожу граф экранов по кадрам; сверяю 10 случайных строк с 2D; проверяю, что экраны не держат
состояние (всё из `ISaveStore`), что `Time.timeScale` не используется, что HUD `pickingMode: Ignore`.

## Риски

- UI Toolkit runtime на Android: производительность при `RenderTexture` поворотника – ограничить 30 fps
  гаража на мобильной ступени.
- Шрифт Unbounded: конвертация и лицензия OFL – соблюсти атрибуцию в credits.
- Соблазн «улучшить» вординг – запрещено; изменения только по слову владельца.

## Связи

D-13, O-02; SPEC-02, SPEC-03 (превью), SPEC-04 (поворотник), SPEC-07 (интерфейсы данных), `DESIGN.md`.

## Находки гейта

(пусто)
