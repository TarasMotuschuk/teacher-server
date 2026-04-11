using Teacher.Common.Localization;

namespace TeacherClient.CrossPlatform.Localization;

internal static partial class CrossPlatformText
{
    public static UiLanguage CurrentLanguage { get; private set; } = UiLanguageExtensions.GetDefault();

    public static bool IsUk => CurrentLanguage == UiLanguage.Ukrainian;

    public static string MainTitle => "ClassCommander";

    public static string ConnectionMenu => IsUk ? "Підключення" : "Connection";

    public static string Settings => IsUk ? "Налаштування" : "Settings";

    public static string RefreshAgents => IsUk ? "Оновити список ПК" : "Refresh PC List";

    public static string ConnectSelectedAgent => IsUk ? "Підключити вибраний ПК" : "Connect Selected PC";

    public static string AddManualAgent => IsUk ? "Додати вручну" : "Add Manual Agent";

    public static string EditManualAgent => IsUk ? "Редагувати вручну" : "Edit Manual Agent";

    public static string RemoveManualAgent => IsUk ? "Видалити вручну" : "Remove Manual Agent";

    public static string Help => IsUk ? "Довідка" : "Help";

    public static string GroupCommands => IsUk ? "Групові команди" : "Group Commands";

    public static string GroupCommandsTitle => IsUk ? "Групові команди" : "Group Commands";

    public static string ProgramUpdatesMenu => IsUk ? "Оновлення програми" : "Program Updates";

    public static string RemoteManagementTab => IsUk ? "Віддалене керування" : "Remote management";

    public static string RemoteManagementTitle => IsUk ? "Віддалене керування" : "Remote management";

    public static string RemoteManagementHint => IsUk ? "Подвійний клік на плитці відкриває повноекранний перегляд. Кнопки нижче запускають або зупиняють VNC, а керування вмикається вже у вікні перегляду." : "Double-click a tile to open the fullscreen viewer. The buttons below start or stop VNC, and control is enabled inside the viewer window.";

    public static string RefreshRemoteManagement => IsUk ? "Оновити екрани" : "Refresh screens";

    public static string StartVncViewOnly => IsUk ? "Запустити VNC" : "Start VNC";

    public static string StartVncControl => IsUk ? "Запустити VNC (керування)" : "Start VNC (control)";

    public static string StopVnc => IsUk ? "Зупинити VNC" : "Stop VNC";

    public static string OpenFullscreenViewer => IsUk ? "Відкрити на весь екран" : "Open fullscreen viewer";

    public static string EnableFullscreenControl => IsUk ? "Увімкнути керування" : "Enable control";

    /// <summary>Fullscreen VNC viewer: button label when remote control is already active (button stays visible for stable layout).</summary>
    public static string ViewerControlActiveButtonLabel => IsUk ? "Керування увімкнено" : "Control on";

    public static string SendKeyboardShortcut => IsUk ? "Надіслати комбінацію клавіш" : "Send keyboard shortcut";

    public static string RemoteManagementNoScreens => IsUk ? "Немає доступних екранів учнівських ПК." : "No student PC screens are available.";

    public static string RemoteManagementNoSelection => IsUk ? "Спочатку виберіть учнівський ПК на вкладці віддаленого керування." : "Choose a student PC on the remote management tab first.";

    public static string RemoteManagementSelectTitle => IsUk ? "Вибір ПК" : "Select PC";

    public static string RemoteManagementRequiresOnlineAgent => IsUk ? "Для цієї дії потрібен онлайн-учнівський ПК." : "The selected student PC must be online.";

    public static string BlockingCommandsMenu => IsUk ? "Блокування" : "Blocking";

    public static string LockInputForDemonstrationOnAllOnlineStudents => IsUk ? "Увімкнути блокування клавіатури і миші (демонстрація) на всіх онлайн учнівських ПК" : "Enable keyboard and mouse lock (demonstration) on all online student PCs";

    public static string WindowsRestrictionsMenu => IsUk ? "Обмеження Windows" : "Windows Restrictions";

