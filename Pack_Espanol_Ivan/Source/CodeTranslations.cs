using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;

namespace PackEspanolIvan
{
    // Optional Harmony integration, discovered without a linked dependency.
    // Match the inspected module, method and original display literal exactly.
    // The original assembly, identifiers, save data and other IL stay intact.
    public static class CodeTranslations
    {
        private sealed class MethodRules
        {
            public string Assembly, Type, Name, Mvid;
            public int Token;
            public readonly Dictionary<string, string> Texts = new Dictionary<string, string>(StringComparer.Ordinal);
        }
        private const string HarmonyId = "ivan.rimworld.traducciones.es.code";
        private static readonly Dictionary<string, MethodRules> Rules = new Dictionary<string, MethodRules>(StringComparer.Ordinal);
        private static readonly HashSet<string> Patched = new HashSet<string>(StringComparer.Ordinal);
        private static Type instructionType;
        private static FieldInfo opcodeField, operandField, blocksField;
        private static ConstructorInfo instructionConstructor;
        // Keep the generated adapter alive for the lifetime of Harmony's patch.
        private static DynamicMethod adapter;
        public static int PatchedMethods { get { return Patched.Count; } }
        public static int FailedMethods { get; private set; }
        public static int MissingMethods { get; private set; }
        public static int UnsupportedMethods { get; private set; }
        public static int MatchedLiterals { get; private set; }

        private static string Key(Guid mvid, int token) { return mvid.ToString("D") + ":" + token.ToString(); }
        private static List<MethodRules> ReadRules(string path)
        {
            XmlReaderSettings options = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            XmlDocument document = new XmlDocument { XmlResolver = null };
            using (XmlReader reader = XmlReader.Create(path, options)) document.Load(reader);
            if (document.DocumentElement == null || document.DocumentElement.Name != "CodeTranslations" || document.DocumentElement.GetAttribute("Language") != "Spanish")
                throw new InvalidDataException("Formato incorrecto de traducciones del código.");
            Dictionary<string, MethodRules> parsed = new Dictionary<string, MethodRules>(StringComparer.Ordinal);
            foreach (XmlNode node in document.DocumentElement.ChildNodes)
            {
                XmlElement e = node as XmlElement;
                if (e == null) continue;
                Guid mvid; int token;
                if (e.Name != "Rule" || !Guid.TryParse(e.GetAttribute("Mvid"), out mvid) || !Int32.TryParse(e.GetAttribute("Token"), out token) || (token & unchecked((int)0xFF000000)) != 0x06000000)
                    throw new InvalidDataException("Identidad de método inválida.");
                string assembly = e.GetAttribute("Assembly"), type = e.GetAttribute("Type"), name = e.GetAttribute("Method");
                XmlNode original = e.SelectSingleNode("Original"), translated = e.SelectSingleNode("Translated");
                if (String.IsNullOrEmpty(assembly) || String.IsNullOrEmpty(type) || String.IsNullOrEmpty(name) || original == null || translated == null || String.IsNullOrEmpty(original.InnerText) || String.IsNullOrEmpty(translated.InnerText))
                    throw new InvalidDataException("Traducción del código incompleta.");
                string key = Key(mvid, token); MethodRules method;
                if (!parsed.TryGetValue(key, out method))
                {
                    method = new MethodRules { Assembly = assembly, Type = type, Name = name, Mvid = mvid.ToString("D"), Token = token };
                    parsed.Add(key, method);
                }
                if (method.Assembly != assembly || method.Type != type || method.Name != name || method.Texts.ContainsKey(original.InnerText))
                    throw new InvalidDataException("Regla del código repetida o incompatible.");
                method.Texts.Add(original.InnerText, translated.InnerText);
            }
            return new List<MethodRules>(parsed.Values);
        }

