using System;
using System.IO;
using PackEspanolIvan;
using Verse;
public static class NoEngineTest
{
    public static void Main(string[] args)
    {
        Prefs.LangFolderName = "Spanish";
        LanguageDatabase.activeLanguage = new LoadedLanguage { folderName = "Spanish" };
        new SpanishPackMod(new ModContentPack { RootDir = Path.GetFullPath(args[0]) });
        if (Bootstrap.ImportedEntries != 0 || Log.Errors.Count != 1 || !Log.Errors[0].Contains("Auto Translation"))
            throw new Exception("Missing dependency was not handled clearly");
        Console.WriteLine("PASS missing-dependency-without-crash");
    }
}
