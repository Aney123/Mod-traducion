using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using PackEspanolIvan;
using Verse;
using FactionColonies;

public static class DirectIntegrationTests
{
    static void Check(bool value, string text) { if(!value) throw new Exception(text); }
    static string Spanish(XmlDocument doc, string type, string name, string path)
    { return doc.SelectSingleNode("/DirectTranslations/Rule[@Type='"+type+"' and @Def='"+name+"' and @Path='"+path+"']/Translated").InnerText.Replace("\\n","\n"); }
    public static void Main(string[] args)
    {
        Check(AutoTranslation.Services.TranslatorManager.CachedTranslationsV2 != null,"Engine fixture not loaded");
        string root=Path.GetFullPath(args[0]), mode=args.Length>1 ? args[1] : "screenshots";
        XmlDocument manifest=new XmlDocument();manifest.Load(Path.Combine(root,"Data/DirectTranslations.xml"));
        Prefs.LangFolderName=mode=="french" ? "French" : "Spanish";
        LanguageDatabase.activeLanguage=new LoadedLanguage { folderName=Prefs.LangFolderName };
        FCPolicyDef expansion=new FCPolicyDef { defName="expansionist",label="Expansionist" };
        expansion.positiveEffects.Add("- All new settlements cost 25% less to establish");
        expansion.positiveEffects.Add("- Settlements that you construct start at level 2");
        expansion.negativeEffects.Add("- New settlements start at 50% prosperity");
        expansion.negativeEffects.Add("- Rapid expansion strains governance (+0.5 unrest per day in every settlement)");
        FCPolicyDef military=new FCPolicyDef {defName="militaristic",label="Militaristic"};
        military.positiveEffects.Add("- +10% combat modifier when using your faction's military forces");
        military.positiveEffects.Add("- Once per five days, you can deploy a second squad from a single settlement by paying 40% of the squad's cost in Silver");
        military.positiveEffects.Add("- When creating a new settlement, it will start with a Barracks");
        military.positiveEffects.Add("- All military buildings cost 50 less upkeep");
        military.positiveEffects.Add("- Can send your military to raze another faction's settlement, destroying it and wiping it from the map");
        ThingDef door=new ThingDef {defName="AV_GarageDoorWater",label="water lock (1x5)",description="Divides rooms. Manual operation allows people to open it, letting vehicles through."};
        ThingDef preserve=new ThingDef {defName="AV_GarageDoorWooden",label="Mi puerta ya traducida",description="Texto cambiado por una actualización"};
        ThingDef helm=new ThingDef {defName="TA_SpectacleHelm",label="spectacle helm",description="Helmet made of iron plates attached into a conical shape, fitted with a nose guard and rings around the eyes, moderate amount of face protection"};
        ThingDef helmWithCoif=new ThingDef {defName="TA_SpectacleCombi",label="spectacle helm with coif",description="Helmet made of iron plates attached into a conical shape, fitted with a nose guard and rings around the eyes, worn with maille coif"};
        ThingDef keepTasty=new ThingDef {defName="TA_NasalHelm",label="Mi casco traducido",description="Nueva descripción de una actualización"};
        ThingDef nonLethal=new ThingDef {defName="VFES_Gun_NonLethalTurret",label="non-lethal turret gun",description="A non-lethal rubber bullet gun made to be mounted on a turret."};
        ThingDef prop=new ThingDef {defName="VFEPD_AncientNutrientDispenser",description="An ancient nutrient solution dispenser that was used to process and convert nutrient ointments into nutrient solutions, and then transport nutrient solutions for hydroponic cultivation through a pipeline system. Inactive."};
        PreceptDef alcohol=new PreceptDef {defName="VME_Alcohol_Demanded"};
        ThoughtComp thought=new ThoughtComp();thought.thoughtStageDescriptions.Add("Missing alcohol (3 days)");alcohol.comps.Add(thought);
        ThingDef chip=new ThingDef {defName="AlphaChip"};
        ThingDef fish=new ThingDef {defName="VCEF_RawAnchovy"};
        Tool fishTool=new Tool {label="fish"};fish.tools.Add(fishTool);
        LettersComp letters=new LettersComp();
        XmlNode chipOriginal=manifest.SelectSingleNode("/DirectTranslations/Rule[@Type='ThingDef' and @Def='AlphaChip' and @Path='comps.0.progressedLetters.0']/Original");
        letters.progressedLetters.Add(chipOriginal.InnerText);chip.comps.Add(letters);
        foreach(Def d in new Def[]{expansion,military,door,preserve,alcohol,chip,fish,helm,helmWithCoif,keepTasty,nonLethal,prop})GenDefDatabase.Add(d);
        if(mode=="atomic")
        {
            string temp=Path.GetTempFileName();File.WriteAllText(temp,"<DirectTranslations><Rule Type='ThingDef' Def='AV_GarageDoorWater' Path='label'><Original>water lock (1x5)</Original><Raw>water lock (1x5)</Raw><Translated>esclusa</Translated></Rule><Rule Type='ThingDef' Def='AV_GarageDoorWater' Path='defName'><Original>x</Original><Raw>x</Raw><Translated>z</Translated></Rule></DirectTranslations>");
            bool rejected=false;try{DirectTranslations.ApplyFromFile(temp);}catch(InvalidDataException){rejected=true;}File.Delete(temp);
            Check(rejected && door.label=="water lock (1x5)" && door.defName=="AV_GarageDoorWater","Unsafe manifest was not rejected atomically");
            Console.WriteLine("PASS direct-invalid-manifest-atomic");return;
        }
        new SpanishPackMod(new ModContentPack {RootDir=root});
        Check(door.label=="water lock (1x5)","Defs changed before loading completed");
        Bootstrap.TryApply(true);
        Check(door.label=="water lock (1x5)","Deferred translation ran before completion");
        Check(helm.label=="spectacle helm","New screenshot translated before Def loading completed");
        LongEventHandler.Finish();
        if(mode=="french")
        {Check(door.label=="water lock (1x5)" && helm.label=="spectacle helm" && expansion.label=="Expansionist" && Bootstrap.ImportedEntries==0,"Changed another language");Console.WriteLine("PASS direct-french-inactive");return;}
        Check(Bootstrap.ImportedEntries==Bootstrap.TotalEntries,"Missing supplemental entries");
        Check(expansion.label=="Expansionista","Custom Def type label not translated");
        for(int i=0;i<expansion.positiveEffects.Count;i++)Check(expansion.positiveEffects[i]==Spanish(manifest,"FactionColonies.FCPolicyDef","expansionist","positiveEffects."+i),"Expansionist advantage untranslated");
        for(int i=0;i<expansion.negativeEffects.Count;i++)Check(expansion.negativeEffects[i]==Spanish(manifest,"FactionColonies.FCPolicyDef","expansionist","negativeEffects."+i),"Expansionist drawback untranslated");
        for(int i=0;i<military.positiveEffects.Count;i++)Check(military.positiveEffects[i]==Spanish(manifest,"FactionColonies.FCPolicyDef","militaristic","positiveEffects."+i),"Militaristic advantage untranslated");
        Check(door.label=="esclusa (1x5)" && door.description==Spanish(manifest,"ThingDef","AV_GarageDoorWater","description"),"Patched water lock untranslated");
        Check(preserve.label=="Mi puerta ya traducida" && preserve.description=="Texto cambiado por una actualización","Existing native translation or updated source overwritten");
        Check(thought.thoughtStageDescriptions[0]=="Echa de menos el alcohol (3 días)","Nested custom list untranslated");
        Check(letters.progressedLetters[0].Contains("{PAWN_labelShort}") && letters.progressedLetters[0].Contains("\n") && !letters.progressedLetters[0].Contains("\\n"),"Research letter marker or newline broken");
        Check(fishTool.label=="pez","Literal XML patch target not translated");
        Check(helm.label=="casco con protección ocular","Reported Tasty Armory helmet label untranslated");
        Check(helm.description=="Un casco cónico formado por placas de hierro unidas, equipado con una protección para la nariz y aros alrededor de los ojos. Ofrece una protección moderada para el rostro.","Reported helmet description untranslated");
        Check(helmWithCoif.label=="casco con protección ocular y almófar" && helmWithCoif.description.Contains("almófar de malla"),"Vanilla branch helmet variant untranslated");
        Check(keepTasty.label=="Mi casco traducido" && keepTasty.description=="Nueva descripción de una actualización","Preserved Tasty values overwritten");
        Check(nonLethal.label=="arma de torreta no letal" && nonLethal.description.Contains("balas de goma"),"Other conditional mod folder untranslated");
        Check(prop.description.Contains("dispensador de solución nutritiva") && prop.description.EndsWith("Inactivo."),"Props conditional folder omitted");
        Check(AutoTranslation.Services.TranslatorManager.Translate("spectacle helm","","al9000.highmedieval")=="casco con protección ocular","Tasty cache key not available to engine");
        Check(helm.defName=="TA_SpectacleHelm" && helmWithCoif.defName=="TA_SpectacleCombi","Tasty technical identifiers changed");
        Check(expansion.defName=="expansionist" && door.defName=="AV_GarageDoorWater","Def identifiers changed");
        Check(DirectTranslations.Failed==0,"Direct translation threw field errors");
        DirectTranslations.ApplyFromFile(Path.Combine(root,"Data/DirectTranslations.xml"));
        Check(DirectTranslations.Applied==0,"Reapplication retranslates native Spanish");
        Console.WriteLine(mode=="tasty" ? "PASS tasty-screenshot-helmet-description-vanilla-variant-conditional-mods-cache-native-preserved" : "PASS direct-screenshots-custom-def-lists-patches-nested-fields-native-preserved-deferred-idempotent");
    }
}
