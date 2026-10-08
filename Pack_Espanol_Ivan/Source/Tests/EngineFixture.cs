// Test double for the documented AutoTranslation V2 cache contract.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace AutoTranslation
{
    public static class Settings
    {
        public static bool EnableAutoTranslation = true;
        public static int Resets;
        internal static void ResetDefCaches() { Resets++; }
    }
}
namespace AutoTranslation.Services
{
    public static class TranslatorManager
    {
        public static readonly ConcurrentDictionary<string, string> CachedTranslationsV2 = new ConcurrentDictionary<string, string>();
        public static bool Ready = true;
        public static string Translate(string text, string additional, string package)
        {
            string value;
            string key = package + ":" + (text + additional).Trim().Replace("\r\n", "\n").Replace('\r', '\n');
            return CachedTranslationsV2.TryGetValue(key, out value) ? value : text;
        }
    }
    public class TranslationCacheManager
    {
        private static readonly TranslationCacheManager instance = new TranslationCacheManager();
        public static TranslationCacheManager Instance { get { return instance; } }
        public ConcurrentDictionary<string, string> Cache { get; private set; }
        private TranslationCacheManager() { Cache = new ConcurrentDictionary<string, string>(); }
    }
    public static class InjectionManager
    {
        public static int Reapplications;
        internal static int ReapplyTranslations(IEnumerable<KeyValuePair<string, string>> data)
        {
            Reapplications++;
            return 1;
        }
    }
}
namespace AutoTranslation.Utilities
{
    internal static class Helpers
    {
        public static Tuple<string, List<string>> ToFormatString(string text)
        {
            List<string> placeholders = new List<string>();
            string formatted = Regex.Replace(text, @"[\[{](.*?)[\]}]", delegate(Match match)
            {
                placeholders.Add(match.Value);
                return "{" + (placeholders.Count - 1) + "}";
            });
            return Tuple.Create(formatted, placeholders);
        }
    }
}
