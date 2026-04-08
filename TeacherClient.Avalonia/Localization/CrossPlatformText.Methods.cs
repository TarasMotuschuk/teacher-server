using Teacher.Common.Contracts;
using Teacher.Common.Localization;

namespace TeacherClient.CrossPlatform.Localization;

internal static partial class CrossPlatformText
{
    public static string RemoteManagementRunning(string machine) => IsUk ? $"{machine}: VNC працює" : $"{machine}: VNC running";

    public static string RemoteManagementViewOnly(string machine) => IsUk ? $"{machine}: перегляд без керування" : $"{machine}: view-only";

    public static string RemoteManagementControl(string machine) => IsUk ? $"{machine}: керування увімкнено" : $"{machine}: control enabled";

    public static string RemoteManagementStopped(string machine) => IsUk ? $"{machine}: VNC зупинено" : $"{machine}: VNC stopped";

    public static string RemoteManagementDisabled(string machine) => IsUk ? $"{machine}: VNC вимкнено" : $"{machine}: VNC disabled";

    public static string RemoteManagementConnecting(string machine) => IsUk ? $"{machine}: підключення..." : $"{machine}: connecting...";

    public static string RemoteManagementConnectionFailed(string machine, string message) => IsUk ? $"{machine}: помилка VNC - {message}" : $"{machine}: VNC error - {message}";

    public static string RemoteManagementViewerTitle(string machine) => IsUk ? $"VNC - {machine}" : $"VNC - {machine}";

    public static string DriveFreeSpace(string free, string total) => IsUk ? $"Вільно: {free} / {total}" : $"Free: {free} / {total}";

    public static string StudentPolicySettingsApplied(int succeeded) => IsUk ? $"Policy settings застосовано на {succeeded} онлайн ПК" : $"Applied policy settings to {succeeded} online PCs";

    public static string StudentPolicySettingsAppliedWithFailures(int succeeded, int failures) => IsUk ? $"Policy settings застосовано: успішно {succeeded}, з помилками {failures}" : $"Applied policy settings: {succeeded} succeeded, {failures} failed";

    public static string MachineSummary(int total, int discovered, int manual) => IsUk ? $"Доступно агентів: {total} всього, {discovered} знайдено, {manual} вручну" : $"Available agents: {total} total, {discovered} discovered, {manual} manual";

    public static string MachineSummaryWithConnected(int total, int discovered, int manual, string machine)
        => IsUk
            ? $"Доступно агентів: {total} всього, {discovered} знайдено, {manual} вручну. Підключено: {machine}"
            : $"Available agents: {total} total, {discovered} discovered, {manual} manual. Connected: {machine}";

    public static string WindowsRestrictionName(WindowsRestrictionKind restriction)
        => restriction switch
        {
            WindowsRestrictionKind.TaskManager => IsUk ? "Диспетчер задач" : "Task Manager",
            WindowsRestrictionKind.RunDialog => IsUk ? "Вікно Виконати" : "Run dialog",
            WindowsRestrictionKind.ControlPanelAndSettings => IsUk ? "Панель керування і Параметри" : "Control Panel and Settings",
            WindowsRestrictionKind.LockWorkstation => IsUk ? "Блокування робочої станції" : "Lock workstation",
            WindowsRestrictionKind.ChangePassword => IsUk ? "Зміна пароля" : "Change password",
            WindowsRestrictionKind.LogOff => IsUk ? "Вихід з облікового запису" : "Log off",
            _ => restriction.ToString(),
        };

    public static string WindowsRestrictionPrompt(WindowsRestrictionKind restriction, bool enabled, int count)
        => IsUk
            ? $"{(enabled ? "Увімкнути" : "Вимкнути")} обмеження \"{WindowsRestrictionName(restriction)}\" на {count} онлайн учнівських ПК?"
            : $"{(enabled ? "Enable" : "Disable")} the \"{WindowsRestrictionName(restriction)}\" restriction on {count} online student PCs?";

    public static string WindowsRestrictionProgress(string machine, int index, int total, WindowsRestrictionKind restriction, bool enabled)
        => IsUk
            ? $"{(enabled ? "Увімкнення" : "Вимкнення")} обмеження \"{WindowsRestrictionName(restriction)}\": {machine} ({index}/{total})"
            : $"{(enabled ? "Enabling" : "Disabling")} \"{WindowsRestrictionName(restriction)}\": {machine} ({index}/{total})";

    public static string WindowsRestrictionCompleted(WindowsRestrictionKind restriction, bool enabled, int succeeded)
        => IsUk
            ? $"{(enabled ? "Увімкнено" : "Вимкнено")} обмеження \"{WindowsRestrictionName(restriction)}\" на {succeeded} ПК"
            : $"{(enabled ? "Enabled" : "Disabled")} \"{WindowsRestrictionName(restriction)}\" on {succeeded} PCs";

