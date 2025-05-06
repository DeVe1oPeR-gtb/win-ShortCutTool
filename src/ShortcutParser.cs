using System;
using System.Windows.Forms;
using System.Collections.Generic;

public static class ShortcutParser
{
    private static readonly Dictionary<string, Keys> ModifierKeys = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase)
    {
        { "CTRL", Keys.Control },
        { "ALT", Keys.Alt },
        { "SHIFT", Keys.Shift }
    };

    private static readonly Dictionary<string, Keys> NumberKeys = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase)
    {
        { "0", Keys.D0 },
        { "1", Keys.D1 },
        { "2", Keys.D2 },
        { "3", Keys.D3 },
        { "4", Keys.D4 },
        { "5", Keys.D5 },
        { "6", Keys.D6 },
        { "7", Keys.D7 },
        { "8", Keys.D8 },
        { "9", Keys.D9 }
    };

    public static Keys ParseShortcut(string shortcutText)
    {
        if (string.IsNullOrWhiteSpace(shortcutText))
        {
            throw new ArgumentException("ショートカットキーが指定されていません。");
        }

        Keys result = Keys.None;
        string[] parts = shortcutText.Split('+');

        foreach (string part in parts)
        {
            string trimmed = part.Trim();
            
            // 修飾キーのチェック
            Keys modifierKey;
            if (ModifierKeys.TryGetValue(trimmed, out modifierKey))
            {
                result |= modifierKey;
                continue;
            }
            
            // 数字キーのチェック
            Keys numberKey;
            if (NumberKeys.TryGetValue(trimmed, out numberKey))
            {
                result |= numberKey;
                continue;
            }
            
            // その他のキーのチェック
            Keys key;
            if (Enum.TryParse(trimmed, true, out key))
            {
                result |= key;
            }
            else
            {
                throw new ArgumentException(string.Format("無効なキーが指定されています: '{0}'", trimmed));
            }
        }

        if (result == Keys.None)
        {
            throw new ArgumentException("有効なショートカットキーが指定されていません。");
        }

        return result;
    }
}

