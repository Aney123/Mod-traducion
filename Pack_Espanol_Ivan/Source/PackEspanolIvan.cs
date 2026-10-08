using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Verse;

namespace PackEspanolIvan
{
    // Only Verse.Mod and its constructor are linked to the game. The cache API
    // is discovered at runtime, so this DLL never bundles AutoTranslation.
    public sealed class SpanishPackMod : Mod
    {
        public SpanishPackMod(ModContentPack content) : base(content)
        {
            Bootstrap.SetRoot(content);
            Bootstrap.TryApply(false);
        }
    }

    [StaticConstructorOnStartup]
    public static class FinishLoading
    {
        static FinishLoading() { Bootstrap.TryApply(true); }
    }

    public static class Bootstrap
    {
        public const int ExpectedEntries = 41850;
        public const int SupplementEntries = 7766;
        public const int TotalEntries = ExpectedEntries + SupplementEntries;
        private const string Prefix = "[Pack Espanol de Ivan] ";
        private static readonly BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly Regex RuleHead = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*(?:\([^\r\n]*?\))?$", RegexOptions.CultureInvariant);
        private static readonly Regex RuleBoundary = new Regex(@"(?:\r?\n|\\n)(?=\s*[A-Za-z_][A-Za-z0-9_]*(?:\([^\r\n]*?\))?->)", RegexOptions.CultureInvariant);
        private static string root;
        private static bool applied;
        private static bool reportedError;
        private static bool directQueued;
        public static int ImportedEntries { get; private set; }
        public static int CompatibilityAliases { get; private set; }
        public static int GrammarAliases { get; private set; }

        internal static void SetRoot(object content)
        {
            root = ReadMember(content.GetType(), content, "RootDir") as string;
        }

        private static object ReadMember(Type type, object instance, string name)
        {
            if (type == null) return null;
            BindingFlags flags = instance == null ? AnyStatic : AnyInstance;
            FieldInfo field = type.GetField(name, flags);
            if (field != null) return field.GetValue(instance);
            PropertyInfo property = type.GetProperty(name, flags);
            return property == null ? null : property.GetValue(instance, null);
        }

        private static bool IsSpanish(string name)
        {
            return !String.IsNullOrEmpty(name) &&
                (name.IndexOf("Spanish", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.StartsWith("Espa", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("es", StringComparison.OrdinalIgnoreCase) ||
                 name.StartsWith("es-", StringComparison.OrdinalIgnoreCase));
        }

        internal static bool SpanishIsSelected()
        {
            Assembly game = typeof(Mod).Assembly;
            object active = ReadMember(game.GetType("Verse.LanguageDatabase"), null, "activeLanguage");
            if (active != null)
            {
                foreach (string member in new[] { "folderName", "FriendlyNameEnglish", "FriendlyNameNative" })
                {
                    string name = ReadMember(active.GetType(), active, member) as string;
                    if (!String.IsNullOrEmpty(name)) return IsSpanish(name);
                }
            }
            return IsSpanish(ReadMember(game.GetType("Verse.Prefs"), null, "LangFolderName") as string);
        }

        public static Dictionary<string, string> ReadPayload(string path)
        {
            return ReadCache(path, ExpectedEntries);
        }

        private static Dictionary<string, string> ReadCache(string path, int expected)
        {
            XmlReaderSettings options = new XmlReaderSettings();
            options.DtdProcessing = DtdProcessing.Prohibit;
            options.XmlResolver = null;
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            using (XmlReader reader = XmlReader.Create(path, options)) doc.Load(reader);
            XmlElement cache = doc.DocumentElement;
            if (cache == null || cache.Name != "Cache" || cache.GetAttribute("Language") != "Spanish")
                throw new InvalidDataException("La traduccion debe ser una cache en espanol.");
            Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XmlNode child in cache.ChildNodes)
            {
                XmlElement entry = child as XmlElement;
                if (entry == null) continue;
                if (entry.Name != "Entry" || !entry.HasAttribute("Key"))
                    throw new InvalidDataException("La cache contiene una entrada no reconocida.");
                string key = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(entry.GetAttribute("Key")));
                if (key.IndexOf(':') < 1 || String.IsNullOrEmpty(entry.InnerText) || data.ContainsKey(key))
                    throw new InvalidDataException("La cache contiene una clave vacia o repetida.");
                data.Add(key, entry.InnerText);
            }
            if (data.Count != expected)
                throw new InvalidDataException("Numero de entradas incorrecto; se esperaban " + expected + ".");
            return data;
        }