    public static string WindowsRestrictionCompletedWithFailures(WindowsRestrictionKind restriction, bool enabled, int succeeded, int failures)
        => IsUk
            ? $"{(enabled ? "Увімкнення" : "Вимкнення")} \"{WindowsRestrictionName(restriction)}\": успішно {succeeded}, з помилками {failures}"
            : $"{(enabled ? "Enabled" : "Disabled")} \"{WindowsRestrictionName(restriction)}\": {succeeded} succeeded, {failures} failed";

    public static string BrowserLockEnabledFor(string machine) => IsUk ? $"Блокування браузера увімкнено на {machine}" : $"Browser lock enabled on {machine}";

    public static string BrowserLockDisabledFor(string machine) => IsUk ? $"Блокування браузера вимкнено на {machine}" : $"Browser lock disabled on {machine}";

    public static string InputLockEnabledFor(string machine) => IsUk ? $"Блокування клавіатури і миші увімкнено на {machine}" : $"Keyboard and mouse lock enabled on {machine}";

    public static string InputLockDisabledFor(string machine) => IsUk ? $"Блокування клавіатури і миші вимкнено на {machine}" : $"Keyboard and mouse lock disabled on {machine}";

    public static string RemoveManualAgentPrompt(string name) => IsUk ? $"Видалити ручний агент {name}?" : $"Remove manual agent {name}?";

    public static string AddedManualAgent(string name) => IsUk ? $"Додано ручний агент {name}" : $"Added manual agent {name}";

    public static string UpdatedManualAgent(string name) => IsUk ? $"Оновлено ручний агент {name}" : $"Updated manual agent {name}";

    public static string RemovedManualAgent(string name) => IsUk ? $"Видалено ручний агент {name}" : $"Removed manual agent {name}";

    public static string ConnectedToAgent(string source, string machine, string user, string version) => IsUk ? $"Підключено до {source} агента {machine} ({user})  v{version}" : $"Connected to {source} agent {machine} ({user})  v{version}";

    public static string DesktopIconLayoutSaved(string machine, int count) => IsUk ? $"{machine}: розкладку іконок збережено ({count})" : $"{machine}: desktop icon layout saved ({count})";

    public static string DesktopIconLayoutRestored(string machine, int count) => IsUk ? $"{machine}: розкладку іконок відновлено ({count})" : $"{machine}: desktop icon layout restored ({count})";

    public static string DesktopIconLayoutBulkProgress(string machine, int index, int total) => IsUk ? $"Відновлення іконок: {machine} ({index}/{total})" : $"Restoring desktop icons: {machine} ({index}/{total})";

    public static string DesktopIconLayoutBulkCompleted(int succeeded) => IsUk ? $"Розкладку іконок відновлено на {succeeded} ПК" : $"Restored desktop icons on {succeeded} PCs";

    public static string DesktopIconLayoutBulkCompletedWithFailures(int succeeded, int failures) => IsUk ? $"Розкладку іконок відновлено: успішно {succeeded}, з помилками {failures}" : $"Restored desktop icons: {succeeded} succeeded, {failures} failed";

    public static string DesktopIconLayoutApplyBulkProgress(string machine, int index, int total) => IsUk ? $"Передача layout іконок: {machine} ({index}/{total})" : $"Applying desktop icon layout: {machine} ({index}/{total})";

    public static string DesktopIconLayoutAppliedBulkCompleted(int succeeded) => IsUk ? $"Layout іконок передано на {succeeded} ПК" : $"Applied desktop icon layout to {succeeded} PCs";

    public static string DesktopIconLayoutAppliedBulkCompletedWithFailures(int succeeded, int failures) => IsUk ? $"Layout іконок передано: успішно {succeeded}, з помилками {failures}" : $"Applied desktop icon layout: {succeeded} succeeded, {failures} failed";

    public static string UpdatePreparationReady(string version) => IsUk ? $"Пакет {version} підготовлено" : $"Update package {version} is ready";

    public static string UpdatePreparationManualHint(string manifestUrl, string manualDirectory) => IsUk
        ? $"Якщо немає інтернету: завантажте `student-agent-version.json` і відповідний `student-agent-update-<version>.zip` з {manifestUrl} або GitHub Releases та покладіть їх у папку `{manualDirectory}`."
        : $"If the teacher PC has no internet access, download `student-agent-version.json` and the matching `student-agent-update-<version>.zip` from {manifestUrl} or GitHub Releases, then place them in `{manualDirectory}`.";

