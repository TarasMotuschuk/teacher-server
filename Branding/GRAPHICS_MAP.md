# Graphics Map

This folder is the single source of truth for user-facing graphics across `ClassCommander`.

## Core App Assets

- `ClassCommander-icon.ico`
  Windows application icon for `TeacherClient`, WinForms dialogs, MSI product icon, and Windows shortcuts.

- `ClassCommander-icon-cropped.png`
  Avalonia window icon source for the cross-platform teacher client.

- `ClassCommander-icon.icns`
  macOS app and package icon used when building the Avalonia `.app` and `.pkg`.

- `ClassCommander-splash.png`
  Splash screen image for the WinForms and Avalonia teacher clients.

## Backgrounds

- `Backgrounds/teacher-about.png`
  Background image for the Windows and Avalonia teacher “About” windows.

- `Backgrounds/student-about.png`
  Background image for the student-side “About” window.

- `Backgrounds/input-lock.png`
  Full-screen background for the student keyboard/mouse lock form.

- `Backgrounds/browser-lock.png`
  Background image for the student browser warning/lock dialog.

## Toolbar: Student PCs

- `Toolbar/agents/pc-refresh-list.png`
  `Оновити список ПК`

- `Toolbar/agents/connect.png`
  `Підключити вибраний ПК`

- `Toolbar/agents/add-manual.png`
  `Додати вручну`

- `Toolbar/agents/edit-manual.png`
  `Редагувати вручну`

- `Toolbar/agents/delete-manual.png`
  `Видалити вручну`

## Toolbar: Processes

- `Toolbar/processes/refresh.png`
  Refresh the process list.

- `Toolbar/processes/stop.png`
  Terminate the selected process.

## Toolbar: Files

- `Toolbar/files/refresh-both.png`
  Refresh both file panels.

- `Toolbar/files/upload.png`
  Upload local file(s) to the current student PC.

- `Toolbar/files/upload-group.png`
  Send files to selected student PCs.

- `Toolbar/files/broadcast.png`
  Send files to all online student PCs.

- `Toolbar/files/download.png`
  Download from the current student PC.

- `Toolbar/files/open-local.png`
  Open a local file/folder with the associated program.

- `Toolbar/files/open-remote.png`
  Open the current path on the student PC.

- `Toolbar/files/rename-local.png`
  Rename a local file or folder.

- `Toolbar/files/rename-remote.png`
  Rename a remote file or folder on the student PC.

- `Toolbar/files/delete-local.png`
  Delete a local file or folder.

- `Toolbar/files/delete-remote.png`
  Delete a remote file or folder on the student PC.

- `Toolbar/files/new-folder.png`
  Create a new folder on the student PC.

## Toolbar: Registry

- `Toolbar/registry/refresh.png`
  Refresh the registry tree and values.

- `Toolbar/registry/new-value.png`
  Create a new registry value.

- `Toolbar/registry/new-key.png`
  Create a new registry key.

- `Toolbar/registry/edit-value.png`
  Edit the selected registry value.

- `Toolbar/registry/delete-value.png`
  Delete the selected registry value.

- `Toolbar/registry/delete-key.png`
  Delete the selected registry key.

- `Toolbar/registry/export-reg.png`
  Export the selected registry key to `.reg`.

- `Toolbar/registry/import-reg.png`
  Import a `.reg` file into the selected registry location.

## Notes

- Toolbar images are expected to be `PNG` with transparent background.
- Recommended toolbar size: `24x24` or `28x28`.
- If a toolbar image is missing, the current code falls back to the built-in generated icon so the UI stays usable.
