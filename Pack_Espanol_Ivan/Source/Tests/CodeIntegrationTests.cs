using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;
using HarmonyLib;
using InspectedMod;
using PackEspanolIvan;
using Verse;
public static class CodeIntegrationTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Add(XmlDocument doc, MethodBase method, string original, string target, Guid? mvid = null)
    {
        XmlElement rule = doc.CreateElement("Rule");
        rule.SetAttribute("Assembly", method.Module.Assembly.GetName().Name);
        rule.SetAttribute("Mvid", (mvid ?? method.Module.ModuleVersionId).ToString("D"));
        rule.SetAttribute("Token", method.MetadataToken.ToString());
        rule.SetAttribute("Type", method.DeclaringType.FullName); rule.SetAttribute("Method", method.Name);
        XmlElement en = doc.CreateElement("Original"); en.InnerText = original; rule.AppendChild(en);
        XmlElement es = doc.CreateElement("Translated"); es.InnerText = target; rule.AppendChild(es);
        doc.DocumentElement.AppendChild(rule);
    }
    public static IEnumerable<CodeInstruction> NativeTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        { if (instruction.opcode == OpCodes.Ldstr && (string)instruction.operand == "Cancel") instruction.operand = "Cancelar nativo"; yield return instruction; }
    }
    private static void InstallNative()
    {
        Harmony owner = new Harmony("integration.native-spanish");
        HarmonyMethod descriptor = new HarmonyMethod(typeof(CodeIntegrationTests).GetMethod("NativeTranspiler")); descriptor.priority = Priority.First;
        owner.Patch(typeof(Display).GetMethod("NativeText"), transpiler: descriptor);
    }
    public static void Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]), mode = args[1];
        string package = Path.Combine(Path.GetDirectoryName(root), "code_" + mode + "_package"); Directory.CreateDirectory(Path.Combine(package, "Data"));
        foreach (string file in Directory.GetFiles(Path.Combine(root, "Data"))) File.Copy(file, Path.Combine(package, "Data", Path.GetFileName(file)), true);
        Prefs.LangFolderName = mode == "french" ? "French" : mode == "latin" ? "SpanishLatin" : "Spanish";
        LanguageDatabase.activeLanguage = new LoadedLanguage { folderName = Prefs.LangFolderName };
        Check(AutoTranslation.Services.TranslatorManager.CachedTranslationsV2 != null, "Engine fixture not loaded");
        Check(Display.EarlyLabel == "Save" && Display.Initializations == 1, "Initializer setup failed");
        XmlDocument doc = new XmlDocument(); XmlElement manifest = doc.CreateElement("CodeTranslations"); manifest.SetAttribute("Language", "Spanish"); doc.AppendChild(manifest);
        Add(doc, typeof(Display).GetConstructor(Type.EmptyTypes), "Save", "Guardar");
        Add(doc, typeof(Display).TypeInitializer, "Save", "Guardar");
        Add(doc, typeof(Display).GetMethod("TechnicalAndUI"), "Cancel", "Cancelar");
        Add(doc, typeof(Display).GetMethod("Format"), "Cost: <color=cyan>{0:N0}</color>", "Coste: <color=cyan>{0:N0}</color>");
        Add(doc, typeof(Display).GetMethod("Send"), "Settlement construction: ", "Construcción de asentamiento: ");
        Add(doc, typeof(Display).GetMethod("Send"), " has begun constructing a new settlement near the edge of its territory.", " ha comenzado a construir un nuevo asentamiento cerca del límite de su territorio.");
        foreach (string method in new[] { "Branch", "ExceptionBranch" })
        { Add(doc, typeof(Display).GetMethod(method), "Apply", "Aplicar"); Add(doc, typeof(Display).GetMethod(method), "Cancel", "Cancelar"); }
        Add(doc, typeof(Display).GetMethod("NativeText"), "Cancel", "Cancelar");
        Add(doc, typeof(Display).GetMethod("Generic"), "Save", "Guardar");
        foreach (Type nested in typeof(Display).GetNestedTypes(BindingFlags.NonPublic))
            if (nested.Name.StartsWith("<Options>")) Add(doc, nested.GetMethod("MoveNext"), "Open folder", "Abrir carpeta");
        Add(doc, typeof(Display).GetMethod("Branch"), "Unused", "Sin usar", new Guid("11111111-1111-1111-1111-111111111111"));
        string path = Path.Combine(package, "Data", "CodeTranslations.xml");
        if (mode == "invalid")
        { XmlElement wrong = doc.CreateElement("Unexpected"); manifest.AppendChild(wrong); }
        doc.Save(path);
        if (mode == "dtd") File.WriteAllText(path, "<!DOCTYPE CodeTranslations [<!ENTITY x 'oops'>]><CodeTranslations Language='Spanish'>&x;</CodeTranslations>");
        if (mode == "unknown")
        { foreach (XmlElement element in manifest.ChildNodes) element.SetAttribute("Mvid", "11111111-1111-1111-1111-111111111111");
          // Give each rule a unique token to retain a valid unknown-module manifest.
          int number = 1; foreach (XmlElement element in manifest.ChildNodes) element.SetAttribute("Token", (0x06000000 + number++).ToString()); doc.Save(path); }
        if (mode != "no-harmony") InstallNative();
        new SpanishPackMod(new ModContentPack { RootDir = package }); Bootstrap.TryApply(true);
        Check(CodeTranslations.PatchedMethods == 0, "Code installed before deferred loading finished"); LongEventHandler.Finish();
        if (mode == "french" || mode == "no-harmony" || mode == "invalid" || mode == "dtd" || mode == "unknown")
        {
            Check(CodeTranslations.PatchedMethods == 0 && Display.TechnicalAndUI()[0] == "Cancel" && Display.Branch(true) == "Apply", "Inactive or invalid rules changed code");
            Check(Bootstrap.ImportedEntries == (mode == "french" ? 0 : Bootstrap.TotalEntries), "Code integration lost the existing cache");
            if (mode == "invalid" || mode == "dtd" || mode == "no-harmony") Check(Log.Warnings.Count > 0, "Failed rules or missing Harmony not reported");
            if (mode == "unknown") Check(CodeTranslations.MissingMethods > 0, "Unknown assembly version not skipped");
            Console.WriteLine("PASS code-" + mode + "-atomic-and-cache-preserved"); return;
        }
        Check(CodeTranslations.PatchedMethods == 8 && CodeTranslations.FailedMethods == 0 && CodeTranslations.MissingMethods == 1 && CodeTranslations.UnsupportedMethods == 2, "Unexpected patch counts or adapter failure");
        string[] texts = Display.TechnicalAndUI();
        Check(texts[0] == "Cancelar" && texts[1] == "setting_key" && texts[2] == "Textures/Cancel" && texts[3] == "CancelDef" && texts[4] == "PAWN", "Technical keys changed");
        Check(Display.Format(1200).StartsWith("Coste: <color=cyan>") && Display.Format(1200).EndsWith("</color>"), "Formatting or tags changed");
        object target = new object(), type = new object(); string faction = "<color=#FF00FF>El clan de Oveja</color>";
        Display.Send(faction, target, type);
        Check(Messages.LastText == "Construcción de asentamiento: " + faction + " ha comenzado a construir un nuevo asentamiento cerca del límite de su territorio.", "Faction message not translated");
        Check(Messages.LastTarget == target && Messages.LastType == type && !Messages.LastHistorical && Messages.Delivered == 1, "Message behavior changed");
        Check(new Display().Label == "Guardar", "Instance constructor not translated");
        Check(Display.Initializations == 1 && Display.EarlyLabel == "Save", "Static initializer replayed");
        Check(Display.Branch(true) == "Aplicar" && Display.Branch(false) == "Cancelar", "Branch labels damaged");
        Check(Display.ExceptionBranch(true) == "Cancelar" && Display.ExceptionBranch(false) == "Aplicar", "Exception blocks damaged");
        Check(Display.NativeText() == "Cancelar nativo", "Other mod's native Spanish overwritten");
        List<string> options = new List<string>(Display.Options()); Check(options[0] == "Abrir carpeta" && options[1] == "Textures/OpenFolder", "Iterator changed technical text");
        int count = CodeTranslations.PatchedMethods; CodeTranslations.TryInstallFromFile(path); Check(count == CodeTranslations.PatchedMethods, "Duplicate code patch");
        LanguageDatabase.activeLanguage.folderName = "French"; Check(Display.Branch(true) == "Apply" && new Display().Label == "Save", "Spanish survived a language change");
        LanguageDatabase.activeLanguage.folderName = "SpanishLatin"; Check(Display.Branch(true) == "Aplicar", "SpanishLatin runtime change failed");
        Check(Log.Errors.Count == 0 && Log.Warnings.Count == 0, "Unexpected integration warnings");
        Console.WriteLine("PASS code-" + mode + "-real-Harmony-exact-methods-keys-variables-tags-branches-exceptions-iterator-constructors-native-language-idempotence");
    }
}
