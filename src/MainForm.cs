using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading.Tasks;

public class MainForm : Form
{
    private NotifyIcon notifyIcon;
    private Settings settings;
    private bool waitingForShortcut = false;
    private ContextMenuStrip trayMenu;
    private ListBox listBoxShortcuts;

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(Keys vKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AttachThreadInput(int idAttach, int idAttachTo, bool fAttach);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    private static bool ForceActive(IntPtr handle)
    {
        const uint SPI_GETFOREGROUNDLOCKTIMEOUT = 0x2000;
        const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;
        const int SPIF_SENDCHANGE = 0x2;

        IntPtr dummy = IntPtr.Zero;
        IntPtr timeout = IntPtr.Zero;

        bool isSuccess = false;

        int processId;
        int foregroundID = GetWindowThreadProcessId(GetForegroundWindow(), out processId);
        int targetID = GetWindowThreadProcessId(handle, out processId);

        AttachThreadInput(targetID, foregroundID, true);

        SystemParametersInfo(SPI_GETFOREGROUNDLOCKTIMEOUT, 0, timeout, 0);
        SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, dummy, SPIF_SENDCHANGE);

        isSuccess = SetForegroundWindow(handle);

        SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, timeout, SPIF_SENDCHANGE);
        AttachThreadInput(targetID, foregroundID, false);

