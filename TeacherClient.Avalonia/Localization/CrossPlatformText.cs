using Teacher.Common.Localization;

namespace TeacherClient.CrossPlatform.Localization;

internal static class CrossPlatformText
{
    public static UiLanguage CurrentLanguage { get; private set; } = UiLanguageExtensions.GetDefault();

    public static void SetLanguage(UiLanguage language)
    {
        CurrentLanguage = language.Normalize();
    }

    public static bool IsUk => CurrentLanguage == UiLanguage.Ukrainian;

    public static string MainTitle => IsUk ? "Клієнт викладача (Avalonia)" : "Teacher Classroom Client (Avalonia)";
    public static string ConnectionMenu => IsUk ? "_Підключення" : "_Connection";
    public static string Settings => IsUk ? "_Налаштування" : "_Settings";
    public static string RefreshAgents => IsUk ? "_Оновити агентів" : "_Refresh Agents";
    public static string ConnectSelectedAgent => IsUk ? "Підключити _вибраний агент" : "Connect _Selected Agent";
    public static string AddManualAgent => IsUk ? "_Додати вручну" : "_Add Manual Agent";
    public static string EditManualAgent => IsUk ? "_Редагувати вручну" : "_Edit Manual Agent";
    public static string RemoveManualAgent => IsUk ? "_Видалити вручну" : "_Remove Manual Agent";
    public static string Help => IsUk ? "_Довідка" : "_Help";
    public static string GroupCommands => IsUk ? "_Групові команди" : "_Group Commands";
    public static string GroupCommandsTitle => IsUk ? "Групові команди" : "Group Commands";
    public static string StudentWorkMenu => IsUk ? "_Роботи учнів" : "_Student Work";
    public static string About => IsUk ? "_Про програму" : "_About";
    public static string StatusReady => IsUk ? "Готово. Виберіть машину на вкладці агентів і підключіться." : "Ready. Use the Agents tab to select a student machine, then connect.";
    public static string Agents => IsUk ? "Агенти" : "Agents";
    public static string Processes => IsUk ? "Процеси" : "Processes";
    public static string Files => IsUk ? "Файли" : "Files";
    public static string SearchAgents => IsUk ? "Пошук агентів" : "Search agents";
    public static string AllGroups => IsUk ? "Усі групи" : "All groups";
    public static string All => IsUk ? "Усі" : "All";
    public static string Online => IsUk ? "Онлайн" : "Online";
    public static string Offline => IsUk ? "Офлайн" : "Offline";
    public static string Unknown => IsUk ? "Невідомо" : "Unknown";
    public static string AutoReconnect => IsUk ? "Автоперепідключення" : "Auto-reconnect";
    public static string Source => IsUk ? "Джерело" : "Source";
    public static string Status => IsUk ? "Статус" : "Status";
    public static string Group => IsUk ? "Група" : "Group";
    public static string Machine => IsUk ? "Машина" : "Machine";
    public static string User => IsUk ? "Користувач" : "User";
    public static string Notes => IsUk ? "Нотатки" : "Notes";
    public static string Version => IsUk ? "Версія" : "Version";
    public static string LastSeenUtc => IsUk ? "Останній сигнал UTC" : "Last Seen UTC";
    public static string Refresh => IsUk ? "Оновити" : "Refresh";
    public static string TerminateSelected => IsUk ? "Завершити вибране" : "Terminate Selected";
    public static string RefreshBoth => IsUk ? "Оновити обидві панелі" : "Refresh Both";
    public static string UploadArrow => IsUk ? "Завантажити ->" : "Upload ->";
    public static string DownloadArrow => IsUk ? "<- Скачати" : "<- Download";
    public static string DeleteLocal => IsUk ? "Видалити локально" : "Delete Local";
    public static string DeleteRemote => IsUk ? "Видалити віддалено" : "Delete Remote";
    public static string NewRemoteFolder => IsUk ? "Нова віддалена папка" : "New Remote Folder";
    public static string SendToSelectedStudents => IsUk ? "Надіслати вибраним учням" : "Send to selected students";
    public static string SendToAllOnlineStudents => IsUk ? "Надіслати всім онлайн учням" : "Send to all online students";
    public static string ClearDestinationFolderOnSelectedStudents => IsUk ? "Очистити папку призначення на вибраних учнях" : "Clear destination folder on selected students";
    public static string ClearDestinationFolderOnAllOnlineStudents => IsUk ? "Очистити папку призначення на всіх онлайн учнях" : "Clear destination folder on all online students";
    public static string CreateStudentWorkFolderOnAllAgents => IsUk ? "Створити папку для робіт на всіх ПК" : "Create work folder on all PCs";
    public static string CollectStudentWorkToTeacherPc => IsUk ? "Зібрати роботи учнів на вчительський ПК" : "Collect student work to teacher PC";
    public static string ClearStudentWorkFolderOnAllAgents => IsUk ? "Очистити папку для робіт на всіх ПК" : "Clear work folder on all PCs";
    public static string TeacherPc => IsUk ? "ПК викладача" : "Teacher PC";
    public static string StudentPc => IsUk ? "ПК студента" : "Student PC";
    public static string Up => IsUk ? "Вгору" : "Up";
    public static string SettingsWindowTitle => IsUk ? "Налаштування клієнта викладача" : "Teacher Client Settings";
    public static string SharedSecret => IsUk ? "Спільний секрет" : "Shared secret";
    public static string BulkCopyDestinationPath => IsUk ? "Папка призначення на учнях" : "Student destination folder";
    public static string StudentWorkRootPath => IsUk ? "Базовий шлях робіт на учнях" : "Student work base path";
    public static string StudentWorkFolderName => IsUk ? "Назва папки робіт" : "Student work folder name";
    public static string Language => IsUk ? "Мова" : "Language";
    public static string SettingsHint => IsUk ? "Спільний секрет використовується для перевірки доступності агентів і для всіх API-запитів. Папка призначення визначає стартовий шлях на учнівських ПК для масового копіювання файлів і папок. Базовий шлях і назва папки робіт визначають спільний каталог, який буде створюватися на учнівських ПК для збереження робіт." : "The shared secret is used for reachability checks and all teacher-to-student API calls. The destination folder defines the starting path on student PCs for bulk file and folder distribution. The work base path and work folder name define the shared student folder that will be created on student PCs for saved work.";
    public static string Save => IsUk ? "Зберегти" : "Save";
    public static string Cancel => IsUk ? "Скасувати" : "Cancel";
    public static string Close => IsUk ? "Закрити" : "Close";
    public static string Confirm => IsUk ? "Підтвердження" : "Confirm";
    public static string Ok => "OK";
    public static string SettingsSaved => IsUk ? "Налаштування збережено." : "Settings saved.";
    public static string ManualAgentTitle => IsUk ? "Ручний агент" : "Manual Agent";
    public static string DisplayName => IsUk ? "Назва" : "Display name";
    public static string IpAddress => IsUk ? "IP адреса" : "IP address";
    public static string Port => IsUk ? "Порт" : "Port";
    public static string MacAddress => IsUk ? "MAC адреса" : "MAC address";
    public static string PromptInput => IsUk ? "Ввід" : "Input";
    public static string AboutWindowTitle => IsUk ? "Про TeacherClient.Avalonia" : "About TeacherClient.Avalonia";
    public static string AboutDescription => IsUk ? "TeacherClient.Avalonia — це кросплатформний клієнт для підключення до StudentAgent, перегляду процесів і файлових операцій з macOS, Linux або Windows." : "TeacherClient.Avalonia is the cross-platform desktop client for connecting to StudentAgent, browsing processes, and performing classroom file operations from macOS, Linux, or Windows.";
    public static string Copyright => "Copyright Taras Motuschuk";
    public static string MachineSummary(int total, int discovered, int manual) => IsUk ? $"Доступно агентів: {total} всього, {discovered} знайдено, {manual} вручну" : $"Available agents: {total} total, {discovered} discovered, {manual} manual";
    public static string NoAgentsAvailable => IsUk ? "Немає доступних агентів." : "No agents available.";
    public static string ChooseAgentFirst => IsUk ? "Спочатку виберіть агент." : "Choose an agent first.";
    public static string ChooseAgentsForDistribution => IsUk ? "Виберіть одного або кількох агентів для розсилки." : "Choose one or more agents for distribution.";
    public static string NoOnlineAgentsAvailableForDistribution => IsUk ? "Немає онлайн-агентів для групового копіювання." : "No online agents are available for bulk copy.";
    public static string ConnectionFailed => IsUk ? "Підключення не вдалося." : "Connection failed.";
    public static string ChooseManualAgentFirst => IsUk ? "Спочатку виберіть ручний агент." : "Choose a manual agent first.";
    public static string ManualAgentNotFound => IsUk ? "Ручний агент не знайдено." : "Manual agent not found.";
    public static string RemoveManualAgentTitle => IsUk ? "Видалити ручний агент" : "Remove Manual Agent";
    public static string RemoveManualAgentPrompt(string name) => IsUk ? $"Видалити ручний агент {name}?" : $"Remove manual agent {name}?";
    public static string AddedManualAgent(string name) => IsUk ? $"Додано ручний агент {name}" : $"Added manual agent {name}";
    public static string UpdatedManualAgent(string name) => IsUk ? $"Оновлено ручний агент {name}" : $"Updated manual agent {name}";
    public static string RemovedManualAgent(string name) => IsUk ? $"Видалено ручний агент {name}" : $"Removed manual agent {name}";
    public static string ConnectedToAgent(string source, string machine, string user) => IsUk ? $"Підключено до {source} агента {machine} ({user})" : $"Connected to {source} agent {machine} ({user})";
    public static string ConnectFromAgentsTabFirst => IsUk ? "Спочатку підключіться до агента на вкладці агентів." : "Connect to an agent from the Agents tab first.";
    public static string ChooseProcessFirst => IsUk ? "Спочатку виберіть процес." : "Choose a process first.";
    public static string TerminateProcessTitle => IsUk ? "Завершити процес" : "Terminate Process";
    public static string TerminateProcessPrompt(string name, int id) => IsUk ? $"Завершити процес {name} ({id})?" : $"Terminate process {name} ({id})?";
    public static string ProcessTerminated(string name) => IsUk ? $"Процес {name} завершено" : $"Process {name} terminated";
    public static string LoadedProcesses(int count) => IsUk ? $"Завантажено процесів: {count}" : $"Loaded {count} processes";
    public static string ProcessLoadError => IsUk ? "Помилка завантаження процесів" : "Process load error";
    public static string DiscoveryError => IsUk ? "Помилка пошуку агентів" : "Discovery error";
    public static string PanelsRefreshed => IsUk ? "Панелі оновлено" : "Panels refreshed";
    public static string LocalBrowseError => IsUk ? "Помилка перегляду локальних файлів" : "Local browse error";
    public static string RemoteBrowseError => IsUk ? "Помилка перегляду віддалених файлів" : "Remote browse error";
    public static string RemoteListingFailed => IsUk ? "Не вдалося отримати список віддалених файлів." : "Remote listing failed.";
    public static string ChooseLocalFileToUpload => IsUk ? "Виберіть локальний файл для завантаження." : "Choose a local file to upload.";
    public static string ChooseLocalFileOrFolderToDistribute => IsUk ? "Виберіть локальний файл або папку для розсилки." : "Choose a local file or folder to distribute.";
    public static string DistributionDestinationPathRequired => IsUk ? "У налаштуваннях задайте папку призначення на учнівських ПК." : "Set the student destination folder in settings first.";
    public static string Uploaded(string name) => IsUk ? $"Завантажено {name}" : $"Uploaded {name}";
    public static string UploadError => IsUk ? "Помилка завантаження файлу" : "Upload error";
    public static string BulkCopyError => IsUk ? "Помилка групового копіювання" : "Bulk copy error";
    public static string BulkClearError => IsUk ? "Помилка групового очищення папки" : "Bulk folder clear error";
    public static string BulkCollectError => IsUk ? "Помилка групового збору робіт" : "Bulk work collection error";
    public static string DistributionCompleted(string name, int count) => IsUk ? $"Розіслано {name} на {count} учн. ПК" : $"Distributed {name} to {count} student machines";
    public static string DistributionCompletedWithFailures(string name, int succeeded, int failed) => IsUk ? $"Розсилка {name}: успішно {succeeded}, з помилками {failed}" : $"Distributed {name}: {succeeded} succeeded, {failed} failed";
    public static string BulkCopyResultTitle => IsUk ? "Результат групового копіювання" : "Bulk copy result";
    public static string BulkCommandsResultTitle => IsUk ? "Результат групової команди" : "Group command result";
    public static string PreparingDistributionPlan => IsUk ? "Підготовка плану копіювання..." : "Preparing distribution plan...";
    public static string DistributionProgress(string agent, string item, int agentIndex, int agentCount, int fileIndex, int fileCount)
        => IsUk
            ? $"Розсилка {item} -> {agent} (агент {agentIndex}/{agentCount}, файл {fileIndex}/{fileCount})"
            : $"Distributing {item} -> {agent} (agent {agentIndex}/{agentCount}, file {fileIndex}/{fileCount})";
    public static string NoOnlineAgentsAvailableForGroupCommand => IsUk ? "Немає онлайн-агентів для групової команди." : "No online agents are available for the group command.";
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
    public static string ChooseRemoteFileToDownload => IsUk ? "Виберіть віддалений файл для скачування." : "Choose a remote file to download.";
    public static string Downloaded(string name) => IsUk ? $"Скачано {name}" : $"Downloaded {name}";
    public static string DownloadError => IsUk ? "Помилка скачування файлу" : "Download error";
    public static string ChooseLocalEntryFirst => IsUk ? "Спочатку виберіть локальний елемент." : "Choose a local entry first.";
    public static string DeleteLocalEntryTitle => IsUk ? "Видалити локальний елемент" : "Delete Local Entry";
    public static string DeleteLocalEntryPrompt(string name) => IsUk ? $"Видалити локальний елемент {name}?" : $"Delete local entry {name}?";
    public static string DeletedLocalEntry(string name) => IsUk ? $"Локальний елемент {name} видалено" : $"Deleted local entry {name}";
    public static string LocalDeleteError => IsUk ? "Помилка локального видалення" : "Local delete error";
    public static string ChooseRemoteEntryFirst => IsUk ? "Спочатку виберіть віддалений елемент." : "Choose a remote entry first.";
    public static string DeleteRemoteEntryTitle => IsUk ? "Видалити віддалений елемент" : "Delete Remote Entry";
    public static string DeleteRemoteEntryPrompt(string name) => IsUk ? $"Видалити віддалений елемент {name}?" : $"Delete remote entry {name}?";
    public static string DeletedRemoteEntry(string name) => IsUk ? $"Віддалений елемент {name} видалено" : $"Deleted remote entry {name}";
    public static string RemoteDeleteError => IsUk ? "Помилка віддаленого видалення" : "Remote delete error";
    public static string CreateRemoteFolderTitle => IsUk ? "Створити віддалену папку" : "Create Remote Folder";
    public static string FolderName => IsUk ? "Назва папки:" : "Folder name:";
    public static string DefaultFolderName => IsUk ? "НоваПапка" : "NewFolder";
    public static string CreatedRemoteFolder(string name) => IsUk ? $"Створено віддалену папку {name}" : $"Created remote folder {name}";
    public static string CreateFolderError => IsUk ? "Помилка створення папки" : "Create folder error";
    public static string FooterDescription => IsUk ? "Клієнт Avalonia є кросплатформним і підтримує той самий сценарій пошуку агентів, ручних записів, фільтрів статусу, автоперепідключення та групового копіювання, що й Windows-клієнт." : "Avalonia client is cross-platform and includes the same agent discovery, manual entries, status filtering, auto-reconnect, and bulk copy workflow as the Windows client.";
    public static string DisplayNameRequired => IsUk ? "Назва є обов'язковою." : "Display name is required.";
    public static string IpAddressRequired => IsUk ? "IP адреса є обов'язковою." : "IP address is required.";
    public static string Validation => IsUk ? "Перевірка" : "Validation";
    public static string AutoSource => IsUk ? "Авто" : "Auto";
    public static string ManualSource => IsUk ? "Вручну" : "Manual";
    public static string ManualAutoSource => IsUk ? "Вручну+Авто" : "Manual+Auto";
    public static string ManualVersion => IsUk ? "Вручну" : "Manual";
}
