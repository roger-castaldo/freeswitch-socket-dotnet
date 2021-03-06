﻿using Org.Reddragonit.FreeSwitchSockets;
using Org.Reddragonit.FreeSwitchSockets.Inbound;
using Org.Reddragonit.FreeSwitchSockets.Messages;
using Org.Reddragonit.FreeSwitchSockets.Outbound;
using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;

namespace SocketTester
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting up listener socket to test PIN obtainment...");
            InboundListener list = new InboundListener(IPAddress.Any, 8084,
                new InboundListener.delProcessConnection(ProcessConnection));
            list.DisposeInvalidMessage = new ASocket.delDisposeInvalidMessage(DisposeInvalidMessage);
            OutboundSocket os = new OutboundSocket(IPAddress.Loopback, 8021, "ClueCon", new ASocket.delProcessEventMessage(ProcessEvent),
                null, null, null);
            os.DisposeInvalidMessage += new ASocket.delDisposeInvalidMessage(DisposeInvalidMessage);
            os.RegisterEvent("all");
            //os.RegisterEventFilter("Event-Name", "CUSTOM");
            //os.RegisterEventFilter("Event-Subclass", "conference::maintenance");
            Console.WriteLine("Issuing command to show registrations");
            string resp;
            os.IssueCommand("show registrations as xml", out resp);
            Console.WriteLine(resp);
            Console.ReadLine();
            try
            {
                os.Close();
            }catch(Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        public static void DisposeInvalidMessage(string message){
            Console.WriteLine("Disposing invalid message...");
            Console.WriteLine(message);
        }

        public static void ProcessEvent(SocketEvent evnt){
            Console.WriteLine("Event recieved of type {0} @ {1}",new object[]{
                evnt.EventName,
                DateTime.Now
            });
            if (evnt.EventName.EndsWith("conference::maintenance")) {
                StringBuilder sb = new StringBuilder();
                foreach (string str in evnt.Keys)
                {
                    switch (str)
                    {
                        case "Action":
                        case "Conference-Name":
                        case "Conference-Size":
                        case "Conference-Ghosts":
                        case "Conference-Profile-Name":
                        case "Conference-Unique-ID":
                        case "Hear":
                        case "Speak":
                        case "Talking":
                            sb.AppendFormat("{0}:{1},", str,evnt[str]);
                            break;
                    }
                    
                }
                Console.WriteLine(string.Format("\tKEYS:{0}", sb.ToString()));
            }else if (evnt.EventName == "HEARTBEAT")
            {
                Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                long totalBytesOfMemoryUsed = currentProcess.WorkingSet64;
                decimal obytes = (decimal)totalBytesOfMemoryUsed;
                if (obytes > 1024 * 1024 * 1024)
                    Console.WriteLine("Current Memory: {0} GB", Math.Round(obytes / (1024 * 1024 * 1024),2));
                else if(obytes > 1024 * 1024)
                    Console.WriteLine("Current Memory: {0} MB", Math.Round(obytes / (1024 * 1024),2));
                else if (obytes > 1024)
                    Console.WriteLine("Current Memory: {0} KB", Math.Round(obytes / 1024,2));
                else
                    Console.WriteLine("Current Memory: {0} B", Math.Round(obytes,2));
            }
        }

        public static void ProcessConnection(InboundConnection conn)
        {
            conn.BindDigitAction("", "##", "", null, null, null);
            SocketEvent ev;
            conn.Answer();
            string[] keys = new string[conn.Keys.Count];
            conn.Keys.CopyTo(keys, 0);
            foreach (string str in keys)
                System.Diagnostics.Debug.WriteLine(str + " --> " + (conn[str]==null ? "" : conn[str]));
            string pin = conn.PlayAndGetDigits(4, 10, 3, 3000, "#","sounds/en/us/callie/conference/8000/conf-pin.wav" ,null, "\\d+", null);
            Console.WriteLine("The pin entered was: " + (pin == null ? "NO PIN" : pin));
            if (pin == "8888")
            {
                conn.PlayAudioFile("sounds/music/48000/ponce-preludio-in-e-major.wav", false);
                Thread.Sleep(10000);
                if (conn.IsExtensionLive(new sDomainExtensionPair("1001", conn.Domain)))
                {
                    ev = conn.BridgeToExtension(new sDomainExtensionPair("1001", conn.Domain), true);
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
                            ev = conn.Voicemail(conn.Context, new sDomainExtensionPair(conn.Domain, "1001"));
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
                    ev = conn.Voicemail(conn.Context, new sDomainExtensionPair(conn.Domain, "1001"));
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
                conn.PlayAudioFile("sounds/en/us/callie/conference/8000/conf-bad-pin.wav", true);
                conn.PlayAudioFile("sounds/en/us/callie/conference/8000/conf-goodbye.wav", true);
            }
            if (!conn.IsHungUp)
                conn.Hangup();
        }
    }
}
