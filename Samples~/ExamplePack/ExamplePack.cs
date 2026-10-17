using System;
using UniversalConsole;
using UnityEngine;

public static class GameCheats
{
    public static int FpsMax = 60;

    public static void Spawn(string prefabName, int count = 1)
    {
        Debug.Log("Spawn " + count + "x " + prefabName);
    }

    public static void KillAll()
    {
        Debug.Log("Killed all");
    }

    public static string Status()
    {
        return "fps_max=" + FpsMax;
    }
}

public sealed class ExamplePack : IConsolePack
{
    public void Build(IConsole c)
    {
        c.Command("spawn", (Action<string, int>)GameCheats.Spawn, "Spawn: spawn \"big orc\" 5", "Cheats");
        c.Command("killall", (Action)GameCheats.KillAll, "Kill all", "Cheats");
        c.Command("status", (Func<string>)GameCheats.Status, "Status", "Debug");
        c.Var("fps_max", () => GameCheats.FpsMax, v => GameCheats.FpsMax = v, "FPS limit");
    }
}
