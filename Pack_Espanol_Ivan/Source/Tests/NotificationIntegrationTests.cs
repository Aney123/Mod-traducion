using System;
using System.IO;
using System.Reflection;
using PackEspanolIvan;
using Verse;

public static class NotificationIntegrationTests
{
    private const string Head = "Settlement construction: ";
    private const string Tail = " has begun constructing a new settlement near the edge of its territory.";
    private const string SpanishHead = "Construcción de asentamiento: ";
    private const string SpanishTail = " ha comenzado a construir un nuevo asentamiento cerca del límite de su territorio.";
    private static void Check(bool condition, string text) { if (!condition) throw new Exception(text); }
    public static void Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        string mode = args[1];
        Check(AutoTranslation.Services.TranslatorManager.CachedTranslationsV2 != null, "Engine fixture not loaded");
        Prefs.LangFolderName = mode == "latin" ? "SpanishLatin" : mode == "french" ? "French" : "Spanish";
        LanguageDatabase.activeLanguage = new LoadedLanguage { folderName = Prefs.LangFolderName };
        string screenshot = Head + "El clan de Oveja" + Tail;
        if (mode != "no-harmony") Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "0Harmony.dll"));
        new SpanishPackMod(new ModContentPack { RootDir = root });
        Bootstrap.TryApply(true);
        Check(NotificationCorrections.PatchedMethods == 0, "Installed before deferred loading finished");
        LongEventHandler.Finish();
        object target = new object(), type = new object();
        if (mode == "french" || mode == "no-harmony")
        {
            Check(NotificationCorrections.PatchedMethods == 0, "Unexpected hooks while inactive or dependency missing");
            Messages.Message(screenshot, target, type, false);
            Check(Messages.LastText == screenshot && Messages.LastTarget == target && Messages.LastType == type && !Messages.LastHistorical, "Unpatched message delivery changed");
            Check(Bootstrap.ImportedEntries == (mode == "french" ? 0 : Bootstrap.TotalEntries), "Lost existing cache when hook inactive");
            if (mode == "no-harmony") Check(Log.Warnings.Count > 0, "Missing Harmony not reported");
            Console.WriteLine("PASS notification-" + mode + "-delivery-and-cache-preserved");
            return;
        }
        Check(NotificationCorrections.PatchedMethods == 2 && NotificationCorrections.FailedMethods == 0, "Real Harmony did not install both overloads");
        Check(Log.Errors.Count == 0 && Log.Warnings.Count == 0, "Notification installation reported errors");
        Messages.Message(screenshot, target, type, false);
        Check(Messages.LastText == SpanishHead + "El clan de Oveja" + SpanishTail, "Screenshot notification untranslated");
        Check(Messages.LastTarget == target && Messages.LastType == type && !Messages.LastHistorical && Messages.Delivered == 1, "Changed message targets, type, history or delivery");
        string[] factions = { "La unión de Águila", "The O'Neill Union", "<color=#FF00FF>La tribu del Río</color>", "La facción {17}: Norte" };
        foreach (string faction in factions)
        {
            Messages.Message(new TaggedString(Head + faction + Tail), type, true);
            Check(Messages.LastText == SpanishHead + faction + SpanishTail, "TaggedString or dynamic faction name changed");
            Check(Messages.LastType == type && Messages.LastHistorical, "TaggedString message metadata changed");
        }
        string[] untouched = { "Construcción de asentamiento: El clan de Oveja ha comenzado a construir un nuevo asentamiento cerca del límite de su territorio.", "Settlement construction: Changed body", "Other heading: El clan de Oveja" + Tail, Head + Tail, Head + "  " + Tail, Head + "Faction\nOther text" + Tail, "Ordinary message", null };
        foreach (string original in untouched)
        {
            Messages.Message(original, target, type);
            Check(Messages.LastText == original, "Changed an unrelated, updated, malformed or native message");
        }
        Messages.Message("construcción de asentamiento: " + "El clan de Oveja" + Tail, target, type);
        Check(Messages.LastText == SpanishHead + "El clan de Oveja" + SpanishTail, "Definition-translated heading incompatible");
        int count = NotificationCorrections.PatchedMethods;
        NotificationCorrections.TryInstall();
        Check(NotificationCorrections.PatchedMethods == count, "Duplicate Harmony installation");
        LanguageDatabase.activeLanguage.folderName = "French";
        Messages.Message(screenshot, target, type);
        Check(Messages.LastText == screenshot, "Hook changed a message after leaving Spanish");
        LanguageDatabase.activeLanguage.folderName = "SpanishLatin";
        Messages.Message(screenshot, target, type);
        Check(Messages.LastText == SpanishHead + "El clan de Oveja" + SpanishTail, "SpanishLatin message untranslated");
        Console.WriteLine("PASS notification-" + mode + "-real-Harmony-string-TaggedString-factions-markup-targets-type-history-language-idempotence");
    }
}
