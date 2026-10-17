using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UniversalConsole
{
    /// <summary>Scene and GameObject commands.</summary>
    internal sealed class SceneBinder : BuiltinBinder
    {
        /// <summary>Creates the binder.</summary>
        public SceneBinder(ConsoleRegistry reg) : base(reg) { }

        /// <summary>Registers scene commands.</summary>
        public override void Bind(IConsole c)
        {
            c.Command("scene", (Func<string>)Scene, "Active scene info", "scene");
            c.Command("scenes", (Func<string>)Scenes, "Scenes in build settings", "scene");
            c.Command("load", (Action<string>)LoadName, "Load scene: load Menu", "scene");
            c.Command("load", (Action<int>)LoadIndex, "Load scene: load 2", "scene");
            c.Command("load_add", (Action<string>)LoadAddName, "Additive load: load_add HUD", "scene");
            c.Command("load_add", (Action<int>)LoadAddIndex, "Additive load: load_add 3", "scene");
            c.Command("unload", (Action<string>)UnloadName, "Unload scene: unload HUD", "scene");
            c.Command("unload", (Action<int>)UnloadIndex, "Unload scene: unload 3", "scene");
            c.Command("active_scene", (Action<string>)ActiveName, "Set active scene", "scene");
            c.Command("active_scene", (Action<int>)ActiveIndex, "Set active scene", "scene");
            c.Command("reload", (Action)Reload, "Reload active scene", "scene");
            c.Command("objects", (Func<string, string>)Objects, "Root objects: objects boss", "scene");
            c.Command("find_type", (Func<string, string>)FindType, "Find components: find_type Light", "scene");
            c.Command("components", (Func<string, string>)Components, "List components: components Player", "scene");
            c.Command("destroy", (Action<string>)Destroy, "Destroy objects: destroy Crate", "scene");
            c.Command("active", (Action<string, bool>)Active, "Set active: active Door false", "scene");
            c.Command("pos", (Func<string, string>)PosGet, "Get position: pos Player", "scene");
            c.Command("pos", (Action<string, Vector3>)PosSet, "Set position: pos Player 0,1,0", "scene");
            c.Command("scale", (Func<string, string>)ScaleGet, "Get scale: scale Boss", "scene");
            c.Command("scale", (Action<string, Vector3>)ScaleSet, "Set scale: scale Boss 2,2,2", "scene");
            c.Command("persist", (Action<string>)Persist, "Keep across loads: persist Music", "scene");
        }

        string Scene()
        {
            Scene s = SceneManager.GetActiveScene();
            return s.name + " [" + s.buildIndex + "] " + s.path + " roots=" + s.GetRootGameObjects().Length;
        }

        string Scenes()
        {
            var lines = new List<string>();
            int n = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < n; i++)
            {
                Scene s = SceneManager.GetSceneByBuildIndex(i);
                lines.Add(i + ": " + s.name + " (" + s.path + ")");
            }
            if (lines.Count == 0) return "(no scenes in build settings)";
            return string.Join("\n", lines.ToArray());
        }

        void LoadName(string name)
        {
            SceneManager.LoadScene(name);
            Print("Loading " + name + ".");
        }

        void LoadIndex(int index)
        {
            SceneManager.LoadScene(index);
            Print("Loading " + index + ".");
        }

        void LoadAddName(string name)
        {
            SceneManager.LoadScene(name, LoadSceneMode.Additive);
            Print("Loading additive " + name + ".");
        }

        void LoadAddIndex(int index)
        {
            SceneManager.LoadScene(index, LoadSceneMode.Additive);
            Print("Loading additive " + index + ".");
        }

        void UnloadName(string name)
        {
            SceneManager.UnloadSceneAsync(name);
            Print("Unloading " + name + ".");
        }

        void UnloadIndex(int index)
        {
            SceneManager.UnloadSceneAsync(index);
            Print("Unloading " + index + ".");
        }

        void ActiveName(string name)
        {
            Scene s = SceneManager.GetSceneByName(name);
            if (!s.IsValid()) { Print(Red("Scene not found: " + name)); return; }
            Print("Active: " + SceneManager.SetActiveScene(s));
        }

        void ActiveIndex(int index)
        {
            Scene s = SceneManager.GetSceneByBuildIndex(index);
            if (!s.IsValid()) { Print(Red("Scene not found: " + index)); return; }
            Print("Active: " + SceneManager.SetActiveScene(s));
        }

        void Reload()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        string Objects(string filter = "")
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            var lines = new List<string>();
            string f = filter ?? "";
            foreach (var go in roots)
            {
                if (go == null) continue;
                if (f.Length > 0 && go.name.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0) continue;
                lines.Add((go.activeInHierarchy ? "+" : "-") + " " + go.name);
                if (lines.Count >= 50) break;
            }
            if (lines.Count == 0) return "(none)";
            string tail = roots.Length > lines.Count ? "\n...and " + (roots.Length - lines.Count) + " more" : "";
            return string.Join("\n", lines.ToArray()) + tail;
        }

        string FindType(string name)
        {
            Type t = FindTypeByName(name);
            if (t == null) return Red("Type not found: " + name);
            var objs = FindObjects(t);
            var lines = new List<string>();
            foreach (var o in objs)
            {
                if (o == null) continue;
                lines.Add(o.name + " (" + o.GetType().Name + ")");
                if (lines.Count >= 30) break;
            }
            if (lines.Count == 0) return "(none)";
            string tail = objs.Length > lines.Count ? "\n...and " + (objs.Length - lines.Count) + " more" : "";
            return "type=" + t.FullName + "\n" + string.Join("\n", lines.ToArray()) + tail;
        }

        string Components(string name)
        {
            var found = FindAll(name);
            if (found.Count == 0) return Red("Object not found: " + name);
            var comps = found[0].GetComponents<Component>();
            var names = new List<string>();
            foreach (var cp in comps)
                if (cp != null) names.Add(cp.GetType().Name);
            if (names.Count == 0) return "(none)";
            string tail = found.Count > 1 ? " [" + found.Count + " matches, first shown]" : "";
            return string.Join("  ", names.ToArray()) + tail;
        }

        void Destroy(string name)
        {
            var found = FindAll(name);
            foreach (var go in found) UnityEngine.Object.Destroy(go);
            Print(found.Count == 0 ? Red("Object not found: " + name) : "Destroyed " + found.Count + ".");
        }

        void Active(string name, bool on)
        {
            var found = FindAll(name);
            if (found.Count == 0 && on)
            {
#if UNITY_2023_1_OR_NEWER
                var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
#else
                var all = UnityEngine.Object.FindObjectsOfType<GameObject>();
#endif
                foreach (var go in all)
                {
                    if (go == null || go.activeInHierarchy) continue;
                    if (string.Equals(go.name, name, StringComparison.OrdinalIgnoreCase)) { found.Add(go); break; }
                }
            }
            foreach (var go in found) go.SetActive(on);
            Print(found.Count == 0 ? Red("Object not found: " + name) : "Set " + found.Count + " active=" + on + ".");
        }

        string PosGet(string name)
        {
            var found = FindAll(name);
            if (found.Count == 0) return Red("Object not found: " + name);
            return found[0].transform.position.ToString();
        }

        void PosSet(string name, Vector3 p)
        {
            var found = FindAll(name);
            if (found.Count == 0) { Print(Red("Object not found: " + name)); return; }
            found[0].transform.position = p;
            Print(name + " pos=" + p + ".");
        }

        string ScaleGet(string name)
        {
            var found = FindAll(name);
            if (found.Count == 0) return Red("Object not found: " + name);
            return found[0].transform.localScale.ToString();
        }

        void ScaleSet(string name, Vector3 s)
        {
            var found = FindAll(name);
            if (found.Count == 0) { Print(Red("Object not found: " + name)); return; }
            found[0].transform.localScale = s;
            Print(name + " scale=" + s + ".");
        }

        void Persist(string name)
        {
            var found = FindAll(name);
            if (found.Count == 0) { Print(Red("Object not found: " + name)); return; }
            UnityEngine.Object.DontDestroyOnLoad(found[0]);
            Print(name + " persists.");
        }
    }
}
