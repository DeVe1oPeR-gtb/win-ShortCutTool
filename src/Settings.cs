using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Collections;

public class Settings
{
    public WindowSettings Window { get; set; }
    public ShortcutSettings Shortcut { get; set; }
    public BehaviorSettings Behavior { get; set; }
    public BrowserSettings Browser { get; set; }
    public UiSettings Ui { get; set; }
    public LogSettings Log { get; set; }
    public Dictionary<string, string> UiTexts { get; set; }
    public Dictionary<Keys, ActionSetting> Actions { get; set; }

    public static Settings Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(string.Format("設定ファイルが見つかりません: {0}", path));
        }

        try
        {
            var jsonText = File.ReadAllText(path, Encoding.UTF8);
            var serializer = new JavaScriptSerializer();
            var settingsData = serializer.Deserialize<Dictionary<string, object>>(jsonText);

            var settings = new Settings
            {
                Window = LoadWindowSettings(settingsData),
                Shortcut = LoadShortcutSettings(settingsData),
                Behavior = LoadBehaviorSettings(settingsData),
                Browser = LoadBrowserSettings(settingsData),
                Ui = LoadUiSettings(settingsData),
                Log = LoadLogSettings(settingsData),
                UiTexts = new Dictionary<string, string>(),
                Actions = new Dictionary<Keys, ActionSetting>()
            };

            // UIテキストの読み込み
            if (settingsData.ContainsKey("uiTexts"))
            {
                var uiTexts = settingsData["uiTexts"] as Dictionary<string, object>;
                if (uiTexts != null)
                {
                    foreach (var uiText in uiTexts)
                    {
                        if (uiText.Value is Dictionary<string, object>)
                        {
                            var errorTexts = uiText.Value as Dictionary<string, object>;
                            foreach (var errorText in errorTexts)
                            {
                                settings.UiTexts.Add(
                                    string.Format("error.{0}", errorText.Key),
                                    errorText.Value.ToString()
                                );
                            }
                        }
                        else
                        {
                            settings.UiTexts.Add(uiText.Key, uiText.Value.ToString());
                        }
                    }
                }
            }

            // アクションの読み込み
            if (settingsData.ContainsKey("actions"))
            {
                var actions = settingsData["actions"] as Dictionary<string, object>;
                if (actions != null)
                {
                    foreach (var action in actions)
                    {
                        var actionData = action.Value as Dictionary<string, object>;
                        if (actionData != null)
                        {
                            var type = GetStringValue(actionData, "type");
                            string value;
                            
                            if (type == "prompt")
                            {
                                var templateFile = GetStringValue(actionData, "templateFile");
                                if (string.IsNullOrEmpty(templateFile))
                                {
                                    throw new InvalidOperationException("プロンプトタイプのアクションにはtemplateFileが必要です");
                                }
                                value = LoadTemplateFile(templateFile);
                            }
                            else
                            {
                                value = GetStringValue(actionData, "url");
                            }

                            settings.Actions.Add(
                                ShortcutParser.ParseShortcut(action.Key),
                                new ActionSetting
                                {
                                    Type = type,
                                    Value = value,
                                    ShortcutText = action.Key,
                                    DisplayName = GetStringValue(actionData, "displayName"),
                                    Description = GetStringValue(actionData, "description"),
                                    Program = type == "run" ? GetStringValue(actionData, "program") : null,
                                    Arguments = type == "run" ? GetStringValue(actionData, "arguments") : null
                                }
                            );
                        }
                    }
                }
            }

            return settings;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(string.Format("設定ファイルの読み込みに失敗しました: {0}", ex.Message), ex);
        }
    }

    private static WindowSettings LoadWindowSettings(Dictionary<string, object> data)
    {
        var windowSettings = new WindowSettings();
        if (data.ContainsKey("window"))
        {
            var windowData = data["window"] as Dictionary<string, object>;
            if (windowData != null)
            {
                windowSettings.Width = GetIntValue(windowData, "width", 600);
                windowSettings.Height = GetIntValue(windowData, "height", 400);
                windowSettings.X = GetIntValue(windowData, "x", 100);
                windowSettings.Y = GetIntValue(windowData, "y", 100);
            }
        }
        return windowSettings;
    }

    private static ShortcutSettings LoadShortcutSettings(Dictionary<string, object> data)
    {
        var shortcutSettings = new ShortcutSettings();
        if (data.ContainsKey("shortcut"))
        {
            var shortcutData = data["shortcut"] as Dictionary<string, object>;
            if (shortcutData != null)
            {
                shortcutSettings.StartShortcutText = GetStringValue(shortcutData, "start");
                shortcutSettings.StartShortcut = ShortcutParser.ParseShortcut(shortcutSettings.StartShortcutText);
            }
        }
        return shortcutSettings;
    }

    private static BehaviorSettings LoadBehaviorSettings(Dictionary<string, object> data)
    {
        var behaviorSettings = new BehaviorSettings();
        if (data.ContainsKey("behavior"))
        {
            var behaviorData = data["behavior"] as Dictionary<string, object>;
            if (behaviorData != null)
            {
                behaviorSettings.AutoExitWaitAfterAction = GetBoolValue(behaviorData, "autoExitWaitAfterAction", true);
                behaviorSettings.ShowBalloon = GetBoolValue(behaviorData, "showBalloon", false);
                behaviorSettings.ShowStartupBalloon = GetBoolValue(behaviorData, "showStartupBalloon", true);
                behaviorSettings.BalloonTimeout = GetIntValue(behaviorData, "balloonTimeout", 1000);
            }
        }
        return behaviorSettings;
    }

    private static BrowserSettings LoadBrowserSettings(Dictionary<string, object> data)
    {
        var browserSettings = new BrowserSettings();
        if (data.ContainsKey("browser"))
        {
            var browserData = data["browser"] as Dictionary<string, object>;
            if (browserData != null)
            {
                browserSettings.Type = GetStringValue(browserData, "type", "edge");
                browserSettings.AiUrl = GetStringValue(browserData, "aiUrl", "https://chat.openai.com/");
            }
        }
        return browserSettings;
    }

    private static LogSettings LoadLogSettings(Dictionary<string, object> data)
    {
        var logSettings = new LogSettings();
        if (data.ContainsKey("log"))
        {
            var logData = data["log"] as Dictionary<string, object>;
            if (logData != null)
            {
                logSettings.Level = GetStringValue(logData, "level", "Info");
                logSettings.MaxFileSize = GetIntValue(logData, "maxFileSize", 5 * 1024 * 1024);
                logSettings.Directory = GetStringValue(logData, "directory", "logs");
                logSettings.FileName = GetStringValue(logData, "fileName", "app.log");
            }
        }
        return logSettings;
    }

    private static UiSettings LoadUiSettings(Dictionary<string, object> data)
    {
        var uiSettings = new UiSettings();
        if (data.ContainsKey("ui"))
        {
            var uiData = data["ui"] as Dictionary<string, object>;
            if (uiData != null)
            {
                uiSettings.FontSize = GetIntValue(uiData, "fontSize", 10);
                uiSettings.FontFamily = GetStringValue(uiData, "fontFamily", "MS Gothic");
                
                // 区切り線の設定を読み込む
                if (uiData.ContainsKey("separatorLines"))
                {
                    var separatorLinesData = uiData["separatorLines"] as ArrayList;
                    if (separatorLinesData != null)
                    {
                        uiSettings.SeparatorLines = new int[separatorLinesData.Count];
                        for (int i = 0; i < separatorLinesData.Count; i++)
                        {
                            uiSettings.SeparatorLines[i] = Convert.ToInt32(separatorLinesData[i]);
                        }
                        Logger.Log(string.Format("区切り線の設定を読み込み: {0}", 
                            string.Join(", ", uiSettings.SeparatorLines)), LogLevel.Info);
                    }
                }
            }
        }
        return uiSettings;
    }

    private static string LoadTemplateFile(string templateFile)
    {
        if (!File.Exists(templateFile))
        {
            throw new FileNotFoundException(string.Format("テンプレートファイルが見つかりません: {0}", templateFile));
        }

        try
        {
            return File.ReadAllText(templateFile, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(string.Format("テンプレートファイルの読み込みに失敗しました: {0}", ex.Message), ex);
        }
    }

    private static string GetStringValue(Dictionary<string, object> data, string key, string defaultValue = "")
    {
        object value;
        if (data.TryGetValue(key, out value))
        {
            return value != null ? value.ToString() : defaultValue;
        }
        return defaultValue;
    }

    private static int GetIntValue(Dictionary<string, object> data, string key, int defaultValue = 0)
    {
        object value;
        if (data.TryGetValue(key, out value))
        {
            int result;
            if (int.TryParse(value.ToString(), out result))
            {
                return result;
            }
        }
        return defaultValue;
    }

    private static bool GetBoolValue(Dictionary<string, object> data, string key, bool defaultValue = false)
    {
        object value;
        if (data.TryGetValue(key, out value))
        {
            bool result;
            if (bool.TryParse(value.ToString(), out result))
            {
                return result;
            }
        }
        return defaultValue;
    }
}

public class WindowSettings
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public class ShortcutSettings
{
    public Keys StartShortcut { get; set; }
    public string StartShortcutText { get; set; }
}

public class BehaviorSettings
{
    public bool AutoExitWaitAfterAction { get; set; }
    public bool ShowBalloon { get; set; }
    public bool ShowStartupBalloon { get; set; }
    public int BalloonTimeout { get; set; }
}

public class BrowserSettings
{
    public string Type { get; set; }
    public string AiUrl { get; set; }
}

public class ActionSetting
{
    public string Type { get; set; }
    public string Value { get; set; }
    public string ShortcutText { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Program { get; set; }
    public string Arguments { get; set; }
}

public class LogSettings
{
    public string Level { get; set; }
    public int MaxFileSize { get; set; }
    public string Directory { get; set; }
    public string FileName { get; set; }
}

public class UiSettings
{
    public int FontSize { get; set; }
    public string FontFamily { get; set; }
    public int[] SeparatorLines { get; set; }
}