    public static string CommandsMenu => IsUk ? "Команди" : "Commands";

    public static string DesktopIconsMenu => IsUk ? "Іконки робочого стола" : "Desktop Icons";

    public static string PowerCommandsMenu => IsUk ? "Живлення" : "Power";

    public static string SelectedStudentsMenu => IsUk ? "Вибрані ПК" : "Selected PCs";

    public static string AllOnlineStudentsMenu => IsUk ? "Всі онлайн ПК" : "All Online PCs";

    public static string StudentWorkMenu => IsUk ? "Роботи учнів" : "Student Work";

    public static string FrequentProgramsMenu => IsUk ? "Часті програми" : "Frequent Programs";

    public static string About => IsUk ? "Про програму" : "About";

    public static string StatusReady => IsUk ? "Готово. Виберіть машину на вкладці учнівських ПК і підключіться." : "Ready. Use the Student PCs tab to select a student machine, then connect.";

    public static string Agents => IsUk ? "Учнівські ПК" : "Student PCs";

    public static string Processes => IsUk ? "Процеси" : "Processes";

    public static string Files => IsUk ? "Файли" : "Files";

    public static string SearchAgents => IsUk ? "Пошук ПК" : "Search PCs";

    public static string AllGroups => IsUk ? "Усі групи" : "All groups";

    public static string All => IsUk ? "Усі" : "All";

    public static string Online => IsUk ? "Онлайн" : "Online";

    public static string Offline => IsUk ? "Офлайн" : "Offline";

    public static string Unknown => IsUk ? "Невідомо" : "Unknown";

    public static string AutoReconnect => IsUk ? "Автоперепідключення" : "Auto-reconnect";

    public static string Source => IsUk ? "Джерело" : "Source";

    public static string Status => IsUk ? "Статус" : "Status";

    public static string ProcessLabel => IsUk ? "Процес" : "Process";

    public static string BrowserLock => IsUk ? "Блок браузера" : "Browser lock";

    public static string InputLock => IsUk ? "Блок вводу" : "Input lock";

    public static string Group => IsUk ? "Група" : "Group";

    public static string Machine => IsUk ? "Машина" : "Machine";

    public static string User => IsUk ? "Користувач" : "User";

    public static string Notes => IsUk ? "Нотатки" : "Notes";

    public static string UpdateStatus => IsUk ? "Оновлення" : "Update";

    public static string UpdateStatusDetailColumn => IsUk ? "Деталі оновлення" : "Update details";

    public static string Version => IsUk ? "Версія" : "Version";

    public static string LastSeenUtc => IsUk ? "Останній сигнал UTC" : "Last Seen UTC";

    public static string Refresh => IsUk ? "Оновити" : "Refresh";

    public static string TerminateSelected => IsUk ? "Завершити вибране" : "Terminate Selected";

    public static string RefreshBoth => IsUk ? "Оновити обидві панелі" : "Refresh Both";

    public static string UploadArrow => IsUk ? "Завантажити ->" : "Upload ->";

    public static string DownloadArrow => IsUk ? "<- Скачати" : "<- Download";

    public static string OpenLocal => IsUk ? "Відкрити локально" : "Open Local";

    public static string OpenRemote => IsUk ? "Відкрити на учнівському ПК" : "Open on Student PC";

    public static string RenameLocal => IsUk ? "Перейменувати локально" : "Rename Local";

    public static string RenameRemote => IsUk ? "Перейменувати віддалено" : "Rename Remote";

    public static string DeleteLocal => IsUk ? "Видалити локально" : "Delete Local";

    public static string DeleteRemote => IsUk ? "Видалити віддалено" : "Delete Remote";

    public static string NewRemoteFolder => IsUk ? "Нова віддалена папка" : "New Remote Folder";

    public static string SendToSelectedStudents => IsUk ? "Надіслати вибраним учням" : "Send to selected students";

    public static string SendToAllOnlineStudents => IsUk ? "Надіслати всім онлайн учням" : "Send to all online students";

