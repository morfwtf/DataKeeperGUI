using System.Security.Cryptography;
using System.Text;

namespace DataKeeperGUI;

public partial class Form1 : Form
{
    private readonly NoteStore store = new();
    private readonly ListBox notes = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox editor = new()
    {
        Dock = DockStyle.Fill, Multiline = true, AcceptsReturn = true, AcceptsTab = true,
        ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 11)
    };
    private readonly TextBox name = new() { Dock = DockStyle.Fill, PlaceholderText = "Имя заметки" };
    private readonly Label folderLabel = new() { AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button saveButton = new() { Text = "Сохранить", AutoSize = true };
    private readonly Button themeButton = new() { AutoSize = true };
    private string? currentPath;
    private string? currentPassword;
    private bool editing;
    private bool dirty;

    public Form1()
    {
        InitializeComponent();
        Text = "DataKeeper — защищённые заметки";
        MinimumSize = new Size(760, 500);
        Size = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BuildInterface();
        ApplyTheme();
        UpdateFolder();
        Safe(RefreshNotes);
        editor.TextChanged += (_, _) => { if (editing) { dirty = true; UpdateStatus(); } };
        name.TextChanged += (_, _) => { if (editing) { dirty = true; UpdateStatus(); } };
        FormClosing += (_, e) => { if (!CanDiscard()) e.Cancel = true; };
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        Controls.Add(root);

        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        top.Controls.Add(ActionButton("Выбрать папку", ChooseFolder));
        top.Controls.Add(ActionButton("Создать папку", CreateFolder));
        themeButton.Click += (_, _) => { ThemeManager.Toggle(); ApplyTheme(); };
        top.Controls.Add(themeButton);
        folderLabel.Margin = new Padding(14, 9, 0, 0);
        top.Controls.Add(folderLabel);
        root.Controls.Add(top, 0, 0);

        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1000, 550), SplitterDistance = 270 };
        root.Controls.Add(split, 0, 1);
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        left.Controls.Add(new Label { Text = "Заметки", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        left.Controls.Add(notes, 0, 1);
        var leftButtons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        leftButtons.Controls.Add(ActionButton("Открыть", OpenSelected));
        leftButtons.Controls.Add(ActionButton("Удалить", DeleteSelected));
        leftButtons.Controls.Add(ActionButton("Сменить пароль", ChangePassword));
        leftButtons.Controls.Add(ActionButton("Обновить", () => Safe(RefreshNotes)));
        left.Controls.Add(leftButtons, 0, 2);
        split.Panel1.Controls.Add(left);
        notes.DoubleClick += (_, _) => OpenSelected();

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        right.Controls.Add(name, 0, 0);
        right.Controls.Add(editor, 0, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        saveButton.Click += (_, _) => SaveNote();
        actions.Controls.Add(saveButton);
        actions.Controls.Add(ActionButton("Новая заметка", NewNote));
        actions.Controls.Add(ActionButton("Закрыть заметку", CloseNote));
        right.Controls.Add(actions, 0, 2);
        split.Panel2.Controls.Add(right);

        status.Text = "Выберите папку заметок, чтобы начать.";
        root.Controls.Add(status, 0, 2);
        SetEditorEnabled(false);
    }

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(3, 3, 8, 3) };
        button.Click += (_, _) => action();
        return button;
    }

    private void ChooseFolder()
    {
        if (!CanDiscard()) return;
        using var dialog = new FolderBrowserDialog { Description = "Выберите папку зашифрованных заметок" };
        if (store.Folder is { } folder) dialog.InitialDirectory = folder;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        Safe(() => { store.SelectFolder(dialog.SelectedPath); ClearEditor(); UpdateFolder(); RefreshNotes(); UpdateStatus(); });
    }

    private void CreateFolder()
    {
        if (!CanDiscard()) return;
        using var dialog = new FolderBrowserDialog { Description = "Где создать папку для заметок?" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string[]? values = InputDialog.Show(this, "Новая папка", ("Название папки", false));
        if (values is null) return;
        Safe(() => { store.CreateFolder(dialog.SelectedPath, values[0].Trim()); ClearEditor(); UpdateFolder(); RefreshNotes(); UpdateStatus(); });
    }

    private void NewNote()
    {
        if (store.Folder is null) { ShowError("Сначала выберите папку заметок."); return; }
        if (!CanDiscard()) return;
        ClearEditor();
        editing = true;
        SetEditorEnabled(true);
        status.Text = "Новая заметка. Укажите имя, введите текст и сохраните.";
        name.Focus();
    }

    private void OpenSelected()
    {
        if (notes.SelectedItem is not NoteItem item || !CanDiscard()) return;
        string[]? values = InputDialog.Show(this, "Открыть заметку", ("Пароль", true));
        if (values is null) return;
        Safe(() =>
        {
            if (!store.TryRead(item.Path, values[0], out string content))
            {
                ShowError("Неверный пароль или повреждённый файл.");
                return;
            }
            ClearEditor();
            currentPath = item.Path;
            currentPassword = values[0];
            editing = true;
            SetEditorEnabled(true);
            name.Text = Path.GetFileNameWithoutExtension(item.Path);
            name.ReadOnly = true;
            editor.Text = content;
            dirty = false;
            UpdateStatus();
            editor.Focus();
        });
    }

    private void SaveNote()
    {
        if (!editing) return;
        Safe(() =>
        {
            bool isNew = currentPath is null;
            string path = isNew ? store.PathFor(name.Text) : currentPath!;
            string? password = currentPassword;
            if (isNew)
            {
                string[]? values = InputDialog.Show(this, "Пароль новой заметки",
                    ("Пароль", true), ("Повторите пароль", true));
                if (values is null) return;
                if (string.IsNullOrEmpty(values[0]) || values[0] != values[1])
                {
                    ShowError("Пароли не совпадают или пусты.");
                    return;
                }
                password = values[0];
            }
            store.Write(path, editor.Text, password!, isNew);
            currentPath = path;
            currentPassword = password;
            name.ReadOnly = true;
            dirty = false;
            RefreshNotes();
            UpdateStatus();
        });
    }

    private void DeleteSelected()
    {
        if (notes.SelectedItem is not NoteItem item) return;
        string[]? values = InputDialog.Show(this, "Удаление заметки", ("Пароль заметки", true));
        if (values is null) return;
        Safe(() =>
        {
            if (!store.TryRead(item.Path, values[0], out _))
            {
                ShowError("Неверный пароль или повреждённый файл.");
                return;
            }
            if (MessageBox.Show(this, $"Удалить «{item.Name}»? Это действие нельзя отменить.",
                    "Удаление заметки", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            if (currentPath == item.Path && !CanDiscard()) return;
            File.Delete(item.Path);
            if (currentPath == item.Path) ClearEditor();
            RefreshNotes();
        });
    }

    private void ChangePassword()
    {
        if (notes.SelectedItem is not NoteItem item) return;
        string[]? values = InputDialog.Show(this, "Смена пароля",
            ("Старый пароль", true), ("Новый пароль", true), ("Повторите новый пароль", true));
        if (values is null) return;
        if (string.IsNullOrEmpty(values[1]) || values[1] != values[2])
        {
            ShowError("Новые пароли не совпадают или пусты.");
            return;
        }
        Safe(() =>
        {
            if (!store.TryRead(item.Path, values[0], out string content))
            {
                ShowError("Старый пароль неверный или файл повреждён.");
                return;
            }
            if (currentPath == item.Path && dirty)
            {
                ShowError("Сначала сохраните или закройте изменённую заметку.");
                return;
            }
            store.Write(item.Path, content, values[1], false);
            if (currentPath == item.Path) currentPassword = values[1];
            status.Text = "Пароль изменён.";
        });
    }

    private void CloseNote() { if (CanDiscard()) ClearEditor(); }

    private bool CanDiscard() => !dirty || MessageBox.Show(this,
        "Изменения не сохранены. Продолжить без сохранения?", "DataKeeper",
        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

    private void ClearEditor()
    {
        editing = false;
        dirty = false;
        currentPath = null;
        currentPassword = null;
        name.Text = string.Empty;
        editor.Clear();
        SetEditorEnabled(false);
        UpdateStatus();
    }

    private void SetEditorEnabled(bool enabled)
    {
        name.ReadOnly = !enabled || currentPath is not null;
        editor.ReadOnly = !enabled;
        name.TabStop = enabled;
        editor.TabStop = enabled;
        saveButton.Enabled = enabled;
    }

    private void ApplyTheme()
    {
        ThemeManager.Apply(this);
        themeButton.Text = ThemeManager.IsDark ? "Светлая тема" : "Тёмная тема";
    }

    private void UpdateFolder() => folderLabel.Text = store.Folder is null
        ? "Папка не выбрана" : $"Папка: {store.Folder}";

    private void RefreshNotes()
    {
        notes.Items.Clear();
        if (store.Folder is null || !Directory.Exists(store.Folder)) return;
        foreach (string path in store.ListNotes()) notes.Items.Add(new NoteItem(path));
    }

    private void UpdateStatus() => status.Text = editing
        ? (currentPath is null ? "Новая заметка" : $"Открыта: {Path.GetFileName(currentPath)}") +
          (dirty ? " • есть несохранённые изменения" : "")
        : store.Folder is null ? "Выберите папку заметок, чтобы начать." : "Выберите заметку или создайте новую.";

    private void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            System.Security.Cryptography.CryptographicException or ArgumentException)
        { ShowError(ex.Message); }
    }

    private void ShowError(string message) => MessageBox.Show(this, message, "DataKeeper",
        MessageBoxButtons.OK, MessageBoxIcon.Error);

    private sealed class NoteItem(string path)
    {
        public string Path { get; } = path;
        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
        public override string ToString() => Name;
    }
}

internal static class InputDialog
{
    public static string[]? Show(Form owner, string title, params (string Label, bool Secret)[] fields)
    {
        using var dialog = new Form
        {
            Text = title, Width = 400, Height = 115 + fields.Length * 60,
            MinimumSize = new Size(340, 170), StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false
        };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12),
            ColumnCount = 1, RowCount = fields.Length * 2 + 1 };
        var inputs = new TextBox[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
            layout.Controls.Add(new Label { Text = fields[i].Label, Dock = DockStyle.Fill }, 0, i * 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            inputs[i] = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = fields[i].Secret };
            layout.Controls.Add(inputs[i], 0, i * 2 + 1);
        }
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "ОК", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, fields.Length * 2);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        ThemeManager.Apply(dialog);
        return dialog.ShowDialog(owner) == DialogResult.OK ? inputs.Select(x => x.Text).ToArray() : null;
    }
}

