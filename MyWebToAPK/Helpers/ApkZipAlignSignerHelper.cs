using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace MyWebToAPK.Helpers
{
    public static class ApkZipAlignSignerHelper
    {
        #region Embedded Resource Extraction

        /// <summary>
        /// Extracts an embedded resource from the current assembly to a temporary file.
        /// </summary>
        /// <param name="resourceName">The fully‑qualified resource name.</param>
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

        #endregion

        /// <summary>
        /// Aligns the APK using zipalign.
        /// </summary>
        /// <param name="inputApkPath">Path to the input (modified) APK.</param>
        /// <param name="outputApkPath">Path where the aligned APK will be written.</param>
        /// <returns>True if zipalign succeeds; otherwise, false.</returns>
        public static async Task<bool> ZipAlignApkAsync(string inputApkPath, string outputApkPath)
        {
            try
            {
                // 1. Extract the embedded zipalign executable.
                string zipalignResourceName = "MyWebToAPK.Tools.zipalign.exe";
                string extractedZipalignPath = ExtractResourceToTempFile(zipalignResourceName);
                Debug.WriteLine($"Extracted zipalign path: {extractedZipalignPath}");
                if (!File.Exists(extractedZipalignPath))
                {
                    Debug.WriteLine("Extracted zipalign file not found.");
                    return false;
                }

                // 2. Build the command line for zipalign.
                // Example: zipalign.exe -v 4 input.apk output.apk
                string arguments = $"-v 4 \"{inputApkPath}\" \"{outputApkPath}\"";
                Debug.WriteLine("Zipalign arguments: " + arguments);

                // 3. Create ProcessStartInfo for zipalign.
                var psi = new ProcessStartInfo
                {
                    FileName = extractedZipalignPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                using (Process process = Process.Start(psi))
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                    Debug.WriteLine("Zipalign output: " + output);
                    Debug.WriteLine("Zipalign error: " + error);
                    // Delete the temporary zipalign file.
                    if (File.Exists(extractedZipalignPath))
                        File.Delete(extractedZipalignPath);
                    if (process.ExitCode != 0)
                    {
                        Debug.WriteLine("Zipalign failed with exit code: " + process.ExitCode);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception during zipalign: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Re-signs the APK by first aligning it with zipalign and then signing it.
        /// </summary>
        /// <param name="apkPath">Full path to the modified APK file.</param>
        /// <param name="keystorePath">Full path to the keystore file.</param>
        /// <param name="keyAlias">Keystore key alias.</param>
        /// <param name="keystorePass">Keystore password.</param>
        /// <param name="keyPass">Key password.</param>
        /// <returns>True if both zipalign and signing succeed; otherwise, false.</returns>
        public static async Task<bool> ZipAlignAndSignApkAsync(string apkPath, string keystorePath, string keyAlias, string keystorePass, string keyPass)
        {
            // Create a temporary file path for the aligned APK.
            string alignedApkPath = Path.Combine(Path.GetDirectoryName(apkPath), "aligned_" + Path.GetFileName(apkPath));

            // 1. Run zipalign.
            bool aligned = await ZipAlignApkAsync(apkPath, alignedApkPath);
            if (!aligned)
            {
                Debug.WriteLine("Failed to align the APK.");
                return false;
            }

            // 2. Now sign the aligned APK.
            bool signed = await ApkSignerHelper.SignApkAsync(alignedApkPath, keystorePath, keyAlias, keystorePass, keyPass);
            if (!signed)
            {
                Debug.WriteLine("APK signing failed.");
                return false;
            }

            // 3. Optionally, replace the original APK with the aligned and signed APK.
            File.Copy(alignedApkPath, apkPath, true);
            File.Delete(alignedApkPath);
            return true;
        }
    }
}