    public static string ClearDestinationFolderOnSelectedStudents => IsUk ? "Очистити папку призначення на вибраних учнях" : "Clear destination folder on selected students";

    public static string ClearDestinationFolderOnAllOnlineStudents => IsUk ? "Очистити папку призначення на всіх онлайн учнях" : "Clear destination folder on all online students";

    public static string DestinationFolderMenu => IsUk ? "Папка призначення" : "Destination Folder";

    public static string LockBrowsersOnAllOnlineStudents => IsUk ? "Заблокувати браузер на всіх онлайн учнівських ПК" : "Lock browser on all online student PCs";

    public static string LockInputOnAllOnlineStudents => IsUk ? "Заблокувати клавіатуру і мишу на всіх онлайн учнівських ПК" : "Lock keyboard and mouse on all online student PCs";

    public static string UnlockInputOnAllOnlineStudents => IsUk ? "Розблокувати клавіатуру і мишу на всіх онлайн учнівських ПК" : "Unlock keyboard and mouse on all online student PCs";

    public static string EnableCommand => IsUk ? "Увімкнути" : "Enable";

    public static string DisableCommand => IsUk ? "Вимкнути" : "Disable";

    public static string ShutdownCommand => IsUk ? "Вимкнути" : "Shut Down";

    public static string RestartCommand => IsUk ? "Перезавантажити" : "Restart";

    public static string LogOffCommand => IsUk ? "Вийти з облікового запису" : "Log Off";

    public static string CreateStudentWorkFolderOnAllAgents => IsUk ? "Створити папку для робіт на всіх ПК" : "Create work folder on all PCs";

    public static string CollectStudentWorkToTeacherPc => IsUk ? "Зібрати роботи учнів на вчительський ПК" : "Collect student work to teacher PC";

    public static string ClearStudentWorkFolderOnAllAgents => IsUk ? "Очистити папку для робіт на всіх ПК" : "Clear work folder on all PCs";

    public static string TeacherPc => IsUk ? "ПК викладача" : "Teacher PC";

    public static string StudentPc => IsUk ? "ПК студента" : "Student PC";

    public static string Up => IsUk ? "Вгору" : "Up";

    public static string UpWithArrow => IsUk ? "↑ Вгору" : "↑ Up";

    public static string DriveFreeSpaceUnknown => IsUk ? "Вільно: невідомо" : "Free: unknown";

    public static string Name => IsUk ? "Назва" : "Name";

    public static string Extension => IsUk ? "Розширення" : "Extension";

    public static string Attributes => IsUk ? "Атрибути" : "Attributes";

    public static string Size => IsUk ? "Розмір" : "Size";

    public static string ModifiedUtc => IsUk ? "Змінено UTC" : "Modified UTC";

    public static string SettingsWindowTitle => IsUk ? "Налаштування ClassCommander" : "ClassCommander Settings";

    public static string SharedSecret => IsUk ? "Спільний секрет" : "Shared secret";

    public static string BulkCopyDestinationPath => IsUk ? "Папка призначення на учнях" : "Student destination folder";

    public static string StudentWorkRootPath => IsUk ? "Базовий шлях робіт на учнях" : "Student work base path";

    public static string StudentWorkFolderName => IsUk ? "Назва папки робіт" : "Student work folder name";

    public static string DesktopIconAutoRestoreInterval => IsUk ? "Автовідновлення іконок (хв)" : "Desktop icon auto-restore (min)";

    public static string BrowserLockCheckInterval => IsUk ? "Перевірка блокування браузера (с)" : "Browser-lock check interval (s)";

    public static string Language => IsUk ? "Мова" : "Language";

    public static string SettingsHint => IsUk ? "Спільний секрет використовується для перевірки доступності агентів і для всіх API-запитів. Папка призначення визначає стартовий шлях на учнівських ПК для масового копіювання файлів і папок. Базовий шлях і назва папки робіт визначають спільний каталог, який буде створюватися на учнівських ПК для збереження робіт. Інтервали нижче зберігаються на ПК викладача і після збереження застосовуються до всіх онлайн учнівських ПК." : "The shared secret is used for reachability checks and all teacher-to-student API calls. The destination folder defines the starting path on student PCs for bulk file and folder distribution. The work base path and work folder name define the shared student folder that will be created on student PCs for saved work. The intervals below are stored on the teacher PC and are applied to all online student PCs after saving.";

