using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;
namespace InspectedMod
{
    public class Display
    {
        public static int Initializations;
        public static readonly string EarlyLabel;
        static Display() { Initializations++; EarlyLabel = "Save"; }
        public string Label;
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Display() { Label = "Save"; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string[] TechnicalAndUI() { return new[] { "Cancel", "setting_key", "Textures/Cancel", "CancelDef", "PAWN" }; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Format(int cost) { return String.Format("Cost: <color=cyan>{0:N0}</color>", cost); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Send(string faction, object target, object type)
        { Messages.Message("Settlement construction: " + faction + " has begun constructing a new settlement near the edge of its territory.", target, type, false); }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Branch(bool apply) { return apply ? "Apply" : "Cancel"; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string ExceptionBranch(bool fail)
        { try { if (fail) throw new InvalidOperationException(); return "Apply"; } catch (InvalidOperationException) { return "Cancel"; } }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string NativeText() { return "Cancel"; }
        public static string Generic<T>() { return "Save"; }
        public static IEnumerable<string> Options() { yield return "Open folder"; yield return "Textures/OpenFolder"; }
    }
}
