using System.Threading;
using System.Windows.Forms;

namespace WeighbridgeBridge
{
    internal static class Program
    {
        [System.STAThread]
        private static int Main(string[] args)
        {
            string app = Application.StartupPath;
            bool pending = System.IO.File.Exists(UpdateTransaction.JournalPath(app));
            bool updated;
            try
            {
                updated = UpdateTransaction.Startup(app, args);
            }
            catch (System.Exception ex)
            {
                try
                {
                    System.IO.File.AppendAllText(System.IO.Path.Combine(app, "update-error.log"),
                                                 System.DateTime.UtcNow.ToString("o") + " " + ex +
                                                     System.Environment.NewLine);
                }
                catch
                {
                }
                AppMessage.Show(
                    "تعذر استعادة التحديث. احتفظ بملفات النسخة السابقة وراجع سجل التحديث قبل المحاولة مجددًا.");
                return 1;
            }
            if (pending && !updated)
                return 0;
            bool first;
            using (var gate = new Mutex(true, "WeighbridgeBridge.SingleInstance", out first))
            {
                if (!first)
                {
                    AppMessage.Show("البرنامج مفتوح بالفعل.");
                    return 1;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var form = new MainForm();
                form.Shown += (s, e) =>
                {
                    if (updated)
                        UpdateTransaction.Ready(app);
                };
                Application.Run(form);
                return 0;
            }
        }
    }
}
