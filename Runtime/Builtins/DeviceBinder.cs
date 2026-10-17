using System;
using UnityEngine;

namespace UniversalConsole
{
    /// <summary>Device and system info commands.</summary>
    internal sealed class DeviceBinder : BuiltinBinder
    {
        /// <summary>Creates the binder.</summary>
        public DeviceBinder(ConsoleRegistry reg) : base(reg) { }

        /// <summary>Registers device commands.</summary>
        public override void Bind(IConsole c)
        {
            c.Command("sysinfo", (Func<string>)SysInfo, "Device, OS, CPU, GPU", "device");
            c.Command("net", (Func<string>)Net, "Network reachability", "device");
            c.Command("battery", (Func<string>)Battery, "Battery level and status", "device");
        }

        string SysInfo()
        {
            return "device=" + SystemInfo.deviceModel + " os=" + SystemInfo.operatingSystem +
                " ram=" + SystemInfo.systemMemorySize + "MB cpu=" + SystemInfo.processorType +
                " cores=" + SystemInfo.processorCount + " gpu=" + SystemInfo.graphicsDeviceName +
                " api=" + SystemInfo.graphicsDeviceType;
        }

        string Net()
        {
            return "reach=" + Application.internetReachability + " mobile=" + Application.isMobilePlatform;
        }

        string Battery()
        {
            float level = SystemInfo.batteryLevel;
            string pct = level < 0f ? "n/a" : (level * 100f).ToString("F0") + "%";
            return "level=" + pct + " status=" + SystemInfo.batteryStatus;
        }
    }
}