        private static bool CacheOnlyEnabled()
        {
            string path = Path.Combine(root, "Data", "Config.xml");
            if (!File.Exists(path)) return true;
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            XmlReaderSettings options = new XmlReaderSettings();
            options.DtdProcessing = DtdProcessing.Prohibit;
            options.XmlResolver = null;
            using (XmlReader reader = XmlReader.Create(path, options)) doc.Load(reader);
            XmlNode value = doc.SelectSingleNode("/PackSettings/CacheOnly");
            bool result;
            return value == null || !Boolean.TryParse(value.InnerText, out result) || result;
        }

        private static List<KeyValuePair<string, string>> RulesIn(string text)
        {
            List<KeyValuePair<string, string>> rules = new List<KeyValuePair<string, string>>();
            foreach (string part in RuleBoundary.Split(text))
            {
                int arrow = part.IndexOf("->", StringComparison.Ordinal);
                if (arrow <= 0) continue;
                string head = part.Substring(0, arrow).Trim();
                if (RuleHead.IsMatch(head))
                    rules.Add(new KeyValuePair<string, string>(head, part.Substring(arrow + 2)));
            }
            return rules;
        }

        private static string FormatTranslation(string text, IList<string> placeholders)
        {
            if (placeholders.Count == 0) return text;
            List<string> distinct = new List<string>();
            foreach (string token in placeholders) if (!distinct.Contains(token)) distinct.Add(token);
            List<string> patterns = new List<string>();
            foreach (string token in distinct) patterns.Add(Regex.Escape(token));
            Dictionary<string, int> used = new Dictionary<string, int>(StringComparer.Ordinal);
            string result = Regex.Replace(text, String.Join("|", patterns.ToArray()), delegate(Match match)
            {
                int occurrence;
                used.TryGetValue(match.Value, out occurrence);
                used[match.Value] = occurrence + 1;
                int seen = 0;
                for (int i = 0; i < placeholders.Count; i++)
                {
                    if (placeholders[i] != match.Value) continue;
                    if (seen++ == occurrence) return "{" + i + "}";
                }
                throw new InvalidDataException("Marcador repetido de forma incompatible.");
            });
            foreach (string token in distinct)
            {
                int actual; used.TryGetValue(token, out actual);
                int expected = 0;
                foreach (string value in placeholders) if (value == token) expected++;
                if (actual != expected) throw new InvalidDataException("Marcador de traduccion incompleto.");
            }
            return result;
        }

        private static void AddCaseAlias(Dictionary<string, string> target, string key, string value)
        {
            int colon = key.IndexOf(':');
            string alias = key.Substring(0, colon).ToLowerInvariant() + key.Substring(colon);
            if (!target.ContainsKey(alias)) target.Add(alias, value);
        }

        private static string ExpandLineBreaks(string value)
        {
            return value.Replace("\\r\\n", "\n").Replace("\\n", "\n").Replace("\\r", "\n");
        }

        private static void AddLineBreakAlias(Dictionary<string, string> target, string key, string value)
        {
            int colon = key.IndexOf(':');
            string source = key.Substring(colon + 1);
            if (source.IndexOf("\\n", StringComparison.Ordinal) < 0 && source.IndexOf("\\r", StringComparison.Ordinal) < 0) return;
            string alias = key.Substring(0, colon + 1) + ExpandLineBreaks(source).Trim().Replace("\r\n", "\n").Replace('\r', '\n');
            string translation = ExpandLineBreaks(value);
            if (!target.ContainsKey(alias)) target.Add(alias, translation);
            AddCaseAlias(target, alias, translation);
        }

