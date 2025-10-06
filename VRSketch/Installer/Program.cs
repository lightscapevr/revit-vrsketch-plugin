using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;


namespace Installer
{
    internal class Program
    {
#if false
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, int nSize);
#endif

        static void Error(string msg)
        {
            Console.WriteLine(msg);
            Console.WriteLine("(press a key to exit)");
            Console.ReadKey();
        }

        static byte[] Expand(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            {
                byte[] result = new byte[entry.Length];
                int ofs = 0;
                while (ofs < result.Length)
                {
                    int chunk = stream.Read(result, ofs, result.Length - ofs);
                    if (chunk <= 0)
                        throw new Exception("Unexpected end of file");
                    ofs += chunk;
                }
                return result;
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("========================================================================");
            Console.WriteLine("Welcome to VR Sketch (custom beta version installer)!");
            Console.WriteLine("More info and feedback: info@baroquesoftware.com");
            Console.WriteLine("========================================================================\n");

#if false
            var b_name = new StringBuilder(1024);
            GetModuleFileName(IntPtr.Zero, b_name, 1024);
            string my_name = b_name.ToString();
            if (my_name.Length <= 4 || my_name.Substring(my_name.Length - 4).ToLowerInvariant() != ".exe")
            {
                Error($"This executable's file name does not end with '.exe': {my_name}");
                return;
            }
            string zip_name = my_name.Substring(0, my_name.Length - 4) + ".zip";
            if (!File.Exists(zip_name))
            {
                Error($"Cannot find the file {zip_name}");
                return;
            }
            var zip = ZipFile.OpenRead(zip_name);
            DoExtract(zip);
#else
            //var x = Properties.Resources.vrsketch;
            string zip_name = Path.GetTempFileName();
            File.WriteAllBytes(zip_name, Properties.Resources.vrsketch);
            try
            {
                using (var zip = ZipFile.OpenRead(zip_name))
                    DoExtract(zip);
            }
            finally
            {
                File.Delete(zip_name);
            }
#endif
        }

        static void DoExtract(ZipArchive zip)
        {
            string root = "%AppData%\\Autodesk\\Revit\\Addins";
            root = Environment.ExpandEnvironmentVariables(root);
            Console.WriteLine($"Looking for directories in {root}...");
            List<string> dirs;
            try
            {
                dirs = new List<string>(Directory.EnumerateDirectories(root));
            }
            catch (Exception e)
            {
                Error($"Cannot find your user Revit addins directory.\nIt should be at {root}.\nError: {e}");
                return;
            }
            if (dirs.Count == 0)
            {
                Error($"Cannot find your user Revit addins directory.\nIt should be at {root} but this directory is empty.");
                return;
            }

            Console.WriteLine("Revit versions found on this system:\n");
            foreach (var d in dirs)
                Console.WriteLine("       " + Path.GetFileName(d));
            Console.WriteLine("");
            string target;
            while (true)
            {
                Console.Write("Please type the Revit version that you want to install this add-in for: ");
                string s = Console.ReadLine().Trim();
                if (s.Length == 0 || s.StartsWith(".") || s.Contains("\\") || s.Contains("/"))
                {
                    Console.WriteLine("Aborted.");
                    return;
                }
                Console.WriteLine("");
                target = Path.Combine(root, s);
                if (Directory.Exists(target))
                    break;

                Console.WriteLine($"Path not found: {target}");
            }

            string vrsketch_root = Path.Combine(target, "VRSketch");

            var addin_entry = zip.GetEntry("VRSketch.addin");
            if (addin_entry == null)
            {
                Error($"Internal error: the archive does not contain VRSketch.addin!");
                return;
            }

            var old_dir = Path.Combine(vrsketch_root, "VRSketch");
            if (Directory.Exists(old_dir))
            {
                Console.WriteLine($"Removing previous installation...");
                Directory.Delete(old_dir, true);
            }

            Console.WriteLine($"Decompressing files...");
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.ToLowerInvariant() == "vrsketch.addin")
                    continue;
                byte[] data = Expand(entry);
                string dst = Path.Combine(vrsketch_root, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                using (var stream = new FileStream(dst, FileMode.Create))
                    stream.Write(data, 0, data.Length);
            }

            string addin = Encoding.UTF8.GetString(Expand(addin_entry));
            int start = addin.IndexOf("<Assembly>");
            int stop = addin.IndexOf("</Assembly>");
            addin = addin.Substring(0, start) + "<Assembly>" +
                Path.Combine(vrsketch_root, "VRSketch.dll") + addin.Substring(stop);
            File.WriteAllText(Path.Combine(target, "VRSketch.addin"), addin);
            // note: File.WriteAllText() with no explicit encoding defaults to UTF8NoBOM
            // (yay to C# for doing the sane thing and assuming people want to use UTF8!)
            // which I'm pretty sure is the encoding that we need.  The original content
            // is an xml file starting with <?xml version="1.0" encoding="utf-8"?> and
            // it doesn't start with a BOM either.  I didn't test with, say, a username
            // with an accent in it, but unless things are very messed up, this should
            // write the correctly-encoded path in the <Assembly> tag.

            Console.WriteLine($"Installation done!  Press a key to exit.");
            Console.ReadKey();
        }
    }
}