    public static string Save => IsUk ? "Зберегти" : "Save";

    public static string Cancel => IsUk ? "Скасувати" : "Cancel";

    public static string Close => IsUk ? "Закрити" : "Close";

    public static string Confirm => IsUk ? "Підтвердження" : "Confirm";

    public static string Ok => "OK";

    public static string SettingsSaved => IsUk ? "Налаштування збережено." : "Settings saved.";

    public static string StudentPolicySettingsApplyFailed => IsUk ? "Не вдалося застосувати policy settings до учнівських ПК" : "Failed to apply policy settings to student PCs";

    public static string ManualAgentTitle => IsUk ? "Ручний агент" : "Manual Agent";

    public static string RemoteCommandTitle => IsUk ? "Віддалена команда" : "Remote Command";

    public static string RemoteCommandScript => IsUk ? "Команди або сценарій" : "Commands or script";

    public static string RemoteCommandHint => IsUk ? "Вводьте по одній команді в рядок. Команди будуть виконані послідовно." : "Enter one command per line. Commands will run sequentially.";

    public static string RunAs => IsUk ? "Запускати як" : "Run as";

    public static string RunAsCurrentUser => IsUk ? "Поточний користувач" : "Current user";

    public static string RunAsAdministrator => IsUk ? "Адміністратор" : "Administrator";

    public static string InsertFromFrequentPrograms => IsUk ? "Вставити з частих програм" : "Insert from frequent programs";

    public static string SaveDesktopIconLayout => IsUk ? "Зберегти розкладку іконок" : "Save desktop icon layout";

    public static string RestoreDesktopIconLayout => IsUk ? "Відновити розкладку іконок" : "Restore desktop icon layout";

    public static string RestoreDesktopIconLayoutOnSelectedStudents => IsUk ? "Відновити іконки на вибраних ПК" : "Restore icons on selected PCs";

    public static string RestoreDesktopIconLayoutOnAllOnlineStudents => IsUk ? "Відновити іконки на всіх онлайн ПК" : "Restore icons on all online PCs";

    public static string ApplyCurrentDesktopIconLayoutToSelectedStudents => IsUk ? "Передати layout поточного ПК на вибрані ПК" : "Send current PC layout to selected PCs";

    public static string ApplyCurrentDesktopIconLayoutToAllOnlineStudents => IsUk ? "Передати layout поточного ПК на всі онлайн ПК" : "Send current PC layout to all online PCs";

    public static string FrequentProgramsTitle => IsUk ? "Часті програми" : "Frequent Programs";

    public static string RefreshFrequentPrograms => IsUk ? "Оновити список частих програм з усіх ПК" : "Refresh frequent programs from all PCs";

    public static string ManageFrequentPrograms => IsUk ? "Керувати списком частих програм" : "Manage frequent programs";

    public static string RunCommandOnSelectedStudents => IsUk ? "Виконати команду на вибраних ПК" : "Run command on selected PCs";

    public static string RunCommandOnAllOnlineStudents => IsUk ? "Виконати команду на всіх онлайн ПК" : "Run command on all online PCs";

    public static string UpdateSelectedStudents => IsUk ? "Оновити вибрані ПК" : "Update selected PCs";

    public static string UpdateAllOnlineStudents => IsUk ? "Оновити всі онлайн ПК" : "Update all online PCs";

    public static string AddProgram => IsUk ? "Додати" : "Add";

    public static string RemoveProgram => IsUk ? "Видалити" : "Remove";

    public static string InsertSelected => IsUk ? "Вставити вибране" : "Insert selected";

    public static string ProgramName => IsUk ? "Назва програми" : "Program name";

