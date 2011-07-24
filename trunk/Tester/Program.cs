using System;
using System.Collections.Generic;
using System.Text;
using Org.Reddragonit.FreeSwitchSockets.Inbound;
using System.Net;
using System.Threading;
using Org.Reddragonit.FreeSwitchSockets.Messages;

namespace SocketTester
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting up listener socket to test PIN obtainment...");
            InboundListener list = new InboundListener(IPAddress.Any, 8084,
                new InboundListener.delProcessConnection(ProcessConnection));
            Console.WriteLine("Hit Enter to exit...");
            Console.ReadLine();
        }

        public static void ProcessConnection(InboundConnection conn)
        {
            SocketEvent ev;
            conn.Answer();
            string[] keys = new string[conn.Keys.Count];
            conn.Keys.CopyTo(keys, 0);
            foreach (string str in keys)
                System.Diagnostics.Debug.WriteLine(str + " --> " + (conn[str]==null ? "" : conn[str]));
            string pin = conn.PlayAndGetDigits(4, 10, 3, 3000, "#","/opt/freeswitch/sounds/en/us/callie/conference/8000/conf-pin.wav" ,null, "\\d+", null);
            Console.WriteLine("The pin entered was: " + (pin == null ? "NO PIN" : pin));
            if (pin == "8888")
            {
                conn.PlayAudioFile("/opt/freeswitch/sounds/music/48000/ponce-preludio-in-e-major.wav", false);
                Thread.Sleep(10000);
                if (conn.IsExtensionLive("1001", conn.Domain))
                {
                    ev = conn.BridgeToExtension("1001", conn.Domain, true);
                    if (ev == null)
                        Console.WriteLine("Null event returned from bridge");
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Bridge result: ");
                        foreach (string str in ev.Keys)
                        {
                            System.Diagnostics.Debug.WriteLine(str + " --> " + ev[str]);
                        }
                        if (ev["originate_disposition"] == "USER_NOT_REGISTERED")
                        {
                            Console.WriteLine("Bridging to voicemail for unregistered user.");
                            ev = conn.Voicemail(conn.Context, conn.Domain, "1001");
                            if (ev == null)
                                Console.WriteLine("Null event returned from voicemail.");
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("Voicemail result: ");
                                foreach (string str in ev.Keys)
                                {
                                    System.Diagnostics.Debug.WriteLine(str + " --> " + ev[str]);
                                }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Extension 1001 is not connected, bridging to voicemail");
                    ev = conn.Voicemail(conn.Context, conn.Domain, "1001");
                    if (ev == null)
                        Console.WriteLine("Null event returned from voicemail.");
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Voicemail result: ");
                        foreach (string str in ev.Keys)
                        {
                            System.Diagnostics.Debug.WriteLine(str + " --> " + ev[str]);
                        }
                    }
                }
            }
            else
            {
                conn.PlayAudioFile("/opt/freeswitch/sounds/en/us/callie/conference/8000/conf-bad-pin.wav", true);
                conn.PlayAudioFile("/opt/freeswitch/sounds/en/us/callie/conference/8000/conf-goodbye.wav", true);
            }
            if (!conn.IsHungUp)
                conn.Hangup();
        }
    }
}