internal static class ThemeManager
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DataKeeperGUI", "theme.txt");
    public static bool IsDark { get; private set; } = Load();

    public static void Toggle()
    {
        IsDark = !IsDark;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, IsDark ? "dark" : "light");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public static void Apply(Control root)
    {
        Color background = IsDark ? Color.FromArgb(25, 30, 38) : Color.FromArgb(247, 249, 252);
        Color surface = IsDark ? Color.FromArgb(35, 42, 53) : Color.White;
        Color button = IsDark ? Color.FromArgb(49, 59, 73) : Color.FromArgb(229, 234, 241);
        Color foreground = IsDark ? Color.FromArgb(235, 240, 246) : Color.FromArgb(30, 39, 51);
        Color border = IsDark ? Color.FromArgb(85, 99, 118) : Color.FromArgb(179, 188, 202);

        void Paint(Control control)
        {
            control.BackColor = control is TextBox or ListBox ? surface
                : control is Button ? button : background;
            control.ForeColor = foreground;
            if (control is Button item)
            {
                item.FlatStyle = FlatStyle.Flat;
                item.FlatAppearance.BorderColor = border;
                item.UseVisualStyleBackColor = false;
            }
            foreach (Control child in control.Controls) Paint(child);
        }
        Paint(root);
    }

    private static bool Load()
    {
        try { return File.Exists(SettingsPath) &&
            File.ReadAllText(SettingsPath).Trim().Equals("dark", StringComparison.OrdinalIgnoreCase); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}

internal sealed class NoteStore
{
    private const string Extension = ".dk";
    private const string SettingsName = "DataKeeper.settings";
    private const int SaltSize = 16, NonceSize = 12, TagSize = 16, Iterations = 210_000;
    private static readonly string LocalSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DataKeeper", SettingsName);

    public string? Folder { get; private set; }

    public NoteStore()
    {
        foreach (string path in new[] { Path.Combine(AppContext.BaseDirectory, SettingsName), LocalSettingsPath })
        {
            try
            {
                if (!File.Exists(path)) continue;
                string folder = File.ReadAllText(path, Encoding.UTF8).Trim();
                if (Directory.Exists(folder)) { Folder = folder; break; }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public void SelectFolder(string folder)
    {
        folder = Path.GetFullPath(folder);
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Папка не найдена.");
        Folder = folder;
        SaveSettings();
    }

    public void CreateFolder(string parent, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or "..")
            throw new IOException("Некорректное название папки.");
        string folder = Path.Combine(parent, name);
        if (Directory.Exists(folder)) throw new IOException("Папка с таким названием уже существует.");
        Directory.CreateDirectory(folder);
        SelectFolder(folder);
    }

    public string[] ListNotes() => Directory.EnumerateFiles(RequireFolder(), "*" + Extension)
        .OrderBy(Path.GetFileNameWithoutExtension, StringComparer.CurrentCultureIgnoreCase).ToArray();

    public string PathFor(string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or "..")
            throw new IOException("Некорректное имя заметки.");
        return Path.Combine(RequireFolder(), name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
            ? name : name + Extension);
    }

    public bool TryRead(string path, string password, out string content)
    {
        content = string.Empty;
        byte[] data = File.ReadAllBytes(path);
        const int headerSize = 4 + SaltSize + NonceSize + TagSize;
        if (data.Length < headerSize || Encoding.ASCII.GetString(data, 0, 4) != "DK01") return false;
        byte[] salt = data[4..(4 + SaltSize)];
        byte[] nonce = data[(4 + SaltSize)..(4 + SaltSize + NonceSize)];
        byte[] tag = data[(4 + SaltSize + NonceSize)..headerSize];
        byte[] plaintext = new byte[data.Length - headerSize];
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, data.AsSpan(headerSize), tag, plaintext);
            content = Encoding.UTF8.GetString(plaintext);
            return true;
        }
        catch (CryptographicException) { return false; }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public void Write(string path, string content, string password, bool isNew)
    {
        if (string.IsNullOrEmpty(password)) throw new IOException("Пароль не может быть пустым.");
        if (isNew && File.Exists(path)) throw new IOException("Заметка с таким именем уже существует.");
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] plaintext = Encoding.UTF8.GetBytes(content);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        string temp = Path.Combine(Path.GetDirectoryName(path)!, "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var aes = new AesGcm(key, TagSize)) aes.Encrypt(nonce, plaintext, ciphertext, tag);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
            {
                stream.Write("DK01"u8);
                stream.Write(salt);
                stream.Write(nonce);
                stream.Write(tag);
                stream.Write(ciphertext);
                stream.Flush(true);
            }
            File.Move(temp, path, !isNew);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private string RequireFolder() => Folder is { } folder && Directory.Exists(folder)
        ? folder : throw new IOException("Сначала выберите папку заметок.");

    private void SaveSettings()
    {
        foreach (string path in new[] { Path.Combine(AppContext.BaseDirectory, SettingsName), LocalSettingsPath })
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, Folder!, Encoding.UTF8);
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
                return;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        throw new IOException("Не удалось сохранить путь к папке заметок.");
    }
}
