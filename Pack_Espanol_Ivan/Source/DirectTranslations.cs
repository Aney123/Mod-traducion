using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using Verse;

namespace PackEspanolIvan
{
    // Only known display fields at exact, verified Def paths are changed.
    // Existing native Spanish, renamed texts and technical identifiers survive.
    public static class DirectTranslations
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly Regex PathSyntax = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*(?:\.(?:[A-Za-z_][A-Za-z0-9_]*|[0-9]+))*$", RegexOptions.CultureInvariant);
        private static readonly Regex TypeSyntax = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*$", RegexOptions.CultureInvariant);
        public static int Applied { get; private set; }
        public static int Missing { get; private set; }
        public static int Preserved { get; private set; }
        public static int Failed { get; private set; }

        private sealed class Rule
        {
            public string Type, Def, Path, Original, Raw, Translation, ItemType;
        }
        private static string Normalize(string value)
        {
            return (value ?? String.Empty).Trim().Replace("\r\n", "\n").Replace('\r', '\n');
        }
        private static string Expand(string value)
        {
            return value.Replace("\\r\\n", "\n").Replace("\\n", "\n").Replace("\\r", "\n");
        }
        private static bool Matches(string current, string expected)
        {
            return Normalize(current) == Normalize(expected) || Normalize(current) == Normalize(Expand(expected));
        }
        private static object Read(object value, string name)
        {
            if (value == null) return null;
            Type type = value.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name, Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(value);
                PropertyInfo property = type.GetProperty(name, Instance | BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(value, null);
                type = type.BaseType;
            }
            return null;
        }
        private static List<Rule> ReadRules(string path)
        {
            XmlReaderSettings options = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            XmlDocument doc = new XmlDocument { XmlResolver = null };
            using (XmlReader reader = XmlReader.Create(path, options)) doc.Load(reader);
            if (doc.DocumentElement == null || doc.DocumentElement.Name != "DirectTranslations") throw new InvalidDataException("Formato incorrecto de reglas directas.");
            List<Rule> rules = new List<Rule>();
            foreach (XmlNode node in doc.DocumentElement.ChildNodes)
            {
                XmlElement e = node as XmlElement;
                if (e == null) continue;
                string field = e.GetAttribute("Path");
                if (e.Name != "Rule" || !PathSyntax.IsMatch(field)) throw new InvalidDataException("Ruta de traduccion no valida.");
                string itemType = e.GetAttribute("ItemType");
                if (!String.IsNullOrEmpty(itemType))
                {
                    int numericParts = 0, value; string[] parts = field.Split('.');
                    foreach (string part in parts) if (Int32.TryParse(part, out value)) numericParts++;
                    if (!TypeSyntax.IsMatch(itemType) || numericParts != 1 || Int32.TryParse(parts[parts.Length - 1], out value))
                        throw new InvalidDataException("Selector de extensión no válido.");
                }
                foreach (string part in field.Split('.'))
                    if (part == "defName" || part == "thingClass" || part == "workerClass" || part == "texPath" || part == "iconPath")
                        throw new InvalidDataException("Una regla intenta cambiar un identificador tecnico.");
                XmlNode original = e.SelectSingleNode("Original"), raw = e.SelectSingleNode("Raw"), translation = e.SelectSingleNode("Translated");
                if (String.IsNullOrEmpty(e.GetAttribute("Type")) || String.IsNullOrEmpty(e.GetAttribute("Def")) || original == null || raw == null || translation == null || String.IsNullOrEmpty(translation.InnerText))
                    throw new InvalidDataException("Regla de traduccion incompleta.");
                rules.Add(new Rule { Type = e.GetAttribute("Type"), Def = e.GetAttribute("Def"), Path = field, Original = original.InnerText, Raw = raw.InnerText, Translation = translation.InnerText, ItemType = itemType });
            }
            return rules;
        }
        private static bool ApplyPath(object def, Rule rule)
        {
            object parent = def;
            string[] parts = rule.Path.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                bool last = i == parts.Length - 1;
                int index;
                if (Int32.TryParse(parts[i], out index))
                {
                    IList list = parent as IList;
                    if (list != null && !String.IsNullOrEmpty(rule.ItemType))
                    {
                        // AddModExtension appends to a list whose prior length
                        // depends on other mods. Match the exact extension class.
                        object selected = null;
                        foreach (object item in list)
                            if (item != null && item.GetType().FullName == rule.ItemType) { selected = item; break; }
                        if (selected == null) return false;
                        parent = selected; continue;
                    }
                    if (list == null || index < 0 || index >= list.Count) return false;
                    if (!last) { parent = list[index]; continue; }
                    string current = list[index] as string;
                    if (current == null || list.IsReadOnly) return false;
                    if (!Matches(current, rule.Original) && !Matches(current, rule.Raw)) { Preserved++; return true; }
                    list[index] = Expand(rule.Translation); Applied++; return true;
                }
                if (parent == null) return false;
                if (!last) { parent = Read(parent, parts[i]); continue; }
                Type type = parent.GetType();
                FieldInfo field = null;
                while (type != null && field == null)
                {
                    field = type.GetField(parts[i], Instance | BindingFlags.DeclaredOnly); type = type.BaseType;
                }
                if (field == null || field.FieldType != typeof(string) || field.IsInitOnly) return false;
                string value = field.GetValue(parent) as string;
                if (value == null) return false;
                if (!Matches(value, rule.Original) && !Matches(value, rule.Raw)) { Preserved++; return true; }
                field.SetValue(parent, Expand(rule.Translation)); Applied++; return true;
            }
            return false;
        }
        public static void ApplyFromFile(string path)
        {
            Applied = Missing = Preserved = Failed = 0;
            if (!File.Exists(path)) return;
            // Validate the entire manifest before changing a single Def.
            List<Rule> rules = ReadRules(path);
            Assembly game = typeof(Mod).Assembly;
            Type defBase = game.GetType("Verse.Def"), database = game.GetType("Verse.GenDefDatabase");
            MethodInfo getAll = database == null ? null : database.GetMethod("GetAllDefsInDatabaseForDef", Static, null, new[] { typeof(Type) }, null);
            if (defBase == null || getAll == null) throw new InvalidOperationException("No se encuentra la base de definiciones de RimWorld.");
            Dictionary<string, Type> names = new Dictionary<string, Type>(StringComparer.Ordinal);
            HashSet<string> ambiguous = new HashSet<string>(StringComparer.Ordinal);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException error) { types = error.Types; }
                catch { continue; }
                foreach (Type type in types)
                {
                    // An unrelated assembly can expose a type whose parent or
                    // dependency is unavailable. Keep scanning the valid types.
                    try
                    {
                        if (type == null || !defBase.IsAssignableFrom(type) || type.IsAbstract) continue;
                        names[type.FullName] = type;
                        Type prior;
                        if (names.TryGetValue(type.Name, out prior) && prior != type) ambiguous.Add(type.Name);
                        else names[type.Name] = type;
                    }
                    catch { continue; }
                }
            }
            Dictionary<Type, Dictionary<string, object>> tables = new Dictionary<Type, Dictionary<string, object>>();
            foreach (Rule rule in rules)
            {
                try
                {
                    Type type;
                    if (ambiguous.Contains(rule.Type) || !names.TryGetValue(rule.Type, out type)) { Missing++; continue; }
                    Dictionary<string, object> table;
                    if (!tables.TryGetValue(type, out table))
                    {
                        table = new Dictionary<string, object>(StringComparer.Ordinal);
                        IEnumerable defs = getAll.Invoke(null, new object[] { type }) as IEnumerable;
                        if (defs != null)
                            foreach (object def in defs)
                            {
                                string name = Read(def, "defName") as string;
                                if (!String.IsNullOrEmpty(name)) table[name] = def;
                            }
                        tables[type] = table;
                    }
                    object target;
                    if (!table.TryGetValue(rule.Def, out target) || !ApplyPath(target, rule)) Missing++;
                }
                catch { Failed++; }
            }
        }
    }
}
