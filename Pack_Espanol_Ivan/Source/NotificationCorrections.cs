using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace PackEspanolIvan
{
    // The XML sources do not contain this complete notification. Match its
    // observed template at the message boundary, keeping the faction verbatim.
    // No game content, message type, targets or history flags are changed.
    public static class NotificationCorrections
    {
        private const string EnglishHeading = "Settlement construction: ";
        private const string SpanishHeading = "Construcción de asentamiento: ";
        private const string EnglishTail = " has begun constructing a new settlement near the edge of its territory.";
        private const string SpanishTail = " ha comenzado a construir un nuevo asentamiento cerca del límite de su territorio.";
        private const string HarmonyId = "ivan.rimworld.traducciones.es.notifications";
        private static readonly HashSet<MethodBase> Patched = new HashSet<MethodBase>();
        private static readonly BindingFlags StaticMethods = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        public static int PatchedMethods { get { return Patched.Count; } }
        public static int FailedMethods { get; private set; }

        public static string Translate(string original)
        {
            if (String.IsNullOrEmpty(original) || !original.EndsWith(EnglishTail, StringComparison.Ordinal)) return original;
            // A translated definition label can already be used by the mod in
            // the heading. Accept both spellings, but require the full body.
            string[] headings = { EnglishHeading, SpanishHeading, "construcción de asentamiento: " };
            foreach (string heading in headings)
            {
                if (!original.StartsWith(heading, StringComparison.Ordinal)) continue;
                int length = original.Length - heading.Length - EnglishTail.Length;
                if (length <= 0) return original;
                string faction = original.Substring(heading.Length, length);
                if (faction.IndexOf('\n') >= 0 || faction.IndexOf('\r') >= 0 || String.IsNullOrWhiteSpace(faction)) return original;
                return SpanishHeading + faction + SpanishTail;
            }
            return original;
        }

        private static bool IsDisplayText(Type type)
        {
            return type == typeof(string) || type.FullName == "Verse.TaggedString";
        }

        private static object FromString(Type type, string text)
        {
            if (type == typeof(string)) return text;
            ConstructorInfo constructor = type.GetConstructor(new[] { typeof(string) });
            if (constructor != null) return constructor.Invoke(new object[] { text });
            foreach (MethodInfo method in type.GetMethods(StaticMethods))
            {
                ParameterInfo[] args = method.GetParameters();
                if (method.Name == "op_Implicit" && method.ReturnType == type && args.Length == 1 && args[0].ParameterType == typeof(string))
                    return method.Invoke(null, new object[] { text });
            }
            return null;
        }

        // Harmony's documented __args contract propagates replacements back
        // into the original arguments, including boxed TaggedString structs.
        public static void Prefix(MethodBase __originalMethod, object[] __args)
        {
            try
            {
                if (!Bootstrap.SpanishIsSelected() || __originalMethod == null || __args == null || __args.Length == 0 || __args[0] == null) return;
                ParameterInfo[] parameters = __originalMethod.GetParameters();
                if (parameters.Length == 0 || !IsDisplayText(parameters[0].ParameterType)) return;
                string original = __args[0].ToString();
                string translated = Translate(original);
                if (translated == original) return;
                object value = FromString(parameters[0].ParameterType, translated);
                if (value != null) __args[0] = value;
            }
            catch { /* A notification must still be delivered if an adapter fails. */ }
        }

        public static void TryInstall()
        {
            if (Patched.Count > 0) return;
            try
            {
                if (!Bootstrap.SpanishIsSelected()) return;
                Type harmony = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    harmony = assembly.GetType("HarmonyLib.Harmony", false);
                    if (harmony != null) break;
                }
                if (harmony == null) throw new InvalidOperationException("No se encontró Harmony, necesario para corregir el aviso de construcción.");
                Type descriptor = harmony.Assembly.GetType("HarmonyLib.HarmonyMethod", true);
                object owner = Activator.CreateInstance(harmony, new object[] { HarmonyId });
                object prefix = Activator.CreateInstance(descriptor, new object[] { typeof(NotificationCorrections).GetMethod("Prefix") });
                MethodInfo patch = null;
                foreach (MethodInfo candidate in harmony.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    ParameterInfo[] parameters = candidate.GetParameters();
                    if (candidate.Name == "Patch" && parameters.Length >= 2 && parameters[0].ParameterType == typeof(MethodBase) && parameters[1].ParameterType == descriptor)
                    { patch = candidate; break; }
                }
                if (patch == null) throw new MissingMethodException("La versión de Harmony no expone la API de parches esperada.");
                Type messages = typeof(Mod).Assembly.GetType("Verse.Messages", true);
                foreach (MethodInfo method in messages.GetMethods(StaticMethods))
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (method.Name != "Message" || parameters.Length == 0 || !IsDisplayText(parameters[0].ParameterType)) continue;
                    try
                    {
                        ParameterInfo[] inputs = patch.GetParameters();
                        object[] values = new object[inputs.Length];
                        values[0] = method; values[1] = prefix;
                        for (int i = 2; i < values.Length; i++) values[i] = inputs[i].IsOptional ? Type.Missing : null;
                        patch.Invoke(owner, values);
                        Patched.Add(method);
                    }
                    catch (Exception error)
                    {
                        FailedMethods++;
                        Exception reason = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                        Bootstrap.WriteLog("Warning", "No se pudo corregir una variante del aviso: " + reason.Message);
                    }
                }
                if (Patched.Count == 0) throw new MissingMethodException("No se pudo conectar la corrección con los mensajes del juego.");
                Bootstrap.WriteLog("Message", "Version 1.4: corrección del aviso de construcción activa; " + Patched.Count + " variantes de mensajes, " + FailedMethods + " errores.");
            }
            catch (Exception error)
            {
                Exception reason = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                Bootstrap.WriteLog("Warning", "Las traducciones locales siguen activas, pero no se pudo activar la corrección del aviso: " + reason.Message);
            }
        }
    }
}
