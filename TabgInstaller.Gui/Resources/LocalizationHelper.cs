using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TabgInstaller.Gui.Resources;

public static class LocalizationHelper
{
    public static List<CultureInfo> GetAvailableLanguages()
    {
        var cultures = new List<CultureInfo> { CultureInfo.GetCultureInfo("en") };

        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var dir in Directory.GetDirectories(baseDir))
            {
                var folderName = Path.GetFileName(dir);
                try
                {
                    var culture = CultureInfo.GetCultureInfo(folderName);
                    var satPath = Path.Combine(dir, "TabgInstaller.Gui.resources.dll");
                    if (File.Exists(satPath))
                        cultures.Add(culture);
                }
                catch (CultureNotFoundException) { }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalizationHelper] Error scanning for languages: {ex.Message}");
        }

        return cultures.OrderBy(c => c.NativeName).ToList();
    }

    public static void ApplyLanguage(string languageCode)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(languageCode);
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            System.Threading.Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en");
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en");
        }
    }
}
