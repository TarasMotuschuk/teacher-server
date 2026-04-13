using TeacherClient.Localization;

namespace TeacherClient;

internal sealed class DesktopWallpaperStyleForm : Form
{
    private readonly ComboBox _combo;

    public int SelectedStyle => _combo.SelectedIndex >= 0 ? _combo.SelectedIndex : 4;

    public DesktopWallpaperStyleForm()
    {
        Text = TeacherClientText.WallpaperStyleDialogTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, 110);
        _combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(12, 12),
            Width = 350,
        };

        for (var i = 0; i <= 5; i++)
        {
            _combo.Items.Add(TeacherClientText.WallpaperStyleName(i));
        }

        _combo.SelectedIndex = 4;

        var okButton = new Button
        {
            Text = TeacherClientText.Ok,
            DialogResult = DialogResult.OK,
            Location = new Point(196, 52),
            Width = 80,
        };

        var cancelButton = new Button
        {
            Text = TeacherClientText.Cancel,
            DialogResult = DialogResult.Cancel,
            Location = new Point(282, 52),
            Width = 80,
        };

        AcceptButton = okButton;
        CancelButton = cancelButton;
        Controls.Add(_combo);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
    }
}
