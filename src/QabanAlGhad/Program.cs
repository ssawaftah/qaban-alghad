using System.Threading;
using System.Windows.Forms;

namespace WeighbridgeBridge
{
    internal static class Program
    {
        [System.STAThread]
        private static int Main()
        {
            bool first;
            using (var gate = new Mutex(true, "WeighbridgeBridge.SingleInstance", out first))
            {
                if (!first)
                {
                    MessageBox.Show("البرنامج مفتوح بالفعل.");
                    return 1;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }
        }
    }
}
