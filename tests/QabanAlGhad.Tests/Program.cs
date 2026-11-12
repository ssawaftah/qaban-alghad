using System;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Web.Script.Serialization;
using System.Collections.Generic;
namespace WeighbridgeBridge
{
    internal static class TestProgram
    {
        static void Assert(bool condition, string name)
        {
            if (!condition)
                throw new Exception("FAIL: " + name);
        }
        static int Tests()
        {
            try
            {
                decimal w;
                Assert(Protocol.Parse("ST,GS,+000120kg", out w) && w == 120, "GS");
                Assert(Protocol.Parse("ST,GS,-000020kg", out w) && w == -20, "negative");
                Assert(Protocol.Parse("ST,GS,1,+42kg", out w) && w == 42, "channel GS");
                Assert(Protocol.Parse("1,ST,120,kg", out w) && w == 120, "channel ST");
                Assert(Protocol.Parse("ST,NT,12.5,kg", out w) && w == 12.5m, "NT");
                Assert(!Protocol.Parse("US,GS,+120kg", out w), "unstable");
                Assert(!Protocol.Parse("ST,GS,1 2xkg", out w), "malformed");
                Assert(Protocol.Parse("ST,GS,+000000kg", out w) && w == 0, "zero");
                var f = new Frames();
                string raw;
                Assert(!f.Push("ST,GS,+12", out w, out raw), "partial");
                Assert(f.Push("0kg\r\n", out w, out raw) && w == 120, "split");
                Assert(f.Push("ST,GS,+120kg\rST,GS,+120kg\r", out w, out raw) && w == 120, "unchanged fresh");
                Assert(!f.Push("ST,GS,+120kg\rUS,GS,+120kg\r", out w, out raw), "new invalid frame");
                Assert(Protocol.Payload("1", 12.5m) == "{\"company_id\":\"1\",\"weight_scale\":\"12.5\"}", "JSON");
                Console.WriteLine("PASS: 13 protocol/framing checks");
                CompatibilityTests.Run();
                UpdateTests.Run();
                InterfaceTests.Rules();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 1;
            }
        }
        [STAThread]
        static int Main(string[] args)
        {
            string mode = args.Length == 0 ? "--all" : args[0];
            if (mode == "--online-lifecycle" && args.Length == 2)
                return UpdateTests.OnlineLifecycle(args[1]);
            if (mode == "--update-lifecycle")
                return UpdateTests.Lifecycle();
            if (mode == "--ui-test")
                return InterfaceTests.Windows();
            if (mode == "--self-test")
                return Tests();
            if (mode == "--integration-test")
                return CompatibilityTests.Integration();
            if (mode == "--dual-test")
                return DualDeliveryTests.Run();
            if (mode != "--all")
                return 2;
            if (Tests() != 0)
                return 1;
            if (CompatibilityTests.Integration() != 0)
                return 1;
            return DualDeliveryTests.Run();
        }
    }
}