        // Evaluate the current language when the UI runs, rather than baking a
        // Spanish value into English/French after a language change.
        public static string SelectLiteral(string original, string translated)
        {
            try { return Bootstrap.SpanishIsSelected() ? translated : original; }
            catch { return original; }
        }
        private static object Instruction(OpCode code, object operand)
        {
            return instructionConstructor.Invoke(new object[] { code, operand });
        }
        private static void MoveExceptionEnds(object from, object to)
        {
            // A try block ending on this ldstr must still include its replacement
            // call. Entry labels and all other exception markers remain in place.
            if (blocksField == null) return;
            IList oldBlocks = blocksField.GetValue(from) as IList, newBlocks = blocksField.GetValue(to) as IList;
            if (oldBlocks == null || newBlocks == null) return;
            for (int i = 0; i < oldBlocks.Count; )
            {
                object block = oldBlocks[i];
                FieldInfo kind = block.GetType().GetField("blockType");
                if (kind != null && kind.GetValue(block).ToString() == "EndExceptionBlock")
                { newBlocks.Add(block); oldBlocks.RemoveAt(i); }
                else i++;
            }
        }
        public static IEnumerable Rewrite(IEnumerable instructions, MethodBase __originalMethod)
        {
            IList output = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(instructionType));
            MethodRules method;
            if (__originalMethod == null || !Rules.TryGetValue(Key(__originalMethod.Module.ModuleVersionId, __originalMethod.MetadataToken), out method))
            {
                foreach (object item in instructions) output.Add(item);
                return output;
            }
            MethodInfo choose = typeof(CodeTranslations).GetMethod("SelectLiteral");
            foreach (object instruction in instructions)
            {
                output.Add(instruction);
                if (!((OpCode)opcodeField.GetValue(instruction)).Equals(OpCodes.Ldstr)) continue;
                string original = operandField.GetValue(instruction) as string, translated;
                if (original == null || !method.Texts.TryGetValue(original, out translated)) continue;
                object replacement = Instruction(OpCodes.Call, choose);
                MoveExceptionEnds(instruction, replacement);
                output.Add(Instruction(OpCodes.Ldstr, translated));
                output.Add(replacement);
                MatchedLiterals++;
            }
            return output;
        }
        private static MethodInfo MakeAdapter()
        {
            Type stream = typeof(IEnumerable<>).MakeGenericType(instructionType);
            adapter = new DynamicMethod("IvanSpanishDisplayLiterals", stream, new[] { stream, typeof(MethodBase) }, typeof(CodeTranslations), true);
            adapter.DefineParameter(1, ParameterAttributes.None, "instructions");
            adapter.DefineParameter(2, ParameterAttributes.None, "__originalMethod");
            ILGenerator il = adapter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof(CodeTranslations).GetMethod("Rewrite"));
            il.Emit(OpCodes.Castclass, stream); il.Emit(OpCodes.Ret);
            return adapter;
        }
        // Harmony serializes normal method identities and asks this factory
        // for the generated, version-specific instruction adapter when needed.
        public static MethodInfo TranspilerFactory(MethodBase originalMethod)
        {
            return adapter ?? MakeAdapter();
        }
        public static void TryInstallFromFile(string path)
        {
            if (!File.Exists(path) || !Bootstrap.SpanishIsSelected()) return;
            try
            {
                // Parse and validate every rule before installing any patch.
                List<MethodRules> wanted = ReadRules(path);
                Type harmony = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    harmony = assembly.GetType("HarmonyLib.Harmony", false);
                    if (harmony != null) break;
                }
                if (harmony == null) throw new InvalidOperationException("No se encontró Harmony para las traducciones del código.");
                Type descriptor = harmony.Assembly.GetType("HarmonyLib.HarmonyMethod", true);
                instructionType = harmony.Assembly.GetType("HarmonyLib.CodeInstruction", true);
                opcodeField = instructionType.GetField("opcode"); operandField = instructionType.GetField("operand"); blocksField = instructionType.GetField("blocks");
                instructionConstructor = instructionType.GetConstructor(new[] { typeof(OpCode), typeof(object) });
                if (opcodeField == null || operandField == null || instructionConstructor == null) throw new MissingMemberException("La API de instrucciones de Harmony no es compatible.");
                object owner = Activator.CreateInstance(harmony, new object[] { HarmonyId });
                object transpiler = Activator.CreateInstance(descriptor, new object[] { typeof(CodeTranslations).GetMethod("TranspilerFactory") });
                MethodInfo patch = null; int transpilerIndex = -1;
                foreach (MethodInfo candidate in harmony.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (candidate.Name != "Patch") continue;
                    ParameterInfo[] parameters = candidate.GetParameters();
                    if (parameters.Length == 0 || parameters[0].ParameterType != typeof(MethodBase)) continue;
                    for (int i = 1; i < parameters.Length; i++)
                        if (parameters[i].Name == "transpiler" && parameters[i].ParameterType == descriptor)
                        { patch = candidate; transpilerIndex = i; break; }
                    if (patch != null) break;
                }
                if (patch == null) throw new MissingMethodException("Harmony no expone el punto de conexión de transpilers.");
                Dictionary<Guid, Module> loaded = new Dictionary<Guid, Module>();
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    try { foreach (Module module in assembly.GetModules()) loaded[module.ModuleVersionId] = module; }
                    catch { }
                foreach (MethodRules rule in wanted)
                {
                    string key = rule.Mvid + ":" + rule.Token;
                    if (Patched.Contains(key)) continue;
                    try
                    {
                        Module module;
                        if (!loaded.TryGetValue(new Guid(rule.Mvid), out module)) { MissingMethods++; continue; }
                        if (module.Assembly.GetName().Name != rule.Assembly) { MissingMethods++; continue; }
                        MethodBase method = module.ResolveMethod(rule.Token);
                        if (method == null || method.Name != rule.Name || method.DeclaringType == null || method.DeclaringType.FullName != rule.Type)
                        { MissingMethods++; continue; }
                        // Already-run static initializers must never be replayed;
                        // open generic bodies cannot safely be shared by Harmony.
                        if (method.Name == ".cctor" || method.ContainsGenericParameters || method.GetMethodBody() == null)
                        { UnsupportedMethods++; continue; }
                        Rules[key] = rule;
                        ParameterInfo[] inputs = patch.GetParameters(); object[] values = new object[inputs.Length];
                        values[0] = method; values[transpilerIndex] = transpiler;
                        for (int i = 1; i < values.Length; i++) if (i != transpilerIndex) values[i] = inputs[i].IsOptional ? Type.Missing : null;
                        patch.Invoke(owner, values); Patched.Add(key);
                    }
                    catch (Exception error)
                    {
                        FailedMethods++;
                        Exception reason = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                        Bootstrap.WriteLog("Warning", "Se omitió una traducción del código de " + rule.Assembly + ": " + reason.Message);
                    }
                }
                Bootstrap.WriteLog("Message", "Version 1.4: código traducido en " + PatchedMethods + " métodos; versiones ausentes: " + MissingMethods + "; métodos no compatibles: " + UnsupportedMethods + "; errores: " + FailedMethods + ".");
            }
            catch (Exception error)
            {
                Exception reason = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                Bootstrap.WriteLog("Warning", "Las traducciones locales siguen activas; no se pudo cargar la ampliación del código: " + reason.Message);
            }
        }
    }
}
