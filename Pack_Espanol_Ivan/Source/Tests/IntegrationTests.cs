using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using AutoTranslation;
using AutoTranslation.Services;
using PackEspanolIvan;
using Verse;

public static class IntegrationTests
{
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
    }
    private static void VerifyEveryOriginal(string root)
    {
        XmlDocument doc = new XmlDocument(); doc.Load(Path.Combine(root, "Data", "translations.xml"));
        int checkedCount = 0;
        foreach (XmlNode child in doc.DocumentElement.ChildNodes)
        {
            XmlElement entry = child as XmlElement; if (entry == null) continue;
            string key = Encoding.UTF8.GetString(Convert.FromBase64String(entry.GetAttribute("Key")));
            Check(TranslatorManager.CachedTranslationsV2[key] == entry.InnerText, "Altered live value: " + key);
            Check(TranslationCacheManager.Instance.Cache[key] == entry.InnerText, "Altered stored value: " + key);
            checkedCount++;
        }
        Check(checkedCount == 41850, "Incorrect complete payload count");
        doc.Load(Path.Combine(root, "Data", "Correcciones.xml"));
        checkedCount = 0;
        foreach (XmlNode child in doc.DocumentElement.ChildNodes)
        {
            XmlElement entry = child as XmlElement; if (entry == null) continue;
            string key = Encoding.UTF8.GetString(Convert.FromBase64String(entry.GetAttribute("Key")));
            Check(TranslatorManager.CachedTranslationsV2[key] == entry.InnerText, "Altered supplemental live value: " + key);
            Check(TranslationCacheManager.Instance.Cache[key] == entry.InnerText, "Altered supplemental stored value: " + key);
            checkedCount++;
        }
        Check(checkedCount == Bootstrap.SupplementEntries, "Missing supplemental cache entries");
    }
    public static void Main(string[] args)
    {
        string mode = args[0], root = Path.GetFullPath(args[1]);
        if (mode == "grammar")
        {
            Dictionary<string, string> sample = new Dictionary<string, string>();
            sample["Mod.ID:faction->[name] rules [name] and {NUM}"] = "faction->{NUM} domina [name] y [name]";
            sample["Mod.ID:label"] = "Etiqueta original";
            sample["mod.id:label"] = "Etiqueta con clave exacta";
            sample["Mod.ID:line one\\nline two"] = "línea uno\\nlínea dos";
            Dictionary<string, string> compatible = Bootstrap.BuildCompatiblePayload(sample, typeof(TranslatorManager).Assembly);
            List<string> tokens = new List<string> { "[name]", "[name]", "{NUM}" };
            string key = "mod.id:" + ("{0} rules {1} and {2}faction" + GenText.ToLineList(tokens)).Trim();
            Check(compatible[key] == "{2} domina {0} y {1}", "Grammar markers lost their original order");
            Check(String.Format(compatible[key], "A", "B", "7") == "7 domina A y B", "Formatted grammar gives wrong text");
            Check(compatible["mod.id:label"] == "Etiqueta con clave exacta", "Alias replaced an exact original");
            Check(compatible["Mod.ID:faction->[name] rules [name] and {NUM}"] == sample["Mod.ID:faction->[name] rules [name] and {NUM}"], "Grammar adapter altered original data");
            Check(compatible["mod.id:line one\nline two"] == "línea uno\nlínea dos", "XML line breaks were not adapted for display");
            Check(compatible["Mod.ID:line one\\nline two"] == sample["Mod.ID:line one\\nline two"], "Original escaped line breaks changed");
            Console.WriteLine("PASS grammar-reordered-and-repeated-placeholders");
            return;
        }
        TranslatorManager.CachedTranslationsV2["another.mod:Existing text"] = "Texto propio";
        TranslationCacheManager.Instance.Cache["another.mod:Existing text"] = "Texto propio";
        Prefs.LangFolderName = mode == "other-language" ? "French" : mode == "latin" ? "SpanishLatin" : "Spanish";
        if (mode != "late") LanguageDatabase.activeLanguage = new LoadedLanguage { folderName = Prefs.LangFolderName };
        if (mode == "late") Prefs.LangFolderName = null;
        new SpanishPackMod(new ModContentPack { RootDir = root });
        if (mode == "late")
        {
            Check(Bootstrap.ImportedEntries == 0, "Loaded without a known language");
            Prefs.LangFolderName = "Spanish";
            LanguageDatabase.activeLanguage = new LoadedLanguage { folderName = "Spanish" };
            RuntimeHelpers.RunClassConstructor(typeof(FinishLoading).TypeHandle);
            Check(InjectionManager.Reapplications == 1 && Settings.Resets == 1, "Late injection did not refresh text");
        }
        if (mode == "other-language" || mode == "invalid")
        {
            Check(Bootstrap.ImportedEntries == 0 && TranslatorManager.CachedTranslationsV2.Count == 1, "Cache changed before validation");
            Check(Settings.EnableAutoTranslation && TranslatorManager.Ready, "Changed translator settings while inactive");
            if (mode == "invalid") Check(Log.Errors.Count > 0, "Invalid package was not reported");
        }
        else
        {
            Check(Bootstrap.ImportedEntries == Bootstrap.TotalEntries, "Did not import complete cache and corrections");
            VerifyEveryOriginal(root);
            Check(TranslatorManager.CachedTranslationsV2["another.mod:Existing text"] == "Texto propio", "Unrelated live cache lost");
            Check(TranslationCacheManager.Instance.Cache["another.mod:Existing text"] == "Texto propio", "Unrelated stored cache lost");
            bool automaticExpected = mode == "automatic";
            Check(Settings.EnableAutoTranslation == automaticExpected && TranslatorManager.Ready == automaticExpected, "Wrong cache-only mode");
            Check(TranslatorManager.Translate("Autoplay", "RedHorse_AutoPlay", "cp.uncle.boris.used.furniture") == "Reproducción automática", "Keyed package alias did not match");
            Check(TranslatorManager.Translate("Smoke grenade launcher", "", "rimthunder.core") == "lanzador de granadas de humo", "Def text alias did not match");
            Console.WriteLine("ENTRIES=" + Bootstrap.ImportedEntries + " ALIASES=" + Bootstrap.CompatibilityAliases + " GRAMMAR=" + Bootstrap.GrammarAliases);
        }
        Console.WriteLine("PASS " + mode);
    }
}