    public static string ClientUpdateCurrentVersion(string version) => IsUk ? $"Поточна версія клієнта: {version}" : $"Current client version: {version}";

    public static string ClientUpdateAvailable(string version, string platform) => IsUk ? $"Доступне оновлення {version} для {platform}" : $"Update {version} is available for {platform}";

    public static string ClientAlreadyUpToDate(string version) => IsUk ? $"Клієнт уже на актуальній версії {version}" : $"Client is already on version {version}";

    public static string ClientUpdateReady(string version) => IsUk ? $"Інсталятор {version} готовий до запуску" : $"Installer {version} is ready to launch";

    public static string ClientUpdateInstallerOpened(string path) => IsUk ? $"Інсталятор відкрито: {path}" : $"Installer opened: {path}";

    public static string ClientUpdateHint(string manifestUrl, string downloadDirectory) => IsUk
        ? $"Клієнт завантажує інсталятор з {manifestUrl}. Після завантаження файл зберігається в `{downloadDirectory}` і відкривається системним інсталятором."
        : $"The client downloads its installer from {manifestUrl}. After download, the file is stored in `{downloadDirectory}` and opened with the system installer.";

    public static string AgentUpToDate(string machine, string version) => IsUk ? $"{machine}: актуальна версія {version}" : $"{machine}: already on version {version}";

    public static string AgentUpdateAvailable(string machine, string version) => IsUk ? $"{machine}: доступне оновлення {version}" : $"{machine}: update {version} is available";

    public static string AgentUpdateStarted(string machine, string version) => IsUk ? $"{machine}: запущено оновлення до {version}" : $"{machine}: started update to {version}";

    public static string AgentUpdateState(string machine, string state, string? message) => IsUk
        ? $"{machine}: стан оновлення {state}{(string.IsNullOrWhiteSpace(message) ? string.Empty : $" ({message})")}"
        : $"{machine}: update state {state}{(string.IsNullOrWhiteSpace(message) ? string.Empty : $" ({message})")}";

    public static string BulkAgentUpdatePrompt(int count, bool selectedOnly) => IsUk
        ? $"Запустити оновлення на {(selectedOnly ? "вибраних" : "всіх онлайн")} учнівських ПК ({count})?"
        : $"Start the update on {(selectedOnly ? "selected" : "all online")} student PCs ({count})?";

    public static string BulkAgentUpdateProgress(string machine, int index, int total) => IsUk
        ? $"Оновлення агента: {machine} ({index}/{total})"
        : $"Updating agent: {machine} ({index}/{total})";

    public static string BulkAgentUpdateCompleted(int succeeded) => IsUk
        ? $"Оновлення запущено на {succeeded} учн. ПК"
        : $"Started updates on {succeeded} student PCs";

    public static string BulkAgentUpdateCompletedWithFailures(int succeeded, int failures) => IsUk
        ? $"Оновлення запущено: успішно {succeeded}, з помилками {failures}"
        : $"Started updates: {succeeded} succeeded, {failures} failed";

    public static string FormatUpdateStatusDetail(AgentUpdateStatusDto? status)
    {
        if (status is null || status.State != AgentUpdateStateKind.Failed)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(status.Message) ? string.Empty : status.Message.Trim();
    }

    public static string UpdateStateBadge(AgentUpdateStatusDto? status)
    {
        if (status is null)
        {
            return string.Empty;
        }

        var stateText = status.State switch
        {
            AgentUpdateStateKind.Checking => IsUk ? "Перевірка" : "Checking",
            AgentUpdateStateKind.UpToDate => IsUk ? "Актуально" : "Up to date",
            AgentUpdateStateKind.Available => IsUk ? "Доступно" : "Available",
            AgentUpdateStateKind.Downloading => IsUk ? "Завантаження" : "Downloading",
            AgentUpdateStateKind.Installing => IsUk ? "Встановлення" : "Installing",
            AgentUpdateStateKind.Succeeded => IsUk ? "Оновлено" : "Updated",
            AgentUpdateStateKind.Failed => IsUk ? "Помилка" : "Failed",
            AgentUpdateStateKind.RolledBack => IsUk ? "Відкат" : "Rolled back",
            _ => string.Empty,
        };

        if (string.IsNullOrWhiteSpace(stateText))
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(status.AvailableVersion)
            ? stateText
            : $"{stateText} {status.AvailableVersion}";
    }

    public static string LoadedRegistryValues(int count) => IsUk ? $"Значень у ключі: {count}" : $"Values in key: {count}";

    public static string ExportedRegistryKey(string path) => IsUk ? $"Експортовано ключ реєстру {path}" : $"Exported registry key {path}";

    public static string ImportedRegistryFile(int keys, int values) => IsUk ? $"Імпортовано .reg: ключів {keys}, значень {values}" : $"Imported .reg file: {keys} keys, {values} values";

