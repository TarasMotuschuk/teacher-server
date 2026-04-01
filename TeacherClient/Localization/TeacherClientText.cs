using Teacher.Common.Localization;
using Teacher.Common.Contracts;

namespace TeacherClient.Localization;

internal static class TeacherClientText
{
    public static UiLanguage CurrentLanguage { get; private set; } = UiLanguageExtensions.GetDefault();

    public static void SetLanguage(UiLanguage language)
    {
        CurrentLanguage = language.Normalize();
    }

    public static bool IsUk => CurrentLanguage == UiLanguage.Ukrainian;

    public static string MainTitle => IsUk ? "Клієнт викладача" : "Teacher Classroom Client";
    public static string ConnectionMenu => IsUk ? "Підключення" : "Connection";
    public static string Settings => IsUk ? "Налаштування" : "Settings";
    public static string RefreshAgents => IsUk ? "Оновити агентів" : "Refresh Agents";
    public static string ConnectSelectedAgent => IsUk ? "Підключити вибраний агент" : "Connect Selected Agent";
    public static string AddManualAgent => IsUk ? "Додати вручну" : "Add Manual Agent";
    public static string EditManualAgent => IsUk ? "Редагувати вручну" : "Edit Manual Agent";
    public static string RemoveManualAgent => IsUk ? "Видалити вручну" : "Remove Manual Agent";
    public static string ProcessesMenu => IsUk ? "Процеси" : "Processes";
    public static string Refresh => IsUk ? "Оновити" : "Refresh";
    public static string TerminateSelected => IsUk ? "Завершити вибране" : "Terminate Selected";
    public static string FilesMenu => IsUk ? "Файли" : "Files";
    public static string GroupCommandsMenu => IsUk ? "Групові команди" : "Group Commands";
    public static string BrowserCommandsMenu => IsUk ? "Браузер" : "Browser";
    public static string InputCommandsMenu => IsUk ? "Клавіатура і миша" : "Keyboard and Mouse";
    public static string CommandsMenu => IsUk ? "Команди" : "Commands";
    public static string PowerCommandsMenu => IsUk ? "Живлення" : "Power";
    public static string SelectedStudentsMenu => IsUk ? "Вибрані ПК" : "Selected PCs";
    public static string AllOnlineStudentsMenu => IsUk ? "Всі онлайн ПК" : "All Online PCs";
    public static string StudentWorkMenu => IsUk ? "Роботи учнів" : "Student Work";
    public static string FrequentProgramsMenu => IsUk ? "Часті програми" : "Frequent Programs";
    public static string RefreshBoth => IsUk ? "Оновити обидві панелі" : "Refresh Both";
    public static string Upload => IsUk ? "Завантажити на агент" : "Upload";
    public static string Download => IsUk ? "Скачати з агента" : "Download";
    public static string OpenRemote => IsUk ? "Відкрити на учнівському ПК" : "Open on Student PC";
    public static string DeleteLocal => IsUk ? "Видалити локально" : "Delete Local";
    public static string DeleteRemote => IsUk ? "Видалити віддалено" : "Delete Remote";
    public static string NewRemoteFolder => IsUk ? "Нова віддалена папка" : "New Remote Folder";
    public static string Help => IsUk ? "Довідка" : "Help";
    public static string About => IsUk ? "Про програму" : "About";
    public static string StatusReady => IsUk ? "Готово. Виберіть машину на вкладці агентів і підключіться." : "Ready. Use the Agents tab to select a student machine, then connect.";
    public static string AgentsTab => IsUk ? "Агенти" : "Agents";
    public static string ProcessesTab => IsUk ? "Процеси" : "Processes";
    public static string FilesTab => IsUk ? "Файли" : "Files";
    public static string Search => IsUk ? "Пошук" : "Search";
    public static string Group => IsUk ? "Група" : "Group";
    public static string BrowserLock => IsUk ? "Блок браузера" : "Browser lock";
    public static string InputLock => IsUk ? "Блок вводу" : "Input lock";
    public static string Status => IsUk ? "Статус" : "Status";
    public static string AutoReconnect => IsUk ? "Автоперепідключення" : "Auto-reconnect";
    public static string AllGroups => IsUk ? "Усі групи" : "All groups";
    public static string AllStatuses => IsUk ? "Усі" : "All";
    public static string Online => IsUk ? "Онлайн" : "Online";
    public static string Offline => IsUk ? "Офлайн" : "Offline";
    public static string Unknown => IsUk ? "Невідомо" : "Unknown";
    public static string TeacherPc => IsUk ? "ПК викладача" : "Teacher PC";
    public static string StudentPc => IsUk ? "ПК студента" : "Student PC";
    public static string Up => IsUk ? "Вгору" : "Up";
    public static string Source => IsUk ? "Джерело" : "Source";
    public static string Machine => IsUk ? "Машина" : "Machine";
    public static string User => IsUk ? "Користувач" : "User";
    public static string Notes => IsUk ? "Нотатки" : "Notes";
    public static string LastSeenUtc => IsUk ? "Останній сигнал UTC" : "Last Seen UTC";
    public static string Visible => IsUk ? "Видимий" : "Visible";
    public static string StartedUtc => IsUk ? "Запущено UTC" : "Started UTC";
    public static string Window => IsUk ? "Вікно" : "Window";
    public static string Process => IsUk ? "Процес" : "Process";
    public static string Name => IsUk ? "Назва" : "Name";
    public static string NameWithIcon => IsUk ? "Назва" : "Name";
    public static string Extension => IsUk ? "Розширення" : "Extension";
    public static string Attributes => IsUk ? "Атрибути" : "Attributes";
    public static string Size => IsUk ? "Розмір" : "Size";
    public static string DirectoryShort => IsUk ? "Кат." : "Dir";
    public static string ModifiedUtc => IsUk ? "Змінено UTC" : "Modified UTC";
    public static string SettingsDialogTitle => IsUk ? "Налаштування клієнта викладача" : "Teacher Client Settings";
    public static string SharedSecret => IsUk ? "Спільний секрет" : "Shared secret";
    public static string BulkCopyDestinationPath => IsUk ? "Папка призначення на учнях" : "Student destination folder";
    public static string StudentWorkRootPath => IsUk ? "Базовий шлях робіт на учнях" : "Student work base path";
    public static string StudentWorkFolderName => IsUk ? "Назва папки робіт" : "Student work folder name";
    public static string Language => IsUk ? "Мова" : "Language";
    public static string SettingsHint => IsUk ? "Спільний секрет використовується для перевірки доступності агентів і для всіх API-запитів. Папка призначення визначає стартовий шлях на учнівських ПК для масового копіювання файлів і папок. Базовий шлях і назва папки робіт визначають спільний каталог, який буде створюватися на учнівських ПК для збереження робіт." : "The shared secret is used for reachability checks and all teacher-to-student API calls. The destination folder defines the starting path on student PCs for bulk file and folder distribution. The work base path and work folder name define the shared student folder that will be created on student PCs for saved work.";
    public static string Save => IsUk ? "Зберегти" : "Save";
    public static string Cancel => IsUk ? "Скасувати" : "Cancel";
    public static string AboutTitle => IsUk ? "Про TeacherClient" : "About TeacherClient";
    public static string AboutDescription => IsUk ? "TeacherClient — це Windows-клієнт для підключення до StudentAgent, перегляду процесів і керування файлами у прозорому навчальному середовищі." : "TeacherClient is the Windows desktop control panel for connecting to StudentAgent, viewing processes, and managing files in a transparent classroom environment.";
    public static string Version => IsUk ? "Версія:" : "Version:";
    public static string Close => IsUk ? "Закрити" : "Close";
    public static string InputTitle => IsUk ? "Ввід" : "Input";
    public static string Prompt => IsUk ? "Параметр" : "Prompt";
    public static string Ok => "OK";
    public static string ManualAgentTitle => IsUk ? "Ручний агент" : "Manual Agent";
    public static string RemoteCommandTitle => IsUk ? "Віддалена команда" : "Remote Command";
    public static string RemoteCommandScript => IsUk ? "Команди або сценарій" : "Commands or script";
    public static string RemoteCommandHint => IsUk ? "Вводьте по одній команді в рядок. Команди будуть виконані послідовно." : "Enter one command per line. Commands will run sequentially.";
    public static string RunAs => IsUk ? "Запускати як" : "Run as";
    public static string RunAsCurrentUser => IsUk ? "Поточний користувач" : "Current user";
    public static string RunAsAdministrator => IsUk ? "Адміністратор" : "Administrator";
    public static string InsertFromFrequentPrograms => IsUk ? "Вставити з частих програм" : "Insert from frequent programs";
    public static string FrequentProgramsTitle => IsUk ? "Часті програми" : "Frequent Programs";
    public static string RefreshFrequentPrograms => IsUk ? "Оновити список частих програм з усіх ПК" : "Refresh frequent programs from all PCs";
    public static string ManageFrequentPrograms => IsUk ? "Керувати списком частих програм" : "Manage frequent programs";
    public static string RunCommandOnSelectedStudents => IsUk ? "Виконати команду на вибраних ПК" : "Run command on selected PCs";
    public static string RunCommandOnAllOnlineStudents => IsUk ? "Виконати команду на всіх онлайн ПК" : "Run command on all online PCs";
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
    public static string Validation => IsUk ? "Перевірка" : "Validation";
    public static string DisplayNameRequired => IsUk ? "Назва є обов'язковою." : "Display name is required.";
    public static string IpAddressRequired => IsUk ? "IP адреса є обов'язковою." : "IP address is required.";
    public static string Confirm => IsUk ? "Підтвердження" : "Confirm";
    public static string NoAgentsAvailable => IsUk ? "Немає доступних агентів." : "No agents available.";
    public static string ChooseAgentFirst => IsUk ? "Спочатку виберіть агент." : "Choose an agent first.";
    public static string ConnectionFailed => IsUk ? "Підключення не вдалося." : "Connection failed.";
    public static string ConnectFromAgentsTabFirst => IsUk ? "Спочатку підключіться до агента на вкладці агентів." : "Connect to an agent from the Agents tab first.";
    public static string ChooseProcessFirst => IsUk ? "Спочатку виберіть процес." : "Choose a process first.";
    public static string TerminateProcessTitle => IsUk ? "Завершити процес" : "Terminate Process";
    public static string ProcessDetailsTitle => IsUk ? "Відомості про процес" : "Process Details";
    public static string ProcessDetailsLoadError => IsUk ? "Помилка завантаження відомостей про процес" : "Process details load error";
    public static string RestartSelected => IsUk ? "Перезапустити вибране" : "Restart Selected";
    public static string RestartProcessPrompt(string name, int id) => IsUk ? $"Перезапустити процес {name} ({id})?" : $"Restart process {name} ({id})?";
    public static string FormatProcessRestarted(string name) => IsUk ? $"Процес {name} перезапущено" : $"Process {name} restarted";
    public static string LocalBrowseError => IsUk ? "Помилка перегляду локальних файлів" : "Local browse error";
    public static string RemoteBrowseError => IsUk ? "Помилка перегляду віддалених файлів" : "Remote browse error";
    public static string ProcessLoadError => IsUk ? "Помилка завантаження процесів" : "Process load error";
    public static string DiscoveryError => IsUk ? "Помилка пошуку агентів" : "Discovery error";
    public static string UploadError => IsUk ? "Помилка завантаження файлу" : "Upload error";
    public static string BulkCopyError => IsUk ? "Помилка групового копіювання" : "Bulk copy error";
    public static string DownloadError => IsUk ? "Помилка скачування файлу" : "Download error";
    public static string LocalDeleteError => IsUk ? "Помилка локального видалення" : "Local delete error";
    public static string RemoteDeleteError => IsUk ? "Помилка віддаленого видалення" : "Remote delete error";
    public static string CreateFolderError => IsUk ? "Помилка створення папки" : "Create folder error";
    public static string RemoteListingFailed => IsUk ? "Не вдалося отримати список віддалених файлів." : "Remote listing failed.";
    public static string PanelsRefreshed => IsUk ? "Панелі оновлено" : "Panels refreshed";
    public static string ChooseLocalFileToUpload => IsUk ? "Виберіть локальний файл для завантаження." : "Choose a local file to upload.";
    public static string ChooseLocalFileOrFolderToDistribute => IsUk ? "Виберіть локальний файл або папку для розсилки." : "Choose a local file or folder to distribute.";
    public static string ChooseAgentsForDistribution => IsUk ? "Виберіть одного або кількох агентів для розсилки." : "Choose one or more agents for distribution.";
    public static string NoOnlineAgentsAvailableForDistribution => IsUk ? "Немає онлайн-агентів для групового копіювання." : "No online agents are available for bulk copy.";
    public static string BrowserLockEnabledFor(string machine) => IsUk ? $"Блокування браузера увімкнено на {machine}" : $"Browser lock enabled on {machine}";
    public static string BrowserLockDisabledFor(string machine) => IsUk ? $"Блокування браузера вимкнено на {machine}" : $"Browser lock disabled on {machine}";
    public static string BrowserLockToggleFailed => IsUk ? "Не вдалося оновити блокування браузера" : "Failed to update browser lock";
    public static string BrowserLockRequiresOnlineAgent => IsUk ? "Блокування браузера можна змінювати лише для онлайн-агентів." : "Browser lock can only be changed for online agents.";
    public static string InputLockEnabledFor(string machine) => IsUk ? $"Блокування клавіатури і миші увімкнено на {machine}" : $"Keyboard and mouse lock enabled on {machine}";
    public static string InputLockDisabledFor(string machine) => IsUk ? $"Блокування клавіатури і миші вимкнено на {machine}" : $"Keyboard and mouse lock disabled on {machine}";
    public static string InputLockToggleFailed => IsUk ? "Не вдалося оновити блокування клавіатури і миші" : "Failed to update keyboard and mouse lock";
    public static string InputLockRequiresOnlineAgent => IsUk ? "Блокування клавіатури і миші можна змінювати лише для онлайн-агентів." : "Keyboard and mouse lock can only be changed for online agents.";
    public static string DistributionDestinationPathRequired => IsUk ? "У налаштуваннях задайте папку призначення на учнівських ПК." : "Set the student destination folder in settings first.";
    public static string ChooseRemoteFileToDownload => IsUk ? "Виберіть віддалений файл для скачування." : "Choose a remote file to download.";
    public static string OpenRemoteError => IsUk ? "Помилка віддаленого відкриття" : "Remote open error";
    public static string FormatOpenedRemote(string name) => IsUk ? $"Відкрито на учнівському ПК: {name}" : $"Opened on student PC: {name}";
    public static string ChooseLocalEntryFirst => IsUk ? "Спочатку виберіть локальний елемент." : "Choose a local entry first.";
    public static string ChooseRemoteEntryFirst => IsUk ? "Спочатку виберіть віддалений елемент." : "Choose a remote entry first.";
    public static string DeleteLocalEntryTitle => IsUk ? "Видалити локальний елемент" : "Delete Local Entry";
    public static string DeleteRemoteEntryTitle => IsUk ? "Видалити віддалений елемент" : "Delete Remote Entry";
    public static string CreateRemoteFolderTitle => IsUk ? "Створити віддалену папку" : "Create remote folder";
    public static string FolderName => IsUk ? "Назва папки:" : "Folder name:";
    public static string NewFolderDefaultName => IsUk ? "НоваПапка" : "NewFolder";
    public static string AutoSource => IsUk ? "Авто" : "Auto";
    public static string ManualSource => IsUk ? "Вручну" : "Manual";
    public static string ManualAutoSource => IsUk ? "Вручну+Авто" : "Manual+Auto";
    public static string ManualVersion => IsUk ? "Вручну" : "Manual";
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
    public static string NotAvailable => IsUk ? "Недоступно" : "Not available";
    public static string Yes => IsUk ? "Так" : "Yes";
    public static string No => IsUk ? "Ні" : "No";
    public static string SendToSelectedStudents => IsUk ? "Надіслати вибраним учням" : "Send to selected students";
    public static string SendToAllOnlineStudents => IsUk ? "Надіслати всім онлайн учням" : "Send to all online students";
    public static string ClearDestinationFolderOnSelectedStudents => IsUk ? "Очистити папку призначення на вибраних учнях" : "Clear destination folder on selected students";
    public static string ClearDestinationFolderOnAllOnlineStudents => IsUk ? "Очистити папку призначення на всіх онлайн учнях" : "Clear destination folder on all online students";
    public static string DestinationFolderMenu => IsUk ? "Папка призначення" : "Destination Folder";
    public static string LockBrowsersOnAllOnlineStudents => IsUk ? "Заблокувати браузер на всіх онлайн учнівських ПК" : "Lock browser on all online student PCs";
    public static string LockInputOnAllOnlineStudents => IsUk ? "Заблокувати клавіатуру і мишу на всіх онлайн учнівських ПК" : "Lock keyboard and mouse on all online student PCs";
    public static string UnlockInputOnAllOnlineStudents => IsUk ? "Розблокувати клавіатуру і мишу на всіх онлайн учнівських ПК" : "Unlock keyboard and mouse on all online student PCs";
    public static string ShutdownCommand => IsUk ? "Вимкнути" : "Shut Down";
    public static string RestartCommand => IsUk ? "Перезавантажити" : "Restart";
    public static string LogOffCommand => IsUk ? "Вийти з облікового запису" : "Log Off";
    public static string CreateStudentWorkFolderOnAllAgents => IsUk ? "Створити папку для робіт на всіх ПК" : "Create work folder on all PCs";
    public static string CollectStudentWorkToTeacherPc => IsUk ? "Зібрати роботи учнів на вчительський ПК" : "Collect student work to teacher PC";
    public static string ClearStudentWorkFolderOnAllAgents => IsUk ? "Очистити папку для робіт на всіх ПК" : "Clear work folder on all PCs";
    public static string BulkCommandsResultTitle => IsUk ? "Результат групової команди" : "Group command result";
    public static string BulkCommandError => IsUk ? "Помилка групового виконання команди" : "Bulk remote command error";
    public static string FrequentProgramsRefreshError => IsUk ? "Помилка оновлення списку частих програм" : "Failed to refresh frequent programs";
    public static string BulkClearError => IsUk ? "Помилка групового очищення папки" : "Bulk folder clear error";
    public static string BulkCollectError => IsUk ? "Помилка групового збору робіт" : "Bulk work collection error";
    public static string NoOnlineAgentsAvailableForGroupCommand => IsUk ? "Немає онлайн-агентів для групової команди." : "No online agents are available for the group command.";
    public static string RemoteCommandPrompt(int count, bool selectedOnly)
        => IsUk
            ? $"Виконати сценарій на {(selectedOnly ? "вибраних" : "всіх онлайн")} учнівських ПК ({count})?"
            : $"Run the script on {(selectedOnly ? "selected" : "all online")} student PCs ({count})?";
    public static string RemoteCommandProgress(string agent, int agentIndex, int agentCount)
        => IsUk
            ? $"Виконання команди на {agent} ({agentIndex}/{agentCount})"
            : $"Running command on {agent} ({agentIndex}/{agentCount})";
    public static string RemoteCommandCompleted(int count)
        => IsUk ? $"Команду виконано на {count} учнівських ПК" : $"Ran command on {count} student PCs";
    public static string RemoteCommandCompletedWithFailures(int succeeded, int failed)
        => IsUk ? $"Виконання команди: успішно {succeeded}, з помилками {failed}" : $"Remote command: {succeeded} succeeded, {failed} failed";
    public static string FrequentProgramsRefreshed(int count)
        => IsUk ? $"Оновлено список частих програм: {count}" : $"Refreshed frequent programs: {count}";
    public static string BrowserLockPrompt(int count)
        => IsUk
            ? $"Увімкнути блокування браузера на всіх онлайн учнівських ПК ({count})?"
            : $"Enable browser lock on all online student PCs ({count})?";
    public static string BrowserLockCompleted(int count)
        => IsUk ? $"Блокування браузера увімкнено на {count} учнівських ПК" : $"Enabled browser lock on {count} student PCs";
    public static string BrowserLockCompletedWithFailures(int succeeded, int failed)
        => IsUk ? $"Групове блокування браузера: успішно {succeeded}, з помилками {failed}" : $"Bulk browser lock: {succeeded} succeeded, {failed} failed";
    public static string BulkBrowserLockError => IsUk ? "Помилка групового блокування браузера" : "Bulk browser lock error";
    public static string BrowserLockProgress(string agent, int agentIndex, int agentCount)
        => IsUk
            ? $"Увімкнення блокування браузера на {agent} ({agentIndex}/{agentCount})"
            : $"Enabling browser lock on {agent} ({agentIndex}/{agentCount})";
    public static string InputLockPrompt(int count, bool enabled)
        => IsUk
            ? $"{(enabled ? "Увімкнути" : "Вимкнути")} блокування клавіатури і миші на всіх онлайн учнівських ПК ({count})?"
            : $"{(enabled ? "Enable" : "Disable")} keyboard and mouse lock on all online student PCs ({count})?";
    public static string InputLockCompleted(int count, bool enabled)
        => IsUk
            ? $"{(enabled ? "Блокування клавіатури і миші увімкнено" : "Блокування клавіатури і миші вимкнено")} на {count} учнівських ПК"
            : $"{(enabled ? "Enabled" : "Disabled")} keyboard and mouse lock on {count} student PCs";
    public static string InputLockCompletedWithFailures(int succeeded, int failed, bool enabled)
        => IsUk
            ? $"Групове {(enabled ? "увімкнення" : "вимкнення")} блокування вводу: успішно {succeeded}, з помилками {failed}"
            : $"Bulk {(enabled ? "enable" : "disable")} input lock: {succeeded} succeeded, {failed} failed";
    public static string BulkInputLockError => IsUk ? "Помилка групового блокування клавіатури і миші" : "Bulk keyboard and mouse lock error";
    public static string InputLockProgress(string agent, int agentIndex, int agentCount, bool enabled)
        => IsUk
            ? $"{(enabled ? "Увімкнення" : "Вимкнення")} блокування вводу на {agent} ({agentIndex}/{agentCount})"
            : $"{(enabled ? "Enabling" : "Disabling")} input lock on {agent} ({agentIndex}/{agentCount})";
    public static string PowerActionPrompt(PowerActionKind action, int count, bool selectedOnly)
        => IsUk
            ? $"{GetPowerActionVerb(action)} {(selectedOnly ? "вибрані" : "всі онлайн")} учнівські ПК ({count})?"
            : $"{GetPowerActionVerb(action)} {(selectedOnly ? "selected" : "all online")} student PCs ({count})?";
    public static string PowerActionCompleted(PowerActionKind action, int count)
        => IsUk
            ? $"{GetPowerActionPast(action)} {count} учнівських ПК"
            : $"{GetPowerActionPast(action)} {count} student PCs";
    public static string PowerActionCompletedWithFailures(PowerActionKind action, int succeeded, int failed)
        => IsUk
            ? $"{GetPowerActionNoun(action)}: успішно {succeeded}, з помилками {failed}"
            : $"{GetPowerActionNoun(action)}: {succeeded} succeeded, {failed} failed";
    public static string BulkPowerActionError(PowerActionKind action)
        => IsUk ? $"Помилка групової команди: {GetPowerActionNoun(action).ToLowerInvariant()}" : $"Bulk power command error: {GetPowerActionNoun(action).ToLowerInvariant()}";
    public static string PowerActionProgress(PowerActionKind action, string agent, int agentIndex, int agentCount)
        => IsUk
            ? $"{GetPowerActionNoun(action)} на {agent} ({agentIndex}/{agentCount})"
            : $"{GetPowerActionNoun(action)} on {agent} ({agentIndex}/{agentCount})";
    public static string SplashTitle => IsUk ? "Клієнт викладача" : "Teacher Classroom Client";
    public static string SplashSubtitle => IsUk ? "Підготовка робочого середовища викладача..." : "Preparing the teacher workspace...";
    public static string ClearDestinationFolderNotConfigured => IsUk ? "У налаштуваннях задайте папку призначення на учнівських ПК." : "Set the student destination folder in settings first.";
    public static string StudentWorkFolderNotConfigured => IsUk ? "У налаштуваннях задайте базовий шлях і назву папки робіт." : "Set the student work base path and work folder name in settings first.";
    public static string PreparingWorkCollection => IsUk ? "Підготовка збору робіт..." : "Preparing work collection...";
    public static string CollectingWorkProgress(string agent, string path, int agentIndex, int agentCount)
        => IsUk
            ? $"Збір робіт з {agent} ({agentIndex}/{agentCount}) -> {path}"
            : $"Collecting work from {agent} ({agentIndex}/{agentCount}) -> {path}";
    public static string WorkCollectionCompleted(int count, string destination)
        => IsUk ? $"Роботи зібрано з {count} учн. ПК у {destination}" : $"Collected work from {count} student machines into {destination}";
    public static string WorkCollectionCompletedWithFailures(int succeeded, int failed, string destination)
        => IsUk ? $"Збір робіт у {destination}: успішно {succeeded}, з помилками {failed}" : $"Collected work into {destination}: {succeeded} succeeded, {failed} failed";
    public static string WorkFolderProvisioned(int count) => IsUk ? $"Каталог робіт підготовлено на {count} учн. ПК" : $"Prepared student work folder on {count} machines";
    public static string WorkFolderProvisionedWithFailures(int succeeded, int failed) => IsUk ? $"Підготовка каталогу робіт: успішно {succeeded}, з помилками {failed}" : $"Prepared student work folder: {succeeded} succeeded, {failed} failed";
    public static string ClearingDirectoryProgress(string agent, string path, int agentIndex, int agentCount)
        => IsUk
            ? $"Очищення {path} на {agent} (агент {agentIndex}/{agentCount})"
            : $"Clearing {path} on {agent} (agent {agentIndex}/{agentCount})";
    public static string ClearDirectoryCompleted(string name, int count)
        => IsUk ? $"Очищено вміст папки {name} на {count} учн. ПК" : $"Cleared folder contents {name} on {count} student machines";
    public static string ClearDirectoryCompletedWithFailures(string name, int succeeded, int failed)
        => IsUk ? $"Очищення {name}: успішно {succeeded}, з помилками {failed}" : $"Cleared {name}: {succeeded} succeeded, {failed} failed";
    public static string ClearDirectoryPrompt(string path, int count, bool allOnline)
        => IsUk
            ? $"Очистити вміст папки {path} на {(allOnline ? "всіх онлайн" : "вибраних")} учнях ({count})? Сама папка залишиться."
            : $"Clear the contents of folder {path} on {(allOnline ? "all online" : "selected")} students ({count})? The folder itself will remain.";
    public static string BulkCopyResultTitle => IsUk ? "Результат групового копіювання" : "Bulk copy result";
    public static string DistributionCompleted(string name, int count) => IsUk ? $"Розіслано {name} на {count} учн. ПК" : $"Distributed {name} to {count} student machines";
    public static string DistributionCompletedWithFailures(string name, int succeeded, int failed) => IsUk ? $"Розсилка {name}: успішно {succeeded}, з помилками {failed}" : $"Distributed {name}: {succeeded} succeeded, {failed} failed";
    public static string PreparingDistributionPlan => IsUk ? "Підготовка плану копіювання..." : "Preparing distribution plan...";
    public static string DistributionProgress(string agent, string item, int agentIndex, int agentCount, int fileIndex, int fileCount)
        => IsUk
            ? $"Розсилка {item} -> {agent} (агент {agentIndex}/{agentCount}, файл {fileIndex}/{fileCount})"
            : $"Distributing {item} -> {agent} (agent {agentIndex}/{agentCount}, file {fileIndex}/{fileCount})";

