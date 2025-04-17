using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MyWebToAPK.Helpers
{
    public static class ApkHelper
    {
        /// <summary>
        /// Updates the configuration file (e.g., config.json) inside the APK.
        /// </summary>
        /// <param name="apkPath">Full path to the APK.</param>
        /// <param name="newUrl">The URL value to set.</param>
        /// <returns>True if update succeeds; otherwise, false.</returns>
        public static async Task<bool> UpdateApkConfigAsync(string apkPath, string newUrl)
        {
            try
            {
                // Adjust the entry path as needed.
                string configEntryPath = "assets/Resources/Assets/config.json";
                using (ZipArchive archive = ZipFile.Open(apkPath, ZipArchiveMode.Update))
                {
                    var entry = archive.GetEntry(configEntryPath);
                    if (entry != null)
                    {
                        string content;
                        using (var reader = new StreamReader(entry.Open()))
                        {
                            content = await reader.ReadToEndAsync();
                        }

                        // Deserialize the JSON content.
                        var config = JsonSerializer.Deserialize<Dictionary<string, string>>(content) ?? new Dictionary<string, string>();
                        config["url"] = newUrl;
                        string newContent = JsonSerializer.Serialize(config);

                        // Delete and recreate the entry.
                        entry.Delete();
                        var newEntry = archive.CreateEntry(configEntryPath);
                        using (var writer = new StreamWriter(newEntry.Open(), Encoding.UTF8))
                        {
                            await writer.WriteAsync(newContent);
                        }
                        return true;
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception during APK config update: " + ex.Message);
                return false;
            }
        }
    }
}
