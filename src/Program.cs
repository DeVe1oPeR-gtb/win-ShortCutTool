using System;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                string.Format("アプリケーションの起動に失敗しました。\n\nエラー: {0}", ex.Message),
                "エラー",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }
}
