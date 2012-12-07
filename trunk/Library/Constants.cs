using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace Org.Reddragonit.FreeSwitchSockets
{
    internal class Constants
    {
        public static readonly Version _OVERRIDE_VERSION = new Version("2.10");
        private static Version _monoVersion = null;
        public static Version MonoVersion
        {
            get { return _monoVersion; }
        }

        static Constants()
        {
            Type type = Type.GetType("Mono.Runtime", false);
            if (type != null)
            {
                MethodInfo mi = type.GetMethod("GetDisplayName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                string str = mi.Invoke(null, new object[] { }).ToString();
                _monoVersion = new Version(str.Substring(0, str.IndexOf(" ")));
            }
        }
    }
}
