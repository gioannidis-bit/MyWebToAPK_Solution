using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using MyWebToAPK.Helpers;

namespace MyWebToAPK.Controllers
{
    public class HomeController : Controller
    {
        // The base APK is located in wwwroot.
        private readonly string _baseApkPath;
        // The output folder where temporary builds will be stored.
        private readonly string _buildOutputBase = @"C:\Path\To\Output\Builds";
        private readonly IWebHostEnvironment _env;

        // IWebHostEnvironment is injected to gain access to the web root (wwwroot)
        public HomeController(IWebHostEnvironment env)
        {
            _env = env;
            // Build the full path to the base APK in wwwroot.
            _baseApkPath = Path.Combine(_env.WebRootPath, "com.hit.MyMauiTemplateProject-Signed-aligned.apk");
        }

        // GET: /Home/Index
        public IActionResult Index()
        {
            return View();
        }

        // POST: /Home/GenerateAPK
        [HttpPost]
        public async Task<IActionResult> GenerateAPK(string url, string apkName)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                ViewBag.Message = "URL is required.";
                return View("Index");
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                ViewBag.Message = "Please enter a valid URL.";
                return View("Index");
            }

            if (string.IsNullOrWhiteSpace(apkName))
            {
                ViewBag.Message = "APK file name is required.";
                return View("Index");
            }

            try
            {
                // Create a unique temporary folder for this build.
                string buildFolder = Path.Combine(_buildOutputBase, $"Build_{Guid.NewGuid()}");
                Directory.CreateDirectory(buildFolder);

                // Copy the base APK from wwwroot to the temporary folder.
                string tempApkPath = Path.Combine(buildFolder, apkName);
                System.IO.File.Copy(_baseApkPath, tempApkPath, true);

                // Update the APK configuration (for example, replacing a URL placeholder in config.json).
                bool configUpdated = await ApkHelper.UpdateApkConfigAsync(tempApkPath, url);
                if (!configUpdated)
                {
                    ViewBag.Message = "Failed to update the APK configuration.";
                    return View("Index");
                }

                // Extract the keystore from embedded resources.
                // (Ensure that your keystore file (my-release-key.keystore) is added to the Tools folder
                // with Build Action = Embedded Resource and “Do Not Copy” to output.)
                string keystoreResourceName = "MyWebToAPK.Tools.my-release-key.keystore";
                string extractedKeystorePath = ApkSignerHelper.EmbeddedResourceHelper.ExtractResourceToTempFile(keystoreResourceName);
                Debug.WriteLine("Extracted keystore path: " + extractedKeystorePath);

                // Use the new helper to zipalign and sign the APK.
                string keyAlias = "myalias";
                string keystorePass = "@Hit8847418"; // Replace with your actual keystore password
                string keyPass = "@Hit8847418";      // Replace with your actual key password

                bool signed = await ApkZipAlignSignerHelper.ZipAlignAndSignApkAsync(tempApkPath, extractedKeystorePath, keyAlias, keystorePass, keyPass);
                if (!signed)
                {
                    ViewBag.Message = "Failed to re-sign the APK.";
                    return View("Index");
                }

                // Optionally, delete the temporary extracted keystore file.
                if (System.IO.File.Exists(extractedKeystorePath))
                {
                    System.IO.File.Delete(extractedKeystorePath);
                }

                // Return the signed APK file for download.
                byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(tempApkPath);
                return File(fileBytes, "application/vnd.android.package-archive", apkName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception in GenerateAPK: " + ex.Message);
                ViewBag.Message = $"An error occurred: {ex.Message}";
                return View("Index");
            }
        }
    }
}
