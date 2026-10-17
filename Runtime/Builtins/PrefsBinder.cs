using System;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>PlayerPrefs commands.</summary>
    internal sealed class PrefsBinder : BuiltinBinder
    {
        /// <summary>Creates the binder.</summary>
        public PrefsBinder(ConsoleRegistry reg) : base(reg) { }

        /// <summary>Registers prefs commands.</summary>
        public override void Bind(IConsole c)
        {
            c.Command("pref_get", (Func<string, string>)Get, "Read string: pref_get coins", "prefs");
            c.Command("pref_get_int", (Func<string, string>)GetInt, "Read int: pref_get_int coins", "prefs");
            c.Command("pref_get_float", (Func<string, string>)GetFloat, "Read float: pref_get_float speed", "prefs");
            c.Command("pref_set", (Action<string, string>)Set, "Write string: pref_set hero Bob", "prefs");
            c.Command("pref_set_int", (Action<string, int>)SetInt, "Write int: pref_set_int coins 5", "prefs");
            c.Command("pref_set_float", (Action<string, float>)SetFloat, "Write float: pref_set_float speed 1.5", "prefs");
            c.Command("pref_del", (Action<string>)Del, "Delete key: pref_del coins", "prefs");
        }

        string Get(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return Red("No such key: " + key);
            return PlayerPrefs.GetString(key);
        }

        string GetInt(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return Red("No such key: " + key);
            return PlayerPrefs.GetInt(key).ToString();
        }

        string GetFloat(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return Red("No such key: " + key);
            return PlayerPrefs.GetFloat(key).ToString();
        }

        void Set(string key, string value)
        {
            PlayerPrefs.SetString(key, value ?? "");
            PlayerPrefs.Save();
            Print(key + " saved.");
        }

        void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            Print(key + " saved.");
        }

        void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            Print(key + " saved.");
        }

        void Del(string key)
        {
            if (!PlayerPrefs.HasKey(key)) { Print(Red("No such key: " + key)); return; }
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Print(key + " deleted.");
        }
    }
}
