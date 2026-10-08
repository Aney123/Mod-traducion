// Integration fixture, not shipped as a game assembly.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
[assembly: AssemblyVersion("1.6.9676.17735")]
namespace Verse
{
    public class ModContentPack { public string RootDir { get; set; } }
    public class Mod { public Mod(ModContentPack content) { } }
    [AttributeUsage(AttributeTargets.Class)] public class StaticConstructorOnStartup : Attribute { }
    public class LoadedLanguage { public string folderName; public string FriendlyNameEnglish { get { return folderName; } } }
    public static class LanguageDatabase { public static LoadedLanguage activeLanguage; }
    public static class Prefs { public static string LangFolderName { get; set; } }
    public static class Log
    {
        public static List<string> Errors = new List<string>();
        public static List<string> Warnings = new List<string>();
        public static void Message(string text) { Console.WriteLine(text); }
        public static void Error(string text) { Errors.Add(text); Console.WriteLine(text); }
        public static void Warning(string text) { Warnings.Add(text); Console.WriteLine(text); }
    }
    public struct TaggedString
    {
        private readonly string raw;
        public TaggedString(string text) { raw = text; }
        public override string ToString() { return raw; }
        public static implicit operator TaggedString(string text) { return new TaggedString(text); }
    }
    public static class Messages
    {
        public static string LastText;
        public static object LastTarget;
        public static object LastType;
        public static bool LastHistorical;
        public static int Delivered;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Message(string text, object target, object type, bool historical = true)
        { LastText = text; LastTarget = target; LastType = type; LastHistorical = historical; Delivered++; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Message(TaggedString text, object type, bool historical = true)
        { LastText = text.ToString(); LastTarget = null; LastType = type; LastHistorical = historical; Delivered++; }
    }
    public static class GenText
    {
        public static string ToLineList(IEnumerable<string> list, string prefix = "  - ")
        {
            StringBuilder result = new StringBuilder();
            foreach (string line in list) result.Append(prefix).Append(line).Append('\n');
            return result.ToString().TrimEnd('\n');
        }
    }
    public class Def { public string defName; public string label; public string description; public ModContentPack modContentPack; public List<object> modExtensions = new List<object>(); }
    public class ThingDef : Def { public List<object> comps = new List<object>(); public List<object> tools = new List<object>(); }
    public class JobDef : Def { public string reportString; }
    public class DesignationCategoryDef : Def { }
    public class RulePack { public List<string> rulesStrings = new List<string>(); }
    public class QuestScriptDef : Def
    { public RulePack questNameRules = new RulePack(); public RulePack questDescriptionRules = new RulePack(); }
    public class Tool { public string label; }
    public class PreceptDef : Def { public List<object> comps = new List<object>(); }
    public class LettersComp { public List<string> progressedLetters = new List<string>(); }
    public class ThoughtComp { public List<string> thoughtStageDescriptions = new List<string>(); }
    public static class GenDefDatabase
    {
        public static Dictionary<Type, List<Def>> Tables = new Dictionary<Type, List<Def>>();
        public static IEnumerable<Def> GetAllDefsInDatabaseForDef(Type type)
        { List<Def> list; return Tables.TryGetValue(type, out list) ? list : new List<Def>(); }
        public static void Add(Def def)
        { Type t=def.GetType(); if (!Tables.ContainsKey(t)) Tables[t]=new List<Def>(); Tables[t].Add(def); }
    }
    public static class LongEventHandler
    {
        private static List<Action> pending = new List<Action>();
        public static void ExecuteWhenFinished(Action action) { pending.Add(action); }
        public static void Finish() { foreach (Action action in pending) action(); pending.Clear(); }
    }
}
namespace FactionColonies
{
    public class FCPolicyDef : Verse.Def { public string desc; public List<string> positiveEffects = new List<string>(); public List<string> negativeEffects = new List<string>(); }
}
namespace MedievalOverhaul
{
    public class QuestInformation { public string label; }
}