        return isSuccess;
    }

    private void ForceActiveWindow()
    {
        for (int i = 0; i < 3; i++)
            if (ForceActive(this.Handle)) break;
    }

    public MainForm()
    {
        this.Text = "Shortcut Tool";
        this.Size = new Size(600, 450);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.White;

        // フォームのイベントハンドラを追加
        this.Activated += MainForm_Activated;
        this.Deactivate += MainForm_Deactivate;
        this.GotFocus += MainForm_GotFocus;
        this.LostFocus += MainForm_LostFocus;

        // ListBoxの設定
        listBoxShortcuts = new ListBox();
        listBoxShortcuts.Location = new Point(20, 20);  // 上部の区切り線を削除したので、位置を調整
        listBoxShortcuts.Size = new Size(540, 410);     // 高さも調整
        listBoxShortcuts.DoubleClick += ListBoxShortcuts_DoubleClick;
        listBoxShortcuts.KeyDown += ListBoxShortcuts_KeyDown;
        this.Controls.Add(listBoxShortcuts);

        this.Load += MainForm_Load;
        this.Shown += MainForm_Shown; // タスクトレイモード用（まだ無効でOK）
    }

    private void MainForm_Activated(object sender, EventArgs e)
    {
        Logger.Log("フォームがアクティブになりました", LogLevel.Info);
    }

    private void MainForm_Deactivate(object sender, EventArgs e)
    {
        Logger.Log("フォームが非アクティブになりました", LogLevel.Info);
    }

    private void MainForm_GotFocus(object sender, EventArgs e)
    {
        Logger.Log("フォームがフォーカスを取得しました", LogLevel.Info);
    }

    private void MainForm_LostFocus(object sender, EventArgs e)
    {
        Logger.Log("フォームがフォーカスを失いました", LogLevel.Info);
    }

    private void MainForm_Load(object sender, EventArgs e)
    {
        try
        {
            Logger.Log("MainForm_Load開始", LogLevel.Info);
            settings = Settings.Load("Settings.json");
            Logger.Log("設定ファイル読み込み完了", LogLevel.Info);

            // ログレベルの設定
            LogLevel logLevel;
            if (Enum.TryParse(settings.Log.Level, true, out logLevel))
            {
                Logger.SetLogLevel(logLevel);
                Logger.Log(string.Format("ログレベルを設定: {0}", logLevel), LogLevel.Info);
            }

            // フォント設定の適用
            Font uiFont = new Font(settings.Ui.FontFamily, settings.Ui.FontSize);
            this.Font = uiFont;
            listBoxShortcuts.Font = uiFont;
            Logger.Log("フォント設定を適用", LogLevel.Info);

            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = SystemIcons.Application;
            notifyIcon.Text = "ショートカットツール";
            notifyIcon.Visible = true;

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("設定再読み込み", null, ReloadSettings_Click);
            trayMenu.Items.Add("終了", null, Exit_Click);
            notifyIcon.ContextMenuStrip = trayMenu;

            Logger.Log("ショートカットリストの更新を開始", LogLevel.Info);
            RefreshShortcutList();
            Logger.Log("ショートカットリストの更新が完了", LogLevel.Info);

            // タスクトレイに最小化
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();

            // 起動時のバルーン通知
            if (settings.Behavior.ShowStartupBalloon)
            {
                notifyIcon.BalloonTipTitle = string.Format("{0} - タスクトレイ待機", settings.UiTexts["appTitle"]);
                notifyIcon.BalloonTipText = string.Format("実行待ちモードに入るには {0} を押してください",
                    settings.Shortcut.StartShortcutText);
                notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
                notifyIcon.ShowBalloonTip(settings.Behavior.BalloonTimeout);
            }

            // ショートカット監視スレッド開始
            Thread monitorThread = new Thread(() => StartShortcutMonitoring());
            monitorThread.IsBackground = true;
            monitorThread.Start();
            Logger.Log("初期化完了", LogLevel.Info);
        }
        catch (Exception ex)
        {
            Logger.Log("初期化エラー: " + ex.Message, LogLevel.Error);
            ShowErrorBalloon("エラー", "アプリケーションの初期化に失敗しました: " + ex.Message);
            Application.Exit();
        }
    }

    private void ListBoxShortcuts_DoubleClick(object sender, EventArgs e)
    {
        if (listBoxShortcuts.SelectedIndex == -1)
        {
            return;
        }

        // 選択された項目のテキストを取得
        var selectedItem = listBoxShortcuts.SelectedItem.ToString();
        
        // 区切り線の場合は無視
        if (selectedItem.StartsWith("-"))
        {
            return;
        }

        // ショートカットキーを取得
        var shortcutText = selectedItem.Split(new[] { " → " }, StringSplitOptions.None)[0].Trim();

        // 対応するアクションを探して実行
        foreach (var kvp in settings.Actions)
        {
            if (kvp.Value.ShortcutText == shortcutText)
            {
                ExecuteAction(kvp.Value, shortcutText);
                if (settings.Behavior.AutoExitWaitAfterAction)
                {
                    EndWaitMode();
                }
                break;
            }
        }
    }

    private void ListBoxShortcuts_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && listBoxShortcuts.SelectedIndex != -1)
        {
            // 選択された項目のテキストを取得
            var selectedItem = listBoxShortcuts.SelectedItem.ToString();
            
            // 区切り線の場合は無視
            if (selectedItem.StartsWith("-"))
            {
                return;
            }

            // ショートカットキーを取得
            var shortcutText = selectedItem.Split(new[] { " → " }, StringSplitOptions.None)[0].Trim();

            // 対応するアクションを探して実行
            foreach (var kvp in settings.Actions)
            {
                if (kvp.Value.ShortcutText == shortcutText)
                {
                    ExecuteAction(kvp.Value, shortcutText);
                    if (settings.Behavior.AutoExitWaitAfterAction)
                    {
                        EndWaitMode();
                    }
                    break;
                }
            }
        }
    }

    private void RefreshShortcutList()
    {
        Logger.Log("RefreshShortcutList開始", LogLevel.Info);
        listBoxShortcuts.Items.Clear();

        // ショートカットキーの最大長を取得
        int maxShortcutLength = 0;
        foreach (var kvp in settings.Actions)
        {
            int length = kvp.Value.ShortcutText.Length;
            if (length > maxShortcutLength)
            {
                maxShortcutLength = length;
            }
        }
        // Ctrl+Qの長さも考慮
        maxShortcutLength = Math.Max(maxShortcutLength, "Ctrl+Q".Length);
        Logger.Log(string.Format("最大ショートカット長: {0}", maxShortcutLength), LogLevel.Info);

        // 区切り線の文字列を作成
        string separatorLine = new string('-', maxShortcutLength + 10); // 矢印とスペースの分を考慮
        Logger.Log(string.Format("区切り線の長さ: {0}", separatorLine.Length), LogLevel.Info);

        // デバッグログ
        if (settings.Ui.SeparatorLines == null)
        {
            Logger.Log("区切り線の設定がnullです", LogLevel.Warning);
        }
        else
        {
            Logger.Log(string.Format("区切り線の設定: {0}", 
                string.Join(", ", settings.Ui.SeparatorLines)), LogLevel.Info);
        }

        int itemIndex = 0;
        foreach (var kvp in settings.Actions)
        {
            string shortcutText = kvp.Value.ShortcutText;
            string displayName = kvp.Value.DisplayName;
            
            // ショートカットキーの後にスペースを追加して整列
            string paddedShortcut = shortcutText.PadRight(maxShortcutLength);
            listBoxShortcuts.Items.Add(string.Format("{0} → {1}", paddedShortcut, displayName));
            
            itemIndex++;
            Logger.Log(string.Format("項目追加: {0} → {1} (インデックス: {2})", 
                shortcutText, displayName, itemIndex), LogLevel.Info);
            
            // 設定された位置に区切り線を挿入
            if (settings.Ui.SeparatorLines != null && Array.IndexOf(settings.Ui.SeparatorLines, itemIndex) >= 0)
            {
                Logger.Log(string.Format("区切り線を追加: 位置 {0}", itemIndex), LogLevel.Info);
                listBoxShortcuts.Items.Add(separatorLine);
            }
        }

        // 実行待ち解除のショートカットを追加（同じ形式で整列）
        string paddedCtrlQ = "Ctrl+Q".PadRight(maxShortcutLength);
        listBoxShortcuts.Items.Add(string.Format("{0} → {1}", paddedCtrlQ, settings.UiTexts["waitModeEnd"]));
        Logger.Log("RefreshShortcutList完了", LogLevel.Info);
    }

    private void MainForm_Shown(object sender, EventArgs e)
    {
        // タスクトレイモード用
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;
        this.Hide();
    }

    public void StartShortcutMonitoring()
    {
        Keys startShortcut = settings.Shortcut.StartShortcut;

        while (true)
        {
            Thread.Sleep(50);

            if (!waitingForShortcut)
            {
                if (IsShortcutPressed(startShortcut))
                {
                    BeginWaitMode();
                }
            }
            else
            {
                if (IsSpecificShortcutPressed(Keys.Control, Keys.Q))
                {
                    EndWaitMode();
                    continue;
                }

                if (IsShortcutPressed(startShortcut))
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        Logger.Log("ウィンドウを最前面に表示します", LogLevel.Info);
                        ForceActiveWindow();
                        Logger.Log("ウィンドウのアクティブ化完了", LogLevel.Info);
                    });
                    continue;
                }

                foreach (var kvp in settings.Actions)
                {
                    if (IsShortcutPressed(kvp.Key))
                    {
                        string shortcutText = kvp.Key.ToString();
                        ExecuteAction(kvp.Value, shortcutText);

                        if (settings.Behavior.AutoExitWaitAfterAction)
                        {
                            EndWaitMode();
                        }
                        break;
                    }
                }
            }
        }
    }

    private void BeginWaitMode()
    {
        this.Invoke((MethodInvoker)delegate
        {
            this.Size = new Size(settings.Window.Width, settings.Window.Height);
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(settings.Window.X, settings.Window.Y);
            this.Text = string.Format("{0} - {1}", settings.UiTexts["appTitle"], settings.UiTexts["waitModeStart"]);
            
            // ListBoxのサイズを更新
            foreach (Control control in this.Controls)
            {
                ListBox listBox = control as ListBox;
                if (listBox != null)
                {
                    listBox.Size = new Size(settings.Window.Width - 60, settings.Window.Height - 50);
                }
            }
            
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.BringToFront();
        });
        waitingForShortcut = true;
    }

    private void EndWaitMode()
    {
        this.Invoke((MethodInvoker)delegate
        {
            this.Text = settings.UiTexts["appTitle"];
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Hide();
            ShowBalloon(settings.UiTexts["waitModeEnd"]," ");
        });
        waitingForShortcut = false;
    }

    private void ExecuteAction(ActionSetting action, string shortcutText)
    {
        try
        {
            Logger.Log("ExecuteAction開始: Type=" + action.Type, LogLevel.Info);
            
            if (action.Type == "open")
            {
                OpenBrowser(action.Value);
                ShowBalloon(settings.UiTexts["actionExecuted"], string.Format("{0} → URLを開きます", action.ShortcutText));
            }
            else if (action.Type == "prompt")
            {
                Logger.Log("プロンプト処理開始", LogLevel.Info);
                
                this.Invoke((MethodInvoker)delegate
                {
                    try
                    {
                        string clipboardText = Clipboard.GetText();
                        Logger.Log("クリップボードの内容: " + clipboardText, LogLevel.Info);
                        
                        if (string.IsNullOrEmpty(clipboardText))
                        {
                            Logger.Log("クリップボードにテキストがありません。", LogLevel.Info);
                            ShowErrorBalloon("エラー", settings.UiTexts["error.clipboardEmpty"]);
                            return;
                        }

                        Logger.Log("テンプレート: " + action.Value, LogLevel.Info);
                        string prompt = ApplyTemplate(action.Value, clipboardText);
                        Logger.Log("生成されたプロンプト: " + prompt, LogLevel.Info);

                        Clipboard.SetText(prompt);
                        Logger.Log("クリップボードにプロンプトを設定しました", LogLevel.Info);
                        
                        ShowBalloon(settings.UiTexts["actionExecuted"], string.Format("{0} → プロンプト生成完了", action.ShortcutText));
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("クリップボード操作エラー: " + ex.Message, LogLevel.Error);
                        Logger.Log("スタックトレース: " + ex.StackTrace, LogLevel.Error);
                        ShowErrorBalloon("エラー", "クリップボード操作に失敗しました: " + ex.Message);
                    }
                });
            }
            else if (action.Type == "run")
            {
                if (string.IsNullOrEmpty(action.Program))
                {
                    throw new InvalidOperationException("実行するプログラムが指定されていません");
                }

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = action.Program,
                        Arguments = action.Arguments ?? "",
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WindowStyle = ProcessWindowStyle.Normal,
                        WorkingDirectory = Path.GetDirectoryName(action.Program)  // プログラムのディレクトリを作業ディレクトリに設定
                    };

                    Logger.Log(string.Format("プログラム実行開始: {0}", action.Program), LogLevel.Info);
                    Logger.Log(string.Format("引数: {0}", action.Arguments ?? "なし"), LogLevel.Info);
                    Logger.Log(string.Format("作業ディレクトリ: {0}", startInfo.WorkingDirectory), LogLevel.Info);

                    var process = Process.Start(startInfo);
                    if (process == null)
                    {
                        throw new Exception("プロセスの起動に失敗しました");
                    }

                    Logger.Log(string.Format("プロセスID: {0}", process.Id), LogLevel.Info);
                    ShowBalloon(settings.UiTexts["actionExecuted"], string.Format("{0} → プログラムを実行しました", action.ShortcutText));
                }
                catch (Exception ex)
                {
                    Logger.Log(string.Format("プログラム実行エラー: {0}", ex.Message), LogLevel.Error);
                    Logger.Log(string.Format("スタックトレース: {0}", ex.StackTrace), LogLevel.Error);
                    ShowErrorBalloon("エラー", string.Format("プログラムの実行に失敗しました: {0}", ex.Message));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log("エラー発生: " + ex.Message, LogLevel.Error);
            Logger.Log("スタックトレース: " + ex.StackTrace, LogLevel.Error);
            ShowErrorBalloon("エラー", "アクションの実行に失敗しました: " + ex.Message);
        }
    }

    private string ApplyTemplate(string template, string clipboardText)
    {
        string result = template;
        result = result.Replace("{content}", clipboardText);
        result = result.Replace("{date}", DateTime.Now.ToString("yyyy-MM-dd"));
        result = result.Replace("{datetime}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        return result;
    }

    private void OpenBrowser(string url)
    {
        try
        {
            string browserPath = "";
            if (settings.Browser.Type == "edge")
            {
                browserPath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
            }
            else if (settings.Browser.Type == "chrome")
            {
                browserPath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
            }
            else
            {
                Logger.Log("未対応のブラウザ指定です。edgeまたはchromeにしてください。", LogLevel.Warning);
                return;
            }

            if (!File.Exists(browserPath))
            {
                Logger.Log("ブラウザが見つかりません: " + browserPath, LogLevel.Warning);
                return;
            }

            Process.Start(browserPath, url);
        }
        catch (Exception ex)
        {
            Logger.Log("ブラウザ起動エラー: " + ex.Message, LogLevel.Error);
        }
    }

    private bool IsShortcutPressed(Keys shortcut)
    {
        // メインキーを取得
        Keys mainKey = shortcut & Keys.KeyCode;
        
        // 修飾キーの状態を取得
        bool ctrlPressed = (GetAsyncKeyState(Keys.ControlKey) & 0x8000) != 0;
        bool altPressed = (GetAsyncKeyState(Keys.Menu) & 0x8000) != 0;
        bool shiftPressed = (GetAsyncKeyState(Keys.ShiftKey) & 0x8000) != 0;
        
        // メインキーの状態を取得
        bool mainKeyPressed = (GetAsyncKeyState(mainKey) & 0x8000) != 0;
        
        // 必要な修飾キーを確認
        bool needCtrl = (shortcut & Keys.Control) != 0;
        bool needAlt = (shortcut & Keys.Alt) != 0;
        bool needShift = (shortcut & Keys.Shift) != 0;
        
        // すべての条件が一致するか確認
        return mainKeyPressed && 
               ctrlPressed == needCtrl && 
               altPressed == needAlt && 
               shiftPressed == needShift;
    }

    private bool IsSpecificShortcutPressed(Keys modifierKey, Keys mainKey)
    {
        bool ctrl = (GetAsyncKeyState(Keys.ControlKey) & 0x8000) != 0;
        bool mainKeyPressed = (GetAsyncKeyState(mainKey) & 0x8000) != 0;
        return ctrl && mainKeyPressed;
    }

    private void ReloadSettings_Click(object sender, EventArgs e)
    {
        try
        {
            settings = Settings.Load("Settings.json");
            
            // フォント設定の再適用
            Font uiFont = new Font(settings.Ui.FontFamily, settings.Ui.FontSize);
            this.Font = uiFont;
            listBoxShortcuts.Font = uiFont;
            
            RefreshShortcutList();
            ShowBalloon("設定更新", "設定を再読み込みしました");
        }
        catch (Exception ex)
        {
            Logger.Log("設定の再読み込みに失敗しました: " + ex.Message, LogLevel.Error);
            ShowErrorBalloon("エラー", "設定の再読み込みに失敗しました: " + ex.Message);
        }
    }

    private void Exit_Click(object sender, EventArgs e)
    {
        notifyIcon.Visible = false;
        Application.Exit();
    }

    private void ShowBalloon(string title, string text)
    {
        if (settings.Behavior.ShowBalloon)
        {
            notifyIcon.BalloonTipTitle = title;
            notifyIcon.BalloonTipText = text;
            notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon.ShowBalloonTip(settings.Behavior.BalloonTimeout);
        }
    }

    private void ShowErrorBalloon(string title, string text)
    {
        if (settings.Behavior.ShowBalloon)
        {
            notifyIcon.BalloonTipTitle = title;
            notifyIcon.BalloonTipText = text;
            notifyIcon.BalloonTipIcon = ToolTipIcon.Error;
            notifyIcon.ShowBalloonTip(settings.Behavior.BalloonTimeout);
        }
    }
}
