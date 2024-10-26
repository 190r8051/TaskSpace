using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using TaskSpace.Core;

namespace TaskSpace.Core {
    public class UwpWindowIconFinder {
        public Icon Find(AppWindow uwpWindow) {
            string processPath = uwpWindow.ExecutablePath;
            string directoryPath = Path.GetDirectoryName(processPath);
            string directoryName = Path.GetFileName(directoryPath);
            XDocument manifest = XDocument.Parse(File.ReadAllText(Path.Combine(directoryPath, "AppxManifest.xml")));
            XNamespace ns = manifest.Root.Name.Namespace;
            string logoPath = manifest.Root.Element(ns + "Properties").Element(ns + "Logo").Value;
            string name = manifest.Root.Element(ns + "Identity").Attribute("Name").Value;

            string executable = Path.GetFileName(processPath);

            XElement application = manifest.Root
                .Element(ns + "Applications")
                .Elements(ns + "Application")
                .FirstOrDefault(e => executable.Equals(e.Attribute("Executable").Value, StringComparison.InvariantCultureIgnoreCase));

            if(application != null) {
                XNamespace uapNs = "http://schemas.microsoft.com/appx/manifest/uap/windows10";

                XElement visualElements = application.Element(uapNs + "VisualElements");

                XAttribute attribute = visualElements.Attribute("Square44x44Logo");

                if(attribute != null) {
                    logoPath = attribute.Value;
                }
            }

            string resourcePath = "@{" + directoryName + "?ms-resource://" + name + "/Files/" + logoPath.Replace("\\", "/") + "}";

            string logoFullPath = ExtractNormalPath(resourcePath);

            if(File.Exists(logoFullPath)) {
                Bitmap bitmap = new Bitmap(logoFullPath);
                nint iconHandle = bitmap.GetHicon();
                return Icon.FromHandle(iconHandle);
            }

            return Icon.ExtractAssociatedIcon(processPath);
        }

        [DllImport("shlwapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false, ThrowOnUnmappableChar = true)]
        public static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, IntPtr ppvReserved);

        public string ExtractNormalPath(string indirectString) {
            StringBuilder outBuff = new StringBuilder(1024);
            int result = SHLoadIndirectString(indirectString, outBuff, outBuff.Capacity, IntPtr.Zero);

            return outBuff.ToString();
        }
    }
}
