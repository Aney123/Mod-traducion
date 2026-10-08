using System;
using System.IO;
using System.Runtime.CompilerServices;
using PackEspanolIvan;
using AutoTranslation.Services;
using Verse;

public static class XmlIntegrationTests
{
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    public static void Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]), language = args[1];
        Prefs.LangFolderName = language;
        LanguageDatabase.activeLanguage = new LoadedLanguage { folderName = language };
        Check(TranslatorManager.CachedTranslationsV2.Count == 0, "Fixture cache was not empty");
        JobDef job = new JobDef { defName = "HaulToInventory", reportString = "gathering TargetA to hauling inventory." };
        DesignationCategoryDef hygiene = new DesignationCategoryDef { defName = "Hygiene", description = "Things for colonists' hygiene." };
        ThingDef bathtub = new ThingDef { defName = "BathtubStuff", label = "bathtub" };
        ThingDef shower = new ThingDef { defName = "ShowerStuff", label = "shower" };
        ThingDef scroll = new ThingDef { defName = "OrbitalTargeterBombardment", label = "magic missiles spell scroll" };
        ThingDef native = new ThingDef { defName = "Bathtub", label = "Bañera de otra traducción", description = "Descripción ya traducida" };
        QuestScriptDef minerals = new QuestScriptDef { defName = "LongRangeMineralScannerLump" };
        minerals.modExtensions.Add(new Tool { label = "Other extension" });
        MedievalOverhaul.QuestInformation information = new MedievalOverhaul.QuestInformation { label = "Minerals" };
        minerals.modExtensions.Add(information);
        QuestScriptDef quest = new QuestScriptDef { defName = "AncientComplex_Standard" };
        quest.questNameRules.rulesStrings.Add("questName(p=6)->The [complexAdj] [complex]");
        quest.questDescriptionRules.rulesStrings.Add("questDescription->You've recieved word of an ancient outpost nearby. It is believed to contain ancient texts that may have information about [relic_name].\\n\\nIf you can break in and decipher the ancient texts inside, you can collect the information.\\n\\nWatch out - there may be all manner of dangers inside, and other enemies may also be attracted by activity at the location.");
        foreach (Def def in new Def[] { job, hygiene, bathtub, shower, scroll, native, quest, minerals }) GenDefDatabase.Add(def);
        new SpanishPackMod(new ModContentPack { RootDir = root });
        RuntimeHelpers.RunClassConstructor(typeof(FinishLoading).TypeHandle);
        Check(job.reportString == "gathering TargetA to hauling inventory.", "New XML translated before Def loading completed");
        LongEventHandler.Finish();
        Check(Bootstrap.ImportedEntries == Bootstrap.TotalEntries, "Incomplete v1.4 cache import");
        Check(job.reportString == "recogiendo TargetA en el inventario de transporte.", "Hauling report or TargetA changed");
        Check(hygiene.description == "Objetos para la higiene de los colonos.", "Hygiene category description untranslated");
        Check(bathtub.label == "bañera" && shower.label == "ducha", "New hygiene building labels untranslated");
        Check(scroll.label == "pergamino de proyectiles mágicos", "Medieval literal XML patch omitted");
        Check(information.label == "Minerales" && ((Tool)minerals.modExtensions[0]).label == "Other extension", "Appended quest extension omitted or unrelated extension overwritten");
        Check(native.label == "Bañera de otra traducción" && native.description == "Descripción ya traducida", "Native translation overwritten");
        Check(quest.questNameRules.rulesStrings[0] == "questName(p=6)->El [complex] [complexAdj]", "Quest rule head or ordered grammar tokens changed");
        string description = quest.questDescriptionRules.rulesStrings[0];
        Check(description.StartsWith("questDescription->Has recibido noticias de un puesto avanzado antiguo cercano.") && description.Contains("[relic_name]") && description.Contains("\n\n") && !description.Contains("\\n") && description.EndsWith("atraer a otros enemigos."), "Quest description, grammar token or display newlines lost");
        Check(TranslatorManager.Translate("Allow putting corpses into inventory", "PUAH.allowCorpses", "mehni.pickupandhaul") == "Permitir poner cadáveres en el inventario", "Reused keyed translation missing");
        string color = TranslatorManager.Translate("Edit color for {0}", "RimHUD.Interface.Dialog_Config.Tab_Colors.Editor", "jaxe.rimhud");
        Check(String.Format(color, "energía") == "Editar color de energía", "New RimHUD keyed placeholder failed");
        string fishing = TranslatorManager.Translate("This zone is too small (size {0}). Fishing zones need to be at least {1} tiles to be valid (this calculation is made every few seconds: unpause to update).", "VCEF_ZoneTooSmall", "vanillaexpanded.vcef");
        string formatted = String.Format(fishing, 8, 25);
        Check(formatted.Contains("(tamaño 8)") && formatted.Contains("al menos 25 casillas"), "Repaired fishing placeholders give the wrong counts");
        Check(TranslatorManager.Translate("[Expand]", "MissileGirl.Collapsible.Expand", "vr.missilegirl") == "[Expandir]", "Bracketed UI label treated as a grammar token");
        Check(job.defName == "HaulToInventory" && scroll.defName == "OrbitalTargeterBombardment" && quest.defName == "AncientComplex_Standard", "Technical Def names changed");
        Check(DirectTranslations.Failed == 0, "New direct fields caused reflection errors");
        DirectTranslations.ApplyFromFile(Path.Combine(root, "Data/DirectTranslations.xml"));
        Check(DirectTranslations.Applied == 0 && information.label == "Minerales", "New XML or appended extension translated twice");
        Console.WriteLine("PASS xml-v1.4-" + language + "-hygiene-hauling-magic-quests-keyed-placeholder-repair-native-preserved");
    }
}
