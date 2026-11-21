# Nariman: старт ядра игры (N2–N9)

Скрипты уже лежат в `Assets/_Game/Scripts/`. Их писали без Unity под рукой, поэтому первая задача — открыть проект и исправить ошибки компиляции, если они будут.

## 1. Проект Unity (N2)

1. Установить **Unity Hub** и последнюю **Unity 6 LTS** с модулем **Windows Build Support**. Запиши точную версию в чат команды: все ставят её же.
2. Склонировать репозиторий и переключиться на ветку с заготовкой:
   ```bash
   git clone https://github.com/bayesdz/Gjj.git
   ```
   ```bash
   cd Gjj && git checkout claude/vibrant-edison-tsjz3h
   ```
3. Unity Hub не создаёт проект в непустой папке, поэтому:
   - Hub → **New project** → шаблон **Universal 3D** → имя `FalseParadise` → **во временной папке**.
   - Закрыть Unity.
   - Скопировать из временного проекта в корень репозитория папки `Packages/` и `ProjectSettings/`, а также содержимое `Assets/` (кроме `Readme.asset` и `TutorialInfo/`). Папку `Assets/_Game/` из репозитория не трогать.
   - Hub → **Add → Add project from disk** → выбрать корень репозитория.
4. Открыть проект и проверить:
   - **Window → Package Manager**: установлены **Input System** и **AI Navigation** (если нет, поставить).
   - **Edit → Project Settings → Player → Active Input Handling** = `Input System Package (New)` или `Both`.
   - **Edit → Project Settings → Editor**: Asset Serialization = `Force Text` (по умолчанию так и есть).
5. Если в Console есть ошибки компиляции, исправить их или скинуть мне текст ошибок.
6. Закоммитить `Assets/`, `Packages/`, `ProjectSettings/` вместе со всеми `.meta` файлами.

## 2. Песочница `Assets/_Game/Scenes/Sandbox_Nariman.unity`

Пока используем примитивы. Модели KayKit от Бахи подставим позже.

**Пол и стены**
- Plane (масштаб 3, 1, 3) и несколько Cube как стены и колонны.
- Если хочешь, чтобы враги обходили стены: на Plane добавить **NavMeshSurface** → **Bake**.

**Игрок**
- Пустой объект `Player`, **Tag = Player**, позиция (0, 1, 0).
- Компоненты:
  - `CharacterController` (Height 1.8, Radius 0.35);
  - `FirstPersonController`;
  - `Health` (Max 100);
  - `PlayerMelee`;
  - `PlayerInteractor`;
  - `EyeOfTruth`;
  - `PlayerDeath`.
- Дочерний `CameraPivot` в локальной позиции (0, 0.7, 0). Внутрь него перенести **Main Camera** с локальной позицией (0, 0, 0) и добавить на неё `CameraShake`.
- В `FirstPersonController` поле `cameraPivot` = `CameraPivot`.

**HUD**
- Пустой объект `HUD` + `GameHUD`. Ссылки найдутся сами по тегу Player.

**Гоблин**
- Пустой объект `Goblin_01` с компонентами:
  - `CapsuleCollider` (Center Y 1, Height 2);
  - `Health` (Max 100);
  - `EnemyController` (Enemy Id `goblin_01`);
  - `HitFlash`;
  - `IllusionVisual`;
  - `NavMeshAgent` — только если запёк NavMesh. Без него враг идёт к игроку напрямую.
- Дочерний `Illusion`: Capsule (позиция Y 1), **удалить его коллайдер**, материал тёмно-красный.
- Дочерний `Truth`: Capsule (позиция Y 1), **удалить коллайдер**, материал тёплый синий.
- В `IllusionVisual`: Illusion Model = `Illusion`, Truth Model = `Truth`.

**Огр-вождь**
- Дубликат гоблина, увеличенный до 1.6. Enemy Id = `ogre_chief`, Health 200.

**Предмет**
- Маленький Cube с коллайдером + `InspectItem` (Item Id `floor1_toy`).

## 3. Чек-лист проверки

| Задача | Проверка | ✓ |
|---|---|---|
| N3 | WASD, мышь, Shift, Space работают. Esc отпускает курсор, клик возвращает | |
| N4 | ЛКМ по гоблину: тряска камеры, белая вспышка, короткая заморозка | |
| N4 | Гоблин бьёт игрока: красная вспышка, здоровье падает, смерть → перезапуск | |
| N5 | При 30% здоровья гоблин перестаёт атаковать, появляется «[E] Пощадить [ЛКМ] Добить» | |
| N5 | E → в отладке (F1) `EnemySpared(goblin_01)`, милосердие +1 | |
| N5 | ЛКМ по стоящему на коленях → `EnemyKilled(goblin_01)` | |
| N6 | Пощада огра даёт +3 и `Огр-вождь пощажён: True`. Повторная пощада не считается | |
| N7 | Q → гоблин синий на 5 секунд, потом перезарядка 8 секунд | |
| N7 | При ударах гоблин иногда на долю секунды становится синим (глитч) | |
| N8 | E на кубе → окно с текстом, без Ока и с Оком текст разный, +1 милосердие один раз | |

## 4. Контрольная точка 28 ноября (N9)

1. **File → Build Profiles → Windows**, добавить `Sandbox_Nariman` в список сцен.
2. **Build** в папку `Builds/` (она в `.gitignore`).
3. Заархивировать **всю папку** билда, а не только `.exe`.
4. Запустить на чужом компьютере с Windows, где нет Unity, и пройти чек-лист ещё раз.

## Что дальше

- **Подключение моделей KayKit.** Модель кладётся внутрь `Illusion` или `Truth`. У её Animator Controller параметры: `Speed` (float), `Attack` и `Hit` (trigger), `Kneeling`, `Dead`, `Spared` (bool). Отсутствующие параметры скрипт пропускает.
- **Связка с Абу.** Враги и предметы вызывают `GameEvents.RaiseNarrationRequest(ключ)`, его модуль подписывается на это событие. Для этого есть поля `narrationKeyOnSeen` у врага и `narrationKey` у предмета.
- **N10.** Переход «рай → ад» делаем вместе с Бахой.
- **N11.** `SceneFlow` и чекпоинты.
