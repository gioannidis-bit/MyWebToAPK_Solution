using System;
using System.IO;
using System.Reflection;

namespace MyWebToAPK.Helpers
{
    public static class EmbeddedResourceHelper
    {
        /// <summary>
        /// Extracts an embedded resource from the current assembly to a temporary file.
        /// </summary>
        /// <param name="resourceName">The fully-qualified resource name.</param>
        /// <returns>The full path to the extracted file.</returns>
        public static string ExtractResourceToTempFile(string resourceName)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(resourceName));
            var assembly = Assembly.GetExecutingAssembly();
            using (var resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                    throw new Exception($"Resource '{resourceName}' not found.");
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    resourceStream.CopyTo(fileStream);
                }
            }
            return tempPath;
        }
    }
}