    public static string CommandText => IsUk ? "Команда" : "Command";

    public static string ChooseProgramFirst => IsUk ? "Спочатку виберіть програму." : "Choose a program first.";

    public static string CommandScriptRequired => IsUk ? "Введіть хоча б одну команду." : "Enter at least one command.";

    public static string DisplayName => IsUk ? "Назва" : "Display name";

    public static string IpAddress => IsUk ? "IP адреса" : "IP address";

    public static string Port => IsUk ? "Порт" : "Port";

    public static string MacAddress => IsUk ? "MAC адреса" : "MAC address";

    public static string PromptInput => IsUk ? "Ввід" : "Input";

    public static string AboutWindowTitle => IsUk ? "Про ClassCommander" : "About ClassCommander";

    public static string AboutDescription => IsUk ? "ClassCommander — це кросплатформний клієнт для підключення до StudentAgent, перегляду процесів і файлових операцій з macOS, Linux або Windows." : "ClassCommander is the cross-platform desktop client for connecting to StudentAgent, browsing processes, and performing classroom file operations from macOS, Linux, or Windows.";

    public static string Copyright => "© 2026 Taras Motuschuk. All rights reserved. Email: mtomekt@gmail.com";

    public static string NoAgentsAvailable => IsUk ? "Немає доступних агентів." : "No agents available.";

    public static string ChooseAgentFirst => IsUk ? "Спочатку виберіть агент." : "Choose an agent first.";

    public static string ChooseAgentsForDistribution => IsUk ? "Виберіть одного або кількох агентів для розсилки." : "Choose one or more agents for distribution.";

    public static string NoOnlineAgentsAvailableForDistribution => IsUk ? "Немає онлайн-агентів для групового копіювання." : "No online agents are available for bulk copy.";

    public static string BrowserLockToggleFailed => IsUk ? "Не вдалося оновити блокування браузера" : "Failed to update browser lock";

    public static string BrowserLockRequiresOnlineAgent => IsUk ? "Блокування браузера можна змінювати лише для онлайн-агентів." : "Browser lock can only be changed for online agents.";

    public static string InputLockToggleFailed => IsUk ? "Не вдалося оновити блокування клавіатури і миші" : "Failed to update keyboard and mouse lock";

    public static string InputLockRequiresOnlineAgent => IsUk ? "Блокування клавіатури і миші можна змінювати лише для онлайн-агентів." : "Keyboard and mouse lock can only be changed for online agents.";

    public static string ConnectionFailed => IsUk ? "Підключення не вдалося." : "Connection failed.";

    public static string ConnectFromAgentsTabFirst => IsUk ? "Спочатку підключіться до ПК на вкладці учнівських ПК." : "Connect to a PC from the Student PCs tab first.";

    public static string ChooseManualAgentFirst => IsUk ? "Спочатку виберіть ручний агент." : "Choose a manual agent first.";

    public static string ManualAgentNotFound => IsUk ? "Ручний агент не знайдено." : "Manual agent not found.";

    public static string RemoveManualAgentTitle => IsUk ? "Видалити ручний агент" : "Remove Manual Agent";

    public static string DesktopIconLayoutError => IsUk ? "Помилка операції з іконками робочого стола" : "Desktop icon operation error";

    public static string CheckForAgentUpdate => IsUk ? "Перевірити оновлення агентів..." : "Check Agent Updates...";

    public static string CheckForClientUpdate => IsUk ? "Перевірити оновлення клієнта..." : "Check for Client Updates...";

    public static string StartAgentUpdate => IsUk ? "Оновити поточний підключений ПК" : "Update Current Connected PC";

    public static string AgentUpdateRequiresOnlineAgent => IsUk ? "Для оновлення потрібен онлайн-агент." : "The agent must be online to update.";

    public static string AgentUpdateCheckFailed => IsUk ? "Не вдалося перевірити оновлення агента" : "Failed to check for agent updates";

    public static string AgentUpdateStartFailed => IsUk ? "Не вдалося запустити оновлення агента" : "Failed to start agent update";

