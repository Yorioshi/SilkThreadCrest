using System;
using System.IO;
using System.Reflection;

namespace SilkThreadCrest
{
    internal static class SilkThreadSkinAssetReader
    {
        internal static byte[] Read(string externalPath, Assembly assembly, string resourceName, out bool embedded)
        {
            embedded = false;
            if (File.Exists(externalPath))
                return File.ReadAllBytes(externalPath);
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new FileNotFoundException("Skin resource missing in " + assembly.Location, resourceName);
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    embedded = true;
                    return memory.ToArray();
                }
            }
        }
    }
}
