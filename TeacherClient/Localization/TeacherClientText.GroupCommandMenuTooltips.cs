namespace TeacherClient.Localization;

internal static partial class TeacherClientText
{
    public static string MenuTip_GroupFileWork => IsUk
        ? "Надсилання файлів і папок на учнівські ПК, робота з папкою призначення."
        : "Send files and folders to student PCs and manage the bulk-copy destination folder.";

    public static string MenuTip_DestinationFolder => IsUk
        ? "Дії з папкою призначення на учнівських ПК (куди надсилаються файли за налаштуваннями)."
        : "Actions on the destination folder on student PCs (where bulk copies go per settings).";

    public static string MenuTip_ClearDestinationSelected => IsUk
        ? "Очистити вміст папки призначення на ПК, позначених у колонці «Вибір»."
        : "Clear the destination folder contents on PCs marked in the Select column.";

    public static string MenuTip_ClearDestinationAll => IsUk
        ? "Очистити папку призначення на всіх онлайн учнівських ПК."
        : "Clear the destination folder on all online student PCs.";

    public static string MenuTip_SendSubmenu => IsUk
        ? "Надіслати файл або папку з цього ПК на вибрані або всі онлайн учнівські ПК."
        : "Send a file or folder from this PC to selected or all online student PCs.";

    public static string MenuTip_SendFile => IsUk
        ? "Надіслати один обраний файл."
        : "Send a single selected file.";

    public static string MenuTip_SendFolder => IsUk
        ? "Надіслати обрану папку з усім вмістом."
        : "Send a selected folder with its contents.";

    public static string MenuTip_ToAllPcs => IsUk
        ? "Застосувати до всіх учнівських ПК у статусі «Онлайн»."
        : "Apply to all student PCs that are online.";

    public static string MenuTip_ToSelectedPcs => IsUk
        ? "Застосувати лише до ПК, позначених у колонці «Вибір»."
        : "Apply only to PCs marked in the Select column.";

    public static string MenuTip_SendAndOpenDefault => IsUk
        ? "Надіслати файл або папку, потім відкрити на учнівському ПК у програмі за замовчуванням."
        : "Send a file or folder, then open it on the student PC with the default application.";

    public static string MenuTip_SendAndOpenDestFolder => IsUk
        ? "Надіслати дані, потім відкрити папку призначення (куди потрапили файли) на учнівському ПК."
        : "Send data, then open the destination folder on the student PC.";

    public static string MenuTip_Blocking => IsUk
        ? "Блокування браузера та клавіатури/миші на учнівських ПК."
        : "Browser lock and keyboard/mouse lock on student PCs.";

    public static string MenuTip_LockBrowsersAll => IsUk
        ? "Заблокувати браузер на всіх онлайн учнівських ПК."
        : "Lock the browser on all online student PCs.";

    public static string MenuTip_BlockingSelectedGroup => IsUk
        ? "Команди блокування для ПК, позначених у колонці «Вибір»."
        : "Blocking commands for PCs marked in the Select column.";

    public static string MenuTip_LockInputSelected => IsUk
        ? "Заблокувати клавіатуру та мишу на позначених ПК."
        : "Lock keyboard and mouse on marked PCs.";

    public static string MenuTip_LockInputDemoSelected => IsUk
        ? "Режим демонстрації: банер поверх екрана на позначених ПК."
        : "Demonstration mode: banner overlay on marked PCs.";

    public static string MenuTip_UnlockInputSelected => IsUk
        ? "Зняти блокування клавіатури та миші на позначених ПК."
        : "Remove keyboard and mouse lock on marked PCs.";

    public static string MenuTip_BlockingAllOnlineGroup => IsUk
        ? "Команди блокування для всіх онлайн учнівських ПК."
        : "Blocking commands for all online student PCs.";

    public static string MenuTip_LockInputAll => IsUk
        ? "Заблокувати клавіатуру та мишу на всіх онлайн ПК."
        : "Lock keyboard and mouse on all online student PCs.";

    public static string MenuTip_LockInputDemoAll => IsUk
        ? "Увімкнути демонстраційне блокування введення на всіх онлайн ПК."
        : "Enable demonstration input lock on all online student PCs.";

    public static string MenuTip_UnlockInputAll => IsUk
        ? "Зняти блокування клавіатури та миші на всіх онлайн ПК."
        : "Remove keyboard and mouse lock on all online student PCs.";

    public static string MenuTip_GroupPolicies => IsUk
        ? "Застосування обмежень Windows (як групові політики) на учнівських ПК через службу агента."
        : "Apply Windows-style policy restrictions on student PCs via the agent service.";

    public static string MenuTip_RestrictionTaskManager => IsUk
        ? "Обмежити відкриття диспетчера задач (Task Manager)."
        : "Restrict opening Task Manager.";

    public static string MenuTip_RestrictionRunDialog => IsUk
        ? "Обмежити вікно «Виконати» (Win+R)."
        : "Restrict the Run dialog (Win+R).";

    public static string MenuTip_RestrictionControlPanel => IsUk
        ? "Обмежити доступ до панелі керування та параметрів Windows."
        : "Restrict access to Control Panel and Windows Settings.";

    public static string MenuTip_RestrictionLockWorkstation => IsUk
        ? "Обмежити блокування робочої станції (Win+L тощо)."
        : "Restrict locking the workstation (e.g. Win+L).";

    public static string MenuTip_RestrictionChangePassword => IsUk
        ? "Обмежити зміну пароля облікового запису."
        : "Restrict changing the account password.";

