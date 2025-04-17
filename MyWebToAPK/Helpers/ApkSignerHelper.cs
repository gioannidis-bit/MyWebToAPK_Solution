using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace MyWebToAPK.Helpers
{
    public static class ApkSignerHelper
    {
        // Nested helper class to extract embedded resources.
        public static class EmbeddedResourceHelper
        {
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
        }

        /// <summary>
        /// Re-signs the APK at the specified path using the embedded signing tool resources.
        /// </summary>
        /// <param name="apkPath">Full path to the modified APK file.</param>
        /// <param name="keystorePath">Full path to your keystore file.</param>
        /// <param name="keyAlias">The keystore key alias.</param>
        /// <param name="keystorePass">The keystore password.</param>
        /// <param name="keyPass">The key password.</param>
        /// <returns>True if signing succeeds; otherwise, false.</returns>
        public static async Task<bool> SignApkAsync(string apkPath, string keystorePath, string keyAlias, string keystorePass, string keyPass)
        {
            try
            {
                // 1. Extract the embedded batch file.
                string batResourceName = "MyWebToAPK.Tools.apksigner.bat";
                string extractedBatPath = EmbeddedResourceHelper.ExtractResourceToTempFile(batResourceName);
                Debug.WriteLine($"Extracted bat path: {extractedBatPath}");
                if (!File.Exists(extractedBatPath))
                {
                    Debug.WriteLine("Extracted batch file not found: " + extractedBatPath);
                    return false;
                }

                // 2. Extract the embedded jar file.
                string jarResourceName = "MyWebToAPK.Tools.apksigner.jar";
                string extractedJarPath = EmbeddedResourceHelper.ExtractResourceToTempFile(jarResourceName);
                Debug.WriteLine($"Extracted jar path: {extractedJarPath}");
                if (!File.Exists(extractedJarPath))
                {
                    Debug.WriteLine("Extracted jar file not found: " + extractedJarPath);
                    return false;
                }

                // 3. Ensure that the jar file is in the same folder as the batch file.
                string batDirectory = Path.GetDirectoryName(extractedBatPath);
                if (string.IsNullOrEmpty(batDirectory))
                {
                    Debug.WriteLine("Batch file directory not found.");
                    return false;
                }
                string targetJarPath = Path.Combine(batDirectory, "apksigner.jar");
                File.Copy(extractedJarPath, targetJarPath, true);
                Debug.WriteLine($"Copied jar to: {targetJarPath}");
                if (!File.Exists(targetJarPath))
                {
                    Debug.WriteLine("Target jar file not found: " + targetJarPath);
                    return false;
                }
                if (File.Exists(extractedJarPath) &&
                    !string.Equals(extractedJarPath, targetJarPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(extractedJarPath);
                }

                // 4. Verify that the APK and keystore files exist.
                Debug.WriteLine($"APK path: {apkPath}");
                if (!File.Exists(apkPath))
                {
                    Debug.WriteLine("APK file not found: " + apkPath);
                    return false;
                }
                Debug.WriteLine($"Keystore path: {keystorePath}");
                if (!File.Exists(keystorePath))
                {
                    Debug.WriteLine("Keystore file not found: " + keystorePath);
                    return false;
                }

                // 5. Build the arguments string for signing.
                string arguments = $"sign --ks \"{keystorePath}\" --ks-pass pass:{keystorePass} --key-pass pass:{keyPass} --ks-key-alias {keyAlias} \"{apkPath}\"";
                Debug.WriteLine("Signing arguments: " + arguments);

                // 6. Prepare the ProcessStartInfo.
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {extractedBatPath} {arguments}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = batDirectory
                };

                // 7. Set JAVA_HOME and JAVA_EXE and ensure Java's bin folder is in PATH.
                // Use the portable JRE located in the deployment package.
                // Here we assume the portable Java folder is stored in wwwroot/Tools/Java.
                // You can adjust this path as necessary.
                string javaHome = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "Tools", "Java");
                psi.Environment["JAVA_HOME"] = javaHome;
                string javaExe = Path.Combine(javaHome, "bin", "java.exe");
                psi.Environment["JAVA_EXE"] = javaExe;
                string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                psi.Environment["PATH"] = $"{Path.Combine(javaHome, "bin")};{currentPath}";
                Debug.WriteLine("JAVA_HOME set to: " + javaHome);
                Debug.WriteLine("JAVA_EXE set to: " + javaExe);
                Debug.WriteLine("PATH set to: " + psi.Environment["PATH"]);

                // 8. Execute the command.
                using (Process process = Process.Start(psi))
                {
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                    Debug.WriteLine("Signing output: " + output);
                    Debug.WriteLine("Signing error: " + error);
                    if (File.Exists(extractedBatPath))
                        File.Delete(extractedBatPath);
                    if (File.Exists(targetJarPath))
                        File.Delete(targetJarPath);
                    if (process.ExitCode != 0)
                    {
                        Debug.WriteLine("APK signing failed with exit code: " + process.ExitCode);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception during APK signing: " + ex.Message);
                return false;
            }
        }
    }
}