    public static string UpdatePreparationTitle => IsUk ? "Підготовка оновлення" : "Update Preparation";

    public static string UpdatePreparationCheckButton => IsUk ? "Перевірити апдейти" : "Check updates";

    public static string UpdatePreparationDownloadButton => "Download update";

    public static string UpdatePreparationMissing => IsUk ? "Спочатку підготуйте апдейт у вікні перевірки апдейтів." : "Prepare the update package first in the update preparation window.";

    public static string ClientUpdateTitle => IsUk ? "Оновлення клієнта" : "Client Update";

    public static string ClientUpdateCheckButton => IsUk ? "Перевірити оновлення" : "Check updates";

    public static string ClientUpdateDownloadButton => IsUk ? "Завантажити інсталятор" : "Download installer";

    public static string ClientUpdateInstallButton => IsUk ? "Встановити оновлення" : "Install update";

    public static string ClientUpdateDownloadMissing => IsUk ? "Спочатку перевірте наявність оновлення клієнта." : "Check for a client update first.";

    public static string ClientUpdateInstallMissing => IsUk ? "Спочатку завантажте інсталятор клієнта." : "Download the client installer first.";

    public static string ClientUpdateInstallStarted => IsUk ? "Інсталятор оновлення відкрито." : "Update installer has been opened.";

    public static string RegistryTab => IsUk ? "Реєстр" : "Registry";

    public static string RegistryValueType => IsUk ? "Тип" : "Type";

    public static string RegistryValueData => IsUk ? "Дані" : "Data";

    public static string RegistryLoadError => IsUk ? "Помилка завантаження реєстру" : "Registry load error";

    public static string NewValue => IsUk ? "Нове значення" : "New Value";

    public static string NewKey => IsUk ? "Новий ключ" : "New Key";

    public static string EditValue => IsUk ? "Редагувати значення" : "Edit Value";

    public static string DeleteValue => IsUk ? "Видалити значення" : "Delete Value";

    public static string DeleteKey => IsUk ? "Видалити ключ" : "Delete Key";

    public static string ExportRegFile => IsUk ? "Експортувати .reg" : "Export .reg";

    public static string ImportRegFile => IsUk ? "Імпортувати .reg" : "Import .reg";

    public static string ValueName => IsUk ? "Назва значення" : "Value name";

    public static string ValueType => IsUk ? "Тип значення" : "Value type";

    public static string ValueData => IsUk ? "Дані значення" : "Value data";

    public static string KeyName => IsUk ? "Назва ключа" : "Key name";

    public static string ConfirmDeleteValue => IsUk ? "Видалити це значення?" : "Delete this value?";

    public static string ConfirmDeleteKey => IsUk ? "Видалити цей ключ і всі його подключі?" : "Delete this key and all its subkeys?";

    public static string ValueCreated => IsUk ? "Значення створено" : "Value created";

    public static string ValueUpdated => IsUk ? "Значення оновлено" : "Value updated";

    public static string ValueDeleted => IsUk ? "Значення видалено" : "Value deleted";

    public static string KeyCreated => IsUk ? "Ключ створено" : "Key created";

    public static string KeyDeleted => IsUk ? "Ключ видалено" : "Key deleted";

    public static string RegistryError => IsUk ? "Помилка операції з реєстром" : "Registry operation error";

    public static string RegistryExportError => IsUk ? "Помилка експорту реєстру" : "Registry export error";

    public static string RegistryImportError => IsUk ? "Помилка імпорту реєстру" : "Registry import error";

    public static string Confirmation => IsUk ? "Підтвердження" : "Confirmation";

    public static string SelectKeyFirst => IsUk ? "Спочатку виберіть ключ" : "Select a key first";

    public static string SelectValueFirst => IsUk ? "Спочатку виберіть значення" : "Select a value first";

    public static string Required => IsUk ? "обов'язково" : "required";

    public static string ChooseProcessFirst => IsUk ? "Спочатку виберіть процес." : "Choose a process first.";