        public static Dictionary<string, string> BuildCompatiblePayload(Dictionary<string, string> originals, Assembly engine)
        {
            // Exact originals win every collision. Aliases only adapt package ID
            // capitalization or the grammar cache API, never the stored XML.
            Dictionary<string, string> compatible = new Dictionary<string, string>(originals, StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in originals) AddCaseAlias(compatible, pair.Key, pair.Value);
            GrammarAliases = 0;
            Type helpers = engine.GetType("AutoTranslation.Utilities.Helpers") ?? engine.GetType("AutoTranslation.Helpers");
            Type genText = typeof(Mod).Assembly.GetType("Verse.GenText");
            MethodInfo formatter = helpers == null ? null : helpers.GetMethod("ToFormatString", AnyStatic, null, new[] { typeof(string) }, null);
            MethodInfo lineList = genText == null ? null : genText.GetMethod("ToLineList", AnyStatic, null, new[] { typeof(IEnumerable<string>), typeof(string) }, null);
            if (formatter != null && lineList != null)
            {
                ParameterInfo last = lineList.GetParameters()[1];
                object linePrefix = last.IsOptional ? Type.Missing : null;
                foreach (KeyValuePair<string, string> pair in originals)
                {
                    int colon = pair.Key.IndexOf(':');
                    string package = pair.Key.Substring(0, colon + 1);
                    List<KeyValuePair<string, string>> english = RulesIn(pair.Key.Substring(colon + 1));
                    List<KeyValuePair<string, string>> spanish = RulesIn(pair.Value);
                    if (english.Count != spanish.Count) continue;
                    for (int i = 0; i < english.Count; i++)
                    {
                        if (english[i].Key != spanish[i].Key) continue;
                        try
                        {
                            object tuple = formatter.Invoke(null, new object[] { english[i].Value });
                            string formatted = (string)ReadMember(tuple.GetType(), tuple, "Item1");
                            IList<string> tokens = (IList<string>)ReadMember(tuple.GetType(), tuple, "Item2");
                            string tokenList = (string)lineList.Invoke(null, new object[] { tokens, linePrefix });
                            string body = (formatted + english[i].Key + tokenList).Trim().Replace("\r\n", "\n").Replace('\r', '\n');
                            string key = package + body;
                            string translation = FormatTranslation(spanish[i].Value, tokens);
                            if (!compatible.ContainsKey(key)) { compatible.Add(key, translation); GrammarAliases++; }
                            AddCaseAlias(compatible, key, translation);
                        }
                        catch (InvalidDataException) { /* Keep the exact original if a rule is ambiguous. */ }
                        catch (TargetInvocationException) { /* Optional compatibility adapter. */ }
                    }
                }
            }
            // XML language files can store a literal backslash-n while the game
            // exposes a real newline. Keep both spellings available.
            List<KeyValuePair<string, string>> existing = new List<KeyValuePair<string, string>>(compatible);
            foreach (KeyValuePair<string, string> pair in existing) AddLineBreakAlias(compatible, pair.Key, pair.Value);
            CompatibilityAliases = compatible.Count - originals.Count;
            return compatible;
        }