    public static string FormatAddedManualAgent(string name) => IsUk ? $"Додано ручний агент {name}" : $"Added manual agent {name}";
    public static string FormatUpdatedManualAgent(string name) => IsUk ? $"Оновлено ручний агент {name}" : $"Updated manual agent {name}";
    public static string FormatRemovedManualAgent(string name) => IsUk ? $"Видалено ручний агент {name}" : $"Removed manual agent {name}";
    public static string ChooseManualAgentFirst => IsUk ? "Спочатку виберіть ручний агент." : "Choose a manual agent first.";
    public static string ManualAgentNotFound => IsUk ? "Ручний агент не знайдено." : "Manual agent not found.";
    public static string RemoveManualAgentPrompt(string name) => IsUk ? $"Видалити ручний агент {name}?" : $"Remove manual agent {name}?";
    public static string SettingsSaved => IsUk ? "Налаштування збережено." : "Settings saved.";
    public static string CheckForAgentUpdate => IsUk ? "Перевірити оновлення агента" : "Check Agent Update";
    public static string StartAgentUpdate => IsUk ? "Оновити вибраний агент" : "Update Selected Agent";
    public static string AgentUpdateRequiresOnlineAgent => IsUk ? "Для оновлення потрібен онлайн-агент." : "The agent must be online to update.";
    public static string AgentUpdateCheckFailed => IsUk ? "Не вдалося перевірити оновлення агента" : "Failed to check for agent updates";
    public static string AgentUpdateStartFailed => IsUk ? "Не вдалося запустити оновлення агента" : "Failed to start agent update";
    public static string AgentUpToDate(string machine, string version) => IsUk ? $"{machine}: актуальна версія {version}" : $"{machine}: already on version {version}";
    public static string AgentUpdateAvailable(string machine, string version) => IsUk ? $"{machine}: доступне оновлення {version}" : $"{machine}: update {version} is available";
    public static string AgentUpdateStarted(string machine, string version) => IsUk ? $"{machine}: запущено оновлення до {version}" : $"{machine}: started update to {version}";
    public static string AgentUpdateState(string machine, string state, string? message) => IsUk
        ? $"{machine}: стан оновлення {state}{(string.IsNullOrWhiteSpace(message) ? string.Empty : $" ({message})")}"
        : $"{machine}: update state {state}{(string.IsNullOrWhiteSpace(message) ? string.Empty : $" ({message})")}";
    public static string TerminateProcessPrompt(string name, int id) => IsUk ? $"Завершити процес {name} ({id})?" : $"Terminate process {name} ({id})?";
    public static string FormatProcessTerminated(string name) => IsUk ? $"Процес {name} завершено" : $"Process {name} terminated";
    public static string FormatLoadedProcesses(int count) => IsUk ? $"Завантажено процесів: {count}" : $"Loaded {count} processes";
    public static string FormatAvailableAgents(int total, int discovered, int manual) => IsUk ? $"Доступно агентів: {total} всього, {discovered} знайдено, {manual} вручну" : $"Available agents: {total} total, {discovered} discovered, {manual} manual";
    public static string FormatUploaded(string name) => IsUk ? $"Завантажено {name}" : $"Uploaded {name}";
    public static string FormatDownloaded(string name) => IsUk ? $"Скачано {name}" : $"Downloaded {name}";
    public static string FormatDeletedLocal(string name) => IsUk ? $"Локальний елемент {name} видалено" : $"Deleted local entry {name}";
    public static string FormatDeletedRemote(string name) => IsUk ? $"Віддалений елемент {name} видалено" : $"Deleted remote entry {name}";
    public static string FormatCreatedRemoteFolder(string name) => IsUk ? $"Створено віддалену папку {name}" : $"Created remote folder {name}";
    public static string DeleteLocalEntryPrompt(string name) => IsUk ? $"Видалити локальний елемент {name}?" : $"Delete local entry {name}?";
    public static string DeleteRemoteEntryPrompt(string name) => IsUk ? $"Видалити віддалений елемент {name}?" : $"Delete remote entry {name}?";
    public static string FormatConnectedToAgent(string source, string machine, string user, string version) => IsUk ? $"Підключено до {source} агента {machine} ({user})  v{version}" : $"Connected to {source} agent {machine} ({user})  v{version}";
    public static string RegistryTab => IsUk ? "Реєстр" : "Registry";
    public static string RegistryValueType => IsUk ? "Тип" : "Type";
    public static string RegistryValueData => IsUk ? "Дані" : "Data";
    public static string RegistryLoadError => IsUk ? "Помилка завантаження реєстру" : "Registry load error";
    public static string FormatLoadedRegistryValues(int count) => IsUk ? $"Значень у ключі: {count}" : $"Values in key: {count}";
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
    public static string RegFilesFilter => IsUk ? "Файли реєстру (*.reg)|*.reg|Усі файли (*.*)|*.*" : "Registry files (*.reg)|*.reg|All files (*.*)|*.*";
    public static string ExportedRegistryKey(string path) => IsUk ? $"Експортовано ключ реєстру {path}" : $"Exported registry key {path}";
    public static string ImportedRegistryFile(int keys, int values) => IsUk ? $"Імпортовано .reg: ключів {keys}, значень {values}" : $"Imported .reg file: {keys} keys, {values} values";
    public static string Confirmation => IsUk ? "Підтвердження" : "Confirmation";
    public static string SelectKeyFirst => IsUk ? "Спочатку виберіть ключ" : "Select a key first";
    public static string SelectValueFirst => IsUk ? "Спочатку виберіть значення" : "Select a value first";
    public static string Required => IsUk ? "обов'язково" : "required";