    public static string TerminateProcessPrompt(string name, int id) => IsUk ? $"Завершити процес {name} ({id})?" : $"Terminate process {name} ({id})?";

    public static string ProcessTerminated(string name) => IsUk ? $"Процес {name} завершено" : $"Process {name} terminated";

    public static string RestartProcessPrompt(string name, int id) => IsUk ? $"Перезапустити процес {name} ({id})?" : $"Restart process {name} ({id})?";

    public static string ProcessRestarted(string name) => IsUk ? $"Процес {name} перезапущено" : $"Process {name} restarted";

    public static string LoadedProcesses(int count) => IsUk ? $"Завантажено процесів: {count}" : $"Loaded {count} processes";

    public static string Uploaded(string name) => IsUk ? $"Завантажено {name}" : $"Uploaded {name}";

    public static string DistributionCompleted(string name, int count) => IsUk ? $"Розіслано {name} на {count} учн. ПК" : $"Distributed {name} to {count} student machines";

    public static string DistributionCompletedWithFailures(string name, int succeeded, int failed) => IsUk ? $"Розсилка {name}: успішно {succeeded}, з помилками {failed}" : $"Distributed {name}: {succeeded} succeeded, {failed} failed";

    public static string DistributionProgress(string agent, string item, int agentIndex, int agentCount, int fileIndex, int fileCount)
        => IsUk
            ? $"Розсилка {item} -> {agent} (агент {agentIndex}/{agentCount}, файл {fileIndex}/{fileCount})"
            : $"Distributing {item} -> {agent} (agent {agentIndex}/{agentCount}, file {fileIndex}/{fileCount})";

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

    public static string OpenedLocal(string name) => IsUk ? $"Відкрито локально: {name}" : $"Opened locally: {name}";

    public static string OpenedRemote(string name) => IsUk ? $"Відкрито на учнівському ПК: {name}" : $"Opened on student PC: {name}";

    public static string Downloaded(string name) => IsUk ? $"Скачано {name}" : $"Downloaded {name}";

    public static string DeleteLocalEntryPrompt(string name) => IsUk ? $"Видалити локальний елемент {name}?" : $"Delete local entry {name}?";

    public static string RenamedLocalEntry(string oldName, string newName) => IsUk ? $"Локальний елемент {oldName} перейменовано на {newName}" : $"Renamed local entry {oldName} to {newName}";

    public static string DeletedLocalEntry(string name) => IsUk ? $"Локальний елемент {name} видалено" : $"Deleted local entry {name}";

    public static string DeleteRemoteEntryPrompt(string name) => IsUk ? $"Видалити віддалений елемент {name}?" : $"Delete remote entry {name}?";

    public static string RenamedRemoteEntry(string oldName, string newName) => IsUk ? $"Віддалений елемент {oldName} перейменовано на {newName}" : $"Renamed remote entry {oldName} to {newName}";

    public static string DeletedRemoteEntry(string name) => IsUk ? $"Віддалений елемент {name} видалено" : $"Deleted remote entry {name}";

    public static string CreatedRemoteFolder(string name) => IsUk ? $"Створено віддалену папку {name}" : $"Created remote folder {name}";

    public static void SetLanguage(UiLanguage language)
    {
        CurrentLanguage = language.Normalize();
    }

    private static string GetPowerActionVerb(PowerActionKind action) => action switch
    {
        PowerActionKind.Shutdown => IsUk ? "Вимкнути" : "Shut down",
        PowerActionKind.Restart => IsUk ? "Перезавантажити" : "Restart",
        PowerActionKind.LogOff => IsUk ? "Вивести з облікового запису" : "Log off",
        _ => IsUk ? "Виконати дію для" : "Run action for",
    };

    private static string GetPowerActionPast(PowerActionKind action) => action switch
    {
        PowerActionKind.Shutdown => IsUk ? "Надіслано вимкнення для" : "Sent shut down to",
        PowerActionKind.Restart => IsUk ? "Надіслано перезавантаження для" : "Sent restart to",
        PowerActionKind.LogOff => IsUk ? "Надіслано вихід з облікового запису для" : "Sent log off to",
        _ => IsUk ? "Виконано дію для" : "Ran action for",
    };

    private static string GetPowerActionNoun(PowerActionKind action) => action switch
    {
        PowerActionKind.Shutdown => IsUk ? "Вимкнення" : "Shutdown",
        PowerActionKind.Restart => IsUk ? "Перезавантаження" : "Restart",
        PowerActionKind.LogOff => IsUk ? "Вихід з облікового запису" : "Log off",
        _ => IsUk ? "Команда живлення" : "Power command",
    };
}