    public static string MenuTip_RestrictionBlockInterface => IsUk
        ? "Заборонити зміну теми, кольорів, стилю вікон, значків робочого столу, вказівника миші та заставки (політики персоналізації Windows)."
        : "Prevent changes to theme, colors, window styles, desktop icons, mouse pointers, and screen saver (Windows personalization policies).";

    public static string MenuTip_DesktopWallpaperMenu => IsUk
        ? "Копіює вибране зображення на учнівський ПК і вмикає політики Desktop Wallpaper та заборону зміни тла."
        : "Copies the chosen image to the student PC and enables Desktop Wallpaper plus prevent changing background policies.";

    public static string MenuTip_DesktopWallpaperAll => IsUk
        ? "Встановити тло для всіх онлайн учнівських ПК."
        : "Set the desktop background on all online student PCs.";

    public static string MenuTip_DesktopWallpaperSelected => IsUk
        ? "Встановити тло лише для ПК, позначених у колонці «Вибір»."
        : "Set the desktop background only for PCs marked in the Select column.";

    public static string MenuTip_EnableRestrictionAllOnline => IsUk
        ? "Увімкнути це обмеження на всіх онлайн учнівських ПК."
        : "Enable this restriction on all online student PCs.";

    public static string MenuTip_DisableRestrictionAllOnline => IsUk
        ? "Вимкнути це обмеження на всіх онлайн учнівських ПК."
        : "Disable this restriction on all online student PCs.";

    public static string MenuTip_Commands => IsUk
        ? "Віддалене виконання команд та робота зі списком частих програм."
        : "Run remote commands and manage the frequent programs list.";

    public static string MenuTip_RunCommandSelected => IsUk
        ? "Виконати команду на ПК, позначених у колонці «Вибір»."
        : "Run a command on PCs marked in the Select column.";

    public static string MenuTip_RunCommandAll => IsUk
        ? "Виконати команду на всіх онлайн учнівських ПК."
        : "Run a command on all online student PCs.";

    public static string MenuTip_RefreshFrequentPrograms => IsUk
        ? "Оновити список частих програм з усіх онлайн ПК."
        : "Refresh the frequent programs list from all online PCs.";

    public static string MenuTip_ManageFrequentPrograms => IsUk
        ? "Редагувати збережений список частих програм для швидкого запуску."
        : "Edit the saved frequent programs list for quick launch.";

    public static string MenuTip_DesktopIconsCmd => IsUk
        ? "Відновлення та розсилка розкладки значків робочого столу."
        : "Restore and push desktop icon layouts.";

    public static string MenuTip_SaveDesktopIconsCurrentPc => IsUk
        ? "Зберегти розкладку значків на ПК, до якого зараз підключено (вкладка «Віддалено»)."
        : "Save the desktop icon layout on the PC you are currently connected to (Remote tab).";

    public static string MenuTip_RestoreDesktopIconsCurrentPc => IsUk
        ? "Відновити збережену розкладку значків на ПК, до якого зараз підключено."
        : "Restore the saved icon layout on the PC you are currently connected to.";

    public static string MenuTip_RestoreIconsSelected => IsUk
        ? "Відновити збережений розклад значків на позначених ПК."
        : "Restore the saved icon layout on marked PCs.";

    public static string MenuTip_RestoreIconsAll => IsUk
        ? "Відновити збережений розклад значків на всіх онлайн ПК."
        : "Restore the saved icon layout on all online PCs.";

    public static string MenuTip_ApplyLayoutSelected => IsUk
        ? "Надіслати поточний розклад значків з цього ПК на позначені ПК."
        : "Send the current desktop icon layout from this PC to marked PCs.";

    public static string MenuTip_ApplyLayoutAll => IsUk
        ? "Надіслати поточний розклад значків з цього ПК на всі онлайн ПК."
        : "Send the current desktop icon layout from this PC to all online PCs.";

    public static string MenuTip_Power => IsUk
        ? "Завершення роботи, перезавантаження або вихід з облікового запису на віддалених ПК."
        : "Shut down, restart, or log off remote PCs.";

    public static string MenuTip_PowerSelectedGroup => IsUk
        ? "Дії живлення для ПК, позначених у колонці «Вибір»."
        : "Power actions for PCs marked in the Select column.";

    public static string MenuTip_PowerAllGroup => IsUk
        ? "Дії живлення для всіх онлайн учнівських ПК."
        : "Power actions for all online student PCs.";

    public static string MenuTip_Shutdown => IsUk
        ? "Завершити роботу Windows на цільових ПК."
        : "Shut down Windows on the target PCs.";

    public static string MenuTip_Restart => IsUk
        ? "Перезавантажити цільові ПК."
        : "Restart the target PCs.";

    public static string MenuTip_LogOff => IsUk
        ? "Вийти з поточного сеансу користувача на цільових ПК."
        : "Log off the current user session on the target PCs.";

    public static string MenuTip_StudentWork => IsUk
        ? "Папка для робіт учнів: створення, збір на ПК вчителя, очищення."
        : "Student work folder: create, collect to teacher PC, clear.";

    public static string MenuTip_CreateWorkFolder => IsUk
        ? "Створити налаштовану папку для робіт на всіх онлайн ПК."
        : "Create the configured work folder on all online PCs.";

    public static string MenuTip_CollectWork => IsUk
        ? "Скопіювати роботи учнів з їхніх ПК на цей комп’ютер."
        : "Copy student work from their PCs to this computer.";

    public static string MenuTip_ClearWorkFolder => IsUk
        ? "Очистити папку для робіт на всіх онлайн учнівських ПК."
        : "Clear the work folder on all online student PCs.";
}