    private static string GetPowerActionVerb(PowerActionKind action) => action switch
    {
        PowerActionKind.Shutdown => IsUk ? "Вимкнути" : "Shut down",
        PowerActionKind.Restart => IsUk ? "Перезавантажити" : "Restart",
        PowerActionKind.LogOff => IsUk ? "Вивести з облікового запису" : "Log off",
        _ => IsUk ? "Виконати дію для" : "Run action for"
    };

    private static string GetPowerActionPast(PowerActionKind action) => action switch
    {
        PowerActionKind.Shutdown => IsUk ? "Надіслано вимкнення для" : "Sent shut down to",
        PowerActionKind.Restart => IsUk ? "Надіслано перезавантаження для" : "Sent restart to",
        PowerActionKind.LogOff => IsUk ? "Надіслано вихід з облікового запису для" : "Sent log off to",
        _ => IsUk ? "Виконано дію для" : "Ran action for"
    };

    private static string GetPowerActionNoun(PowerActionKind action) => action switch
    {
        PowerActionKind.Shutdown => IsUk ? "Вимкнення" : "Shutdown",
        PowerActionKind.Restart => IsUk ? "Перезавантаження" : "Restart",
        PowerActionKind.LogOff => IsUk ? "Вихід з облікового запису" : "Log off",
        _ => IsUk ? "Команда живлення" : "Power command"
    };
}