        public static void TryApply(bool finishedLoading)
        {
            if (String.IsNullOrEmpty(root)) return;
            if (applied)
            {
                if (finishedLoading && SpanishIsSelected()) QueueDirectTranslations();
                return;
            }
            try
            {
                if (!SpanishIsSelected())
                {
                    if (finishedLoading) WriteLog("Message", "Selecciona Espanol y reinicia para activar las traducciones.");
                    return;
                }
                Assembly engine = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    if (assembly.GetType("AutoTranslation.Services.TranslatorManager", false) != null) { engine = assembly; break; }
                if (engine == null)
                    throw new InvalidOperationException("Activa Auto Translation antes de este pack. Se necesita su version para RimWorld 1.6 con cache V2.");
                Type translator = engine.GetType("AutoTranslation.Services.TranslatorManager");
                FieldInfo cacheField = translator.GetField("CachedTranslationsV2", AnyStatic);
                IDictionary<string, string> live = cacheField == null ? null : cacheField.GetValue(null) as IDictionary<string, string>;
                Type manager = engine.GetType("AutoTranslation.Services.TranslationCacheManager");
                object instance = ReadMember(manager, null, "Instance");
                IDictionary<string, string> stored = instance == null ? null : ReadMember(manager, instance, "Cache") as IDictionary<string, string>;
                if (live == null || stored == null)
                    throw new InvalidOperationException("Actualiza Auto Translation: este pack necesita la cache V2.");
                Dictionary<string, string> original = ReadPayload(Path.Combine(root, "Data", "translations.xml"));
                Dictionary<string, string> extra = ReadCache(Path.Combine(root, "Data", "Correcciones.xml"), SupplementEntries);
                foreach (KeyValuePair<string, string> pair in extra)
                {
                    if (original.ContainsKey(pair.Key)) throw new InvalidDataException("Una correccion duplica una entrada original.");
                    original.Add(pair.Key, pair.Value);
                }
                Dictionary<string, string> data = BuildCompatiblePayload(original, engine);
                if (CacheOnlyEnabled())
                {
                    Type settings = engine.GetType("AutoTranslation.Settings");
                    FieldInfo automatic = settings == null ? null : settings.GetField("EnableAutoTranslation", AnyStatic);
                    if (automatic != null && automatic.FieldType == typeof(bool)) automatic.SetValue(null, false);
                    FieldInfo ready = translator.GetField("Ready", AnyStatic);
                    if (ready != null && ready.FieldType == typeof(bool)) ready.SetValue(null, false);
                }
                foreach (KeyValuePair<string, string> pair in data)
                {
                    live[pair.Key] = pair.Value;
                    stored[pair.Key] = pair.Value;
                }
                applied = true;
                ImportedEntries = original.Count;
                if (finishedLoading)
                {
                    Type injection = engine.GetType("AutoTranslation.Services.InjectionManager");
                    MethodInfo reapply = injection == null ? null : injection.GetMethod("ReapplyTranslations", AnyStatic);
                    if (reapply != null) reapply.Invoke(null, new object[] { data });
                    Type settings = engine.GetType("AutoTranslation.Settings");
                    MethodInfo reset = settings == null ? null : settings.GetMethod("ResetDefCaches", AnyStatic);
                    if (reset != null) reset.Invoke(null, null);
                }
                WriteLog("Message", "Cargadas " + ImportedEntries + " traducciones locales; " + CompatibilityAliases + " claves de compatibilidad. No se necesita Gist.");
                if (finishedLoading) QueueDirectTranslations();
            }
            catch (Exception error)
            {
                if (!reportedError || finishedLoading)
                {
                    reportedError = true;
                    Exception reason = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                    WriteLog("Error", "No se pudo cargar el pack: " + reason.Message);
                }
            }
        }

        private static void QueueDirectTranslations()
        {
            if (directQueued) return;
            directQueued = true;
            Action action = delegate
            {
                CodeTranslations.TryInstallFromFile(Path.Combine(root, "Data", "CodeTranslations.xml"));
                NotificationCorrections.TryInstall();
                try
                {
                    if (!SpanishIsSelected()) return;
                    DirectTranslations.ApplyFromFile(Path.Combine(root, "Data", "DirectTranslations.xml"));
                    if (DirectTranslations.Applied > 0)
                    {
                        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            Type settings = assembly.GetType("AutoTranslation.Settings");
                            MethodInfo reset = settings == null ? null : settings.GetMethod("ResetDefCaches", AnyStatic);
                            if (reset != null) { reset.Invoke(null, null); break; }
                        }
                    }
                    WriteLog("Message", "Version 1.4: textos aplicados directamente: " + DirectTranslations.Applied + "; conservados: " + DirectTranslations.Preserved + "; rutas ausentes: " + DirectTranslations.Missing + "; errores: " + DirectTranslations.Failed + ".");
                }
                catch (Exception error) { WriteLog("Error", "No se pudieron aplicar las reglas directas: " + error.Message); }
            };
            Type events = typeof(Mod).Assembly.GetType("Verse.LongEventHandler");
            MethodInfo queue = events == null ? null : events.GetMethod("ExecuteWhenFinished", AnyStatic, null, new[] { typeof(Action) }, null);
            if (queue != null)
            {
                try { queue.Invoke(null, new object[] { action }); return; }
                catch { }
            }
            action();
        }

        internal static void WriteLog(string level, string text)
        {
            try
            {
                Type log = typeof(Mod).Assembly.GetType("Verse.Log");
                if (log != null)
                {
                    foreach (MethodInfo method in log.GetMethods(AnyStatic))
                    {
                        ParameterInfo[] args = method.GetParameters();
                        if (method.Name != level || args.Length < 1 || args[0].ParameterType != typeof(string)) continue;
                        object[] values = new object[args.Length]; values[0] = Prefix + text;
                        bool allowed = true;
                        for (int i = 1; i < args.Length; i++)
                        {
                            if (!args[i].IsOptional) { allowed = false; break; }
                            values[i] = Type.Missing;
                        }
                        if (allowed) { method.Invoke(null, values); return; }
                    }
                }
            }
            catch { }
            System.Diagnostics.Trace.WriteLine(Prefix + text);
        }
    }
}
