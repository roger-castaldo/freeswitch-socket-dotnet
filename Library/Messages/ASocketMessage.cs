using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Org.Reddragonit.FreeSwitchSockets.Messages
{
    public abstract class ASocketMessage
    {
        private Dictionary<string, string> _parameters;
        public string this[string name]
        {
            get
            {
                if (_parameters.ContainsKey(name))
                    return Uri.UnescapeDataString(_parameters[name]);
                else if (_parameters.ContainsKey("variable_"+name))
                    return Uri.UnescapeDataString(_parameters["variable_"+name]);
                return null;
            }
        }

        public void CopyParameters(ref Dictionary<string, string> parameters)
        {
            foreach (string str in _parameters.Keys)
            {
                if (parameters.ContainsKey(str))
                    parameters.Remove(str);
                parameters.Add(str, Uri.UnescapeDataString(_parameters[str]));
            }
        }

        public Dictionary<string, string>.KeyCollection Keys
        {
            get { return _parameters.Keys; }
        }

        protected ASocketMessage(string message)
        {
            _parameters = ParseProperties(message);
        }

        internal static Dictionary<string, string> ParseProperties(string propertiesText)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();
            foreach (string str in propertiesText.Split('\n'))
            {
                if ((str.Length > 0) && !str.StartsWith("#") && str.Contains(":"))
                {
                    string name = str.Substring(0, str.IndexOf(":"));
                    string value = str.Substring(str.IndexOf(":") + 1);
                    if (value.Contains("#"))
                    {
                        for (int x = 0; x < value.Length; x++)
                        {
                            if (value[x] == '#')
                            {
                                if ((x > 0) && (value[x - 1] != '\\'))
                                {
                                    value = value.Substring(0, x);
                                    break;
                                }
                                else if (x == 0)
                                    value = "";
                            }
                        }
                    }
                    if (value.Length > 0)
                    {
                        if (ret.ContainsKey(name))
                        {
                            ret.Remove(name);
                        }
                        ret.Add(name, value.Trim());
                    }
                }
            }
            return ret;
        }
    }
}