    public static string TerminateProcessTitle => IsUk ? "Завершити процес" : "Terminate Process";

    public static string RestartSelected => IsUk ? "Перезапустити вибране" : "Restart Selected";

    public static string ProcessDetailsTitle => IsUk ? "Відомості про процес" : "Process Details";

    public static string ProcessDetailsLoadError => IsUk ? "Помилка завантаження відомостей про процес" : "Process details load error";

    public static string ProcessLoadError => IsUk ? "Помилка завантаження процесів" : "Process load error";

    public static string DiscoveryError => IsUk ? "Помилка пошуку агентів" : "Discovery error";

    public static string PanelsRefreshed => IsUk ? "Панелі оновлено" : "Panels refreshed";

    public static string LocalBrowseError => IsUk ? "Помилка перегляду локальних файлів" : "Local browse error";

    public static string RemoteBrowseError => IsUk ? "Помилка перегляду віддалених файлів" : "Remote browse error";

    public static string RemoteListingFailed => IsUk ? "Не вдалося отримати список віддалених файлів." : "Remote listing failed.";

    public static string ChooseLocalFileToUpload => IsUk ? "Виберіть локальний файл для завантаження." : "Choose a local file to upload.";

    public static string ChooseLocalFileOrFolderToDistribute => IsUk ? "Виберіть локальний файл або папку для розсилки." : "Choose a local file or folder to distribute.";

    public static string DistributionDestinationPathRequired => IsUk ? "У налаштуваннях задайте папку призначення на учнівських ПК." : "Set the student destination folder in settings first.";

    public static string UploadError => IsUk ? "Помилка завантаження файлу" : "Upload error";

    public static string BulkCopyError => IsUk ? "Помилка групового копіювання" : "Bulk copy error";

    public static string BulkClearError => IsUk ? "Помилка групового очищення папки" : "Bulk folder clear error";

    public static string BulkCollectError => IsUk ? "Помилка групового збору робіт" : "Bulk work collection error";

    public static string BulkCopyResultTitle => IsUk ? "Результат групового копіювання" : "Bulk copy result";

    public static string BulkCommandsResultTitle => IsUk ? "Результат групової команди" : "Group command result";

    public static string BulkCommandError => IsUk ? "Помилка групового виконання команди" : "Bulk remote command error";

    public static string FrequentProgramsRefreshError => IsUk ? "Помилка оновлення списку частих програм" : "Failed to refresh frequent programs";

    public static string PreparingDistributionPlan => IsUk ? "Підготовка плану копіювання..." : "Preparing distribution plan...";

    public static string NoOnlineAgentsAvailableForGroupCommand => IsUk ? "Немає онлайн-агентів для групової команди." : "No online agents are available for the group command.";

    public static string BulkBrowserLockError => IsUk ? "Помилка групового блокування браузера" : "Bulk browser lock error";

    public static string BulkInputLockError => IsUk ? "Помилка групового блокування клавіатури і миші" : "Bulk keyboard and mouse lock error";

    public static string BulkWindowsRestrictionsError => IsUk ? "Помилка групового застосування Windows-обмежень" : "Bulk Windows restrictions error";

    public static string SplashTitle => "ClassCommander";

    public static string SplashSubtitle => IsUk ? "Підготовка ClassCommander..." : "Preparing ClassCommander...";

    public static string ClearDestinationFolderNotConfigured => IsUk ? "У налаштуваннях задайте папку призначення на учнівських ПК." : "Set the student destination folder in settings first.";

    public static string StudentWorkFolderNotConfigured => IsUk ? "У налаштуваннях задайте базовий шлях і назву папки робіт." : "Set the student work base path and work folder name in settings first.";

    public static string PreparingWorkCollection => IsUk ? "Підготовка збору робіт..." : "Preparing work collection...";

    public static string ChooseRemoteFileToDownload => IsUk ? "Виберіть віддалений файл для скачування." : "Choose a remote file to download.";

    public static string OpenLocalError => IsUk ? "Помилка локального відкриття" : "Local open error";

    public static string OpenRemoteError => IsUk ? "Помилка віддаленого відкриття" : "Remote open error";

    public static string DownloadError => IsUk ? "Помилка скачування файлу" : "Download error";

    public static string ChooseLocalEntryFirst => IsUk ? "Спочатку виберіть локальний елемент." : "Choose a local entry first.";

    public static string RenameLocalEntryTitle => IsUk ? "Перейменувати локальний елемент" : "Rename Local Entry";

    public static string DeleteLocalEntryTitle => IsUk ? "Видалити локальний елемент" : "Delete Local Entry";

    public static string LocalDeleteError => IsUk ? "Помилка локального видалення" : "Local delete error";

    public static string LocalRenameError => IsUk ? "Помилка локального перейменування" : "Local rename error";

    public static string ChooseRemoteEntryFirst => IsUk ? "Спочатку виберіть віддалений елемент." : "Choose a remote entry first.";

    public static string RenameRemoteEntryTitle => IsUk ? "Перейменувати віддалений елемент" : "Rename Remote Entry";

    public static string DeleteRemoteEntryTitle => IsUk ? "Видалити віддалений елемент" : "Delete Remote Entry";

    public static string RemoteDeleteError => IsUk ? "Помилка віддаленого видалення" : "Remote delete error";

    public static string RemoteRenameError => IsUk ? "Помилка віддаленого перейменування" : "Remote rename error";

    public static string CreateRemoteFolderTitle => IsUk ? "Створити віддалену папку" : "Create Remote Folder";

    public static string FolderName => IsUk ? "Назва папки:" : "Folder name:";

    public static string EntryName => IsUk ? "Назва елемента:" : "Entry name:";

    public static string DefaultFolderName => IsUk ? "НоваПапка" : "NewFolder";

    public static string CreateFolderError => IsUk ? "Помилка створення папки" : "Create folder error";

    public static string FooterDescription => IsUk ? "Клієнт Avalonia є кросплатформним і підтримує той самий сценарій пошуку агентів, ручних записів, фільтрів статусу, автоперепідключення та групового копіювання, що й Windows-клієнт." : "Avalonia client is cross-platform and includes the same agent discovery, manual entries, status filtering, auto-reconnect, and bulk copy workflow as the Windows client.";

    public static string DisplayNameRequired => IsUk ? "Назва є обов'язковою." : "Display name is required.";

    public static string IpAddressRequired => IsUk ? "IP адреса є обов'язковою." : "IP address is required.";

    public static string Validation => IsUk ? "Перевірка" : "Validation";

    public static string AutoSource => IsUk ? "Авто" : "Auto";

    public static string ManualSource => IsUk ? "Вручну" : "Manual";

    public static string ManualAutoSource => IsUk ? "Вручну+Авто" : "Manual+Auto";

    public static string ManualVersion => IsUk ? "Вручну" : "Manual";

    public static string Window => IsUk ? "Вікно" : "Window";

    public static string Visible => IsUk ? "Видимий" : "Visible";

    public static string StartedUtc => IsUk ? "Запущено UTC" : "Started UTC";

    public static string Responding => IsUk ? "Відповідає" : "Responding";

    public static string ExecutablePath => IsUk ? "Шлях до exe" : "Executable path";

    public static string CommandLine => IsUk ? "Командний рядок" : "Command line";

    public static string SessionId => IsUk ? "Сесія" : "Session ID";

    public static string ThreadCount => IsUk ? "Потоки" : "Thread count";

    public static string HandleCount => IsUk ? "Дескриптори" : "Handle count";

    public static string PriorityClass => IsUk ? "Пріоритет" : "Priority class";

    public static string TotalProcessorTime => IsUk ? "Процесорний час" : "Total processor time";

    public static string FileVersion => IsUk ? "Версія файла" : "File version";

    public static string ProductName => IsUk ? "Продукт" : "Product name";

    public static string Error => IsUk ? "Помилка" : "Error";

    public static string NotAvailable => IsUk ? "Not available" : "Not available";
}
