using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using Org.Reddragonit.FreeSwitchSockets.Messages;
using System.Text.RegularExpressions;
using Org.Reddragonit.FreeSwitch.Sockets;

namespace Org.Reddragonit.FreeSwitchSockets.Inbound
{
    public class InboundConnection : ASocket
    {

        private const string REGISTRATIONS_FOR_PROFILE_CHECK_COMMAND = "sofia status profile {0} reg";
        private const string REGEX_EXTENSION_CHECK = "^MWI-Account:\\s+{0}@{1}\\s*$";

        private const string RANDOM_VAR_NAME_CHARS = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_";
        private const string EXECUTE_COMPLETE_EVENT_NAME = "CHANNEL_EXECUTE_COMPLETE";
        private const string CHANNEL_END_EVENT_NAME = "CHANNEL_HANGUP_COMPLETE";
        private const string APP_RESPONSE_VARIABLE_NAME = "Application-Response";
        private const string APP_NAME_VARIABLE_NAME = "Application";
        private const string APP_DATA_VARIABLE_NAME = "Application-Data";

        private Dictionary<string, string> _properties;
        private ManualResetEvent _awaitingCommandEvent = new ManualResetEvent(false);
        private SocketEvent _currentEvent;
        private string _awaitingCommand = "";
        private Queue<ManualResetEvent> _awaitingCommands;

        private DateTime _startTime;
        public DateTime StartTime
        {
            get { return _startTime; }
        }

        private bool _isHungup;
        public bool IsHungUp
        {
            get { return _isHungup; }
        }

        public InboundConnection(TcpClient client)
            : base(client.Client)
        {
            //bug fix for missing context variable, using Caller-Context as precation when variable is missing.
            if ((this["Caller-Context"] != null) && (this.Context == null))
                this.Context = this["Caller-Context"];
            //bug fix for missing domain variable
            if ((this["Channel-Name"] != null) && (this.Domain == null))
            {
                string dom = this["Channel-Name"];
                if (dom.Contains("@"))
                    dom = dom.Substring(dom.IndexOf("@") + 1);
                if (dom.Contains(":"))
                    dom = dom.Substring(0, dom.IndexOf(":"));
                this.Domain = dom;
            }
        }

        private string RandomVariable
        {
            get
            {
                string ret = "";
                while (ret.Length < 10)
                {
                    ret += RANDOM_VAR_NAME_CHARS[Random.Next(0, RANDOM_VAR_NAME_CHARS.Length - 1)].ToString();
                }
                return ret;
            }
        }

        #region Operations
        #region BasicOps
        public void RingReady()
        {
            ExecuteApplication("ring_ready", false);
        }

        public void Answer()
        {
            ExecuteApplication("answer", false);
        }

        public void PreAnswer()
        {
            ExecuteApplication("pre_answer", false);
        }

        public void Sleep(int milliSeconds)
        {
            ExecuteApplication("sleep", "data=" + milliSeconds.ToString(), false);
            Thread.Sleep(milliSeconds);
        }

        public void Hangup()
        {
            ExecuteApplication("hangup", true);
        }

        public bool IsExtensionLive(sDomainExtensionPair extension)
        {
            string apiRes = _IssueAPICommand(string.Format(REGISTRATIONS_FOR_PROFILE_CHECK_COMMAND, extension.Domain), true);
            return new Regex(string.Format(REGEX_EXTENSION_CHECK, extension.Extension, extension.Domain), RegexOptions.Compiled | RegexOptions.ECMAScript).Matches(extension.Extension).Count > 0;
        }

        public void AttendedTransfer(sDomainExtensionPair extension, bool eventLock)
        {
            ExecuteApplication("att_xfer", "user/" + extension.Extension + "@" + extension.Domain, eventLock);
        }

        public void Break(bool all)
        {
            ExecuteApplication("break", (all ? "all" : ""), false);
        }

        public SocketEvent CheckAcl(string address, string acl, string hangupcause)
        {
            return ExecuteApplication("check_acl", address + " " + acl + (hangupcause == null ? "" : "  " + hangupcause), true);
        }

        public SocketEvent DB(string command, bool eventLock)
        {
            return ExecuteApplication("db", command, eventLock);
        }

        public SocketEvent Deflect(sDomainExtensionPair extension, bool eventLock)
        {
            return ExecuteApplication("deflect", "sip:" + extension.Extension + "@" + extension.Domain, eventLock);
        }

        public void Echo()
        {
            ExecuteApplication("echo", false);
        }

        public string Enum(string number, string searchDomain)
        {
            SocketEvent se = ExecuteApplication("enum", number + " " + searchDomain, true);
            return se["enum_auto_route"];
        }

        public SocketEvent Eval(string command, bool eventLock)
        {
            return ExecuteApplication("eval", command, eventLock);
        }

        public void Event(string eventName, string eventSubClass, Dictionary<string, string> header)
        {
            string ihead = "";
            if (header != null)
            {
                foreach (string str in header.Keys)
                    ihead += str + "=" + header[str] + ",";
                if (ihead.Length > 0)
                    ihead = ihead.Substring(0, ihead.Length - 1);
            }
            if (eventSubClass != null)
                ihead = "Event-Subclass=" + eventSubClass + (ihead.Length == 0 ? "" : ",") + ihead;
            if (eventName != null)
                ihead = eventName + (ihead.Length == 0 ? "" : ",") + ihead;
            ExecuteApplication("event", ihead, false);
        }

        public void ExecuteExtension(string extensionName, string context, bool eventLock)
        {
            ExecuteApplication("execute_extension", (extensionName == null ? "" : extensionName) + " " + (context == null ? "" : context), eventLock);
        }

        public void Export(string variableName, string value, bool eventLock)
        {
            ExecuteApplication("export", variableName + "=" + value, eventLock);
        }

        public SocketEvent Info()
        {
            return ExecuteApplication("info", true);
        }

        public SocketEvent MkDir(string path)
        {
            return ExecuteApplication("mkdir", path, true);
        }

        public void Presence(bool set, sDomainExtensionPair extension, string presenceName, string message, bool eventLock)
        {
            ExecuteApplication("presence", (set ? "in" : "out") + " " + extension.Extension + "@" + extension.Domain + " " + (presenceName == null ? "unknown" : presenceName) + (message == null ? "" : " " + message), eventLock);
        }

        public enum CallPrivacyTypes
        {
            No,
            Yes,
            Name,
            Full,
            Number
        }

        public void Privacy(CallPrivacyTypes type, bool eventLock)
        {
            ExecuteApplication("privacy", type.ToString().ToLower(), eventLock);
        }

        public string ReadDigits(int min, int max, string soundFile, string variableName, int timeoutMS, string terminators)
        {
            SocketEvent ev = ExecuteApplication("read", min.ToString() + " " + max.ToString() + " " + soundFile + " " + variableName + " " + timeoutMS.ToString() + " " + terminators, true);
            return ev[variableName];
        }

        public void Redirect(sDomainExtensionPair[] extensions, bool eventLock)
        {
            string spars = "";
            foreach (sDomainExtensionPair dep in extensions)
                spars += ",sip:" + dep.Extension + "@" + dep.Domain;
            ExecuteApplication("redirect", spars.Substring(1), eventLock);
        }

        public SocketEvent Respond(string response, bool eventLock)
        {
            return ExecuteApplication("respond", response, eventLock);
        }

        public SocketEvent SendDisplay(string msg, bool eventLock)
        {
            return ExecuteApplication("send_display", msg, eventLock);
        }

        public SocketEvent ToneDetect(string tone,int[] frequencies,char[] flags,int relativeTimeout,string app,string appdata,int hits,bool eventLock)
        {
            string sfreq = "";
            foreach (int i in frequencies)
                sfreq += "," + i.ToString();
            string sflags = "";
            foreach (char c in flags)
                sflags += "," + c.ToString();
            return ExecuteApplication("tone_detect",tone+" "+sfreq.Substring(1)+" "+sflags.Substring(1)+" +"+relativeTimeout.ToString()+" "+(app == null ? "" : app)+" "+(appdata==null ? "" : appdata)+" "+hits.ToString(), eventLock);
        }

        public SocketEvent StopToneDetect(bool eventLock)
        {
            return ExecuteApplication("stop_tone_detect", eventLock);
        }

        public string CurrentTime(string format)
        {
            SocketEvent se = ExecuteApplication("strftime", format, true);
            return se.Message;
        }

        public SocketEvent System(string command, bool eventLock)
        {
            return ExecuteApplication("system", command, eventLock);
        }

        public SocketEvent WaitForSilence(int silenceThreshhold, int silenceHits, int listenHits, int timeoutMS, bool eventLock)
        {
            return ExecuteApplication("wait_for_silence", silenceThreshhold.ToString() + " " + silenceHits.ToString() + " " + listenHits.ToString() + " " + timeoutMS.ToString(), eventLock);
        }

        public SocketEvent Chat(string proto,string from, string to, string message, bool eventLock)
        {
            return ExecuteApplication("chat", proto + "|" + from + "|" + to + "|" + message, eventLock);
        }
        #endregion
        #region Logging
        public enum LogLevels
        {
            Console = 0,
            Alert = 1,
            Crit = 1,
            Err = 3,
            Warning = 4,
            Notice = 5,
            Info = 6,
            Debug = 7
        }
        
        public void Log(LogLevels level, string message)
        {
            ExecuteApplication("log", level.ToString().ToUpper() + " " + message, false);
        }

        public void SetSessionLogLevel(LogLevels level)
        {
            ExecuteApplication("session_loglevel", level.ToString().ToLower(), true);
        }
        #endregion
        #region FIFO
        public void FIFO_In(string queue, string exitSound, string onHold, bool eventLock)
        {
            ExecuteApplication("fifo", queue + " in " + exitSound + (onHold == null ? " " + onHold : ""), eventLock);
        }

        public void FIFO_Out(string queue, bool wait, string foundSound, string onHold, bool eventLock)
        {
            ExecuteApplication("fifo", queue + " out " + (wait ? "wait" : "nowait") + foundSound + (onHold == null ? " " + onHold : ""), eventLock);
        }
        #endregion
        #region Settings
        public void SetDelayEcho(int milliseconds, bool eventLock)
        {
            ExecuteApplication("delay_echo", milliseconds.ToString(), eventLock);
        }

        public void SetJitterBuffer(int milliseconds, int? maxMilliseconds, int? maxDriftMilliseconds, bool eventLock)
        {
            ExecuteApplication("jitterbuffer", milliseconds.ToString() + (maxMilliseconds.HasValue ? ":" + maxMilliseconds.Value.ToString() + (maxDriftMilliseconds.HasValue ? ":" + maxDriftMilliseconds.Value.ToString() : "") : ""), eventLock);
        }

        public void SetAudioLevel(bool incoming, int level)
        {
            if ((level >= -4) && (level <= 4))
                ExecuteApplication("set_audio_level", (incoming ? "read" : "write") + " " + level.ToString(), true);
            else
                throw new Exception("Invalid audio level specified, must be between -4 & 4");
        }

        public void SetGlobalVariable(string variableName, string value,bool eventLock)
        {
            ExecuteApplication("set_global", variableName + "=" + value, eventLock);
        }

        public void SetChannelName(string name)
        {
            ExecuteApplication("set_name", name, true);
        }

        public void SetUser(sDomainExtensionPair extension, string prefix, bool eventLock)
        {
            ExecuteApplication("set_user", extension.Extension + "@" + extension.Domain + (prefix == null ? "" : " " + prefix), eventLock);
        }

        public void SetZombieExec()
        {
            ExecuteApplication("set_zombie_exec", false);
        }

        public void SetVerboseEvents(bool yes)
        {
            ExecuteApplication("verbose_events", yes.ToString(),false);
        }
        #endregion
        #region Bridging
        public SocketEvent BridgeToExtension(sDomainExtensionPair extension, bool eventLock)
        {
            return ExecuteApplication("bridge", "user/" + extension.Extension + "@" + extension.Domain, eventLock);
        }

        public SocketEvent BridgeToMultipleExtensions(sDomainExtensionPair[] extensions, bool sequential, bool eventLock)
        {
            string dstring = "";
            foreach (sDomainExtensionPair sdep in extensions)
            {
                dstring += (sequential ? "," : "|") + "user/" + sdep.Extension + "@" + sdep.Domain;
            }
            if (dstring.Length > 1)
                dstring = dstring.Substring(1);
            return ExecuteApplication("bridge", dstring, eventLock);
        }

        public SocketEvent Voicemail(string context, sDomainExtensionPair extension)
        {
            return ExecuteApplication("voicemail", context + " " + extension.Domain + " " + extension.Extension, true);
        }

        public SocketEvent BridgeOutGateway(string gateway, string number, bool eventLock)
        {
            return ExecuteApplication("bridge", "sofia/gateway/" + gateway + "/" + number, eventLock);
        }

        public void BridgeExport(string variableName, string value, bool BLegOnly)
        {
            ExecuteApplication("bride_export", (BLegOnly ? "nolocal:" : "") + variableName + "=" + value, false);
        }

        public void EavesDrop(string uuid, string requiredGroup,
            string failedSound, string newSound, string idleSound,
            bool enableDTMF, bool eventLock)
        {
            if (requiredGroup != null)
                SetVariable("eavesdrop_require_group", requiredGroup);
            if (failedSound != null)
                SetVariable("eavesdrop_indicate_failed", failedSound);
            if (newSound != null)
                SetVariable("eavesdrop_indicate_new", newSound);
            if (idleSound != null)
                SetVariable("eavesdrop_indicate_idle", idleSound);
            SetVariable("eavesdrop_enable_dtmf", enableDTMF.ToString());
            ExecuteApplication("eavesdrop", (uuid == null ? "all" : uuid), eventLock);
        }

        public void Intercept(string uuid, bool bleg, bool eventLock)
        {
            ExecuteApplication("intercept", (bleg ? "-bleg " : "") + uuid, eventLock);
        }

        public void IVR(string ivrName, bool eventLock)
        {
            ExecuteApplication("ivr", ivrName, eventLock);
        }

        public SocketEvent SoftHold(string offHoldKey, string mohA, string mohB, bool eventLock)
        {
            return ExecuteApplication("soft_hold", offHoldKey + " " + (mohA == null ? "" : mohA) + " " + (mohB == null ? "" : mohB), eventLock);
        }

        public void ThreeWay(string uuid, bool eventLock)
        {
            ExecuteApplication("three_way", uuid, eventLock);
        }

        public SocketEvent Transfer(string desinationNumber, string dialplan, string context,bool eventLock)
        {
            return ExecuteApplication("transfer", desinationNumber + " " + (dialplan == null ? "" : dialplan) + " " + (context == null ? "" : context), eventLock);
        }
        #endregion
        #region Variables
        private void SetVariable(string name, string value)
        {
            if (value==null)
                ExecuteApplication("unset", name, false);
            else
                ExecuteApplication("set", name + "=" + value, false);
        }

        public void ExportSetting(string name, string value)
        {
            ExecuteApplication("export", name + "=" + value, false);
        }
        #endregion
        #region Conferencing
        public void ConferenceSetAutoCallExtension(sDomainExtensionPair extension)
        {
            ExecuteApplication("conference_set_auto_outcall", "USER/" + extension.Extension + "@" + extension.Domain, true);
        }

        public void JoinConference(string name, bool eventLock)
        {
            ExecuteApplication("conference", name, eventLock);
        }

        public void KickFromConference(string conferenceName, string extension, bool eventLock)
        {
            ExecuteApplication("conference", conferenceName + " kick " + extension, eventLock);
        }
        #endregion
        #region Audio
        public void PlayAudioFile(string filePath, bool eventLock)
        {
            ExecuteApplication("playback", filePath, eventLock);
        }

        public void PlayAudioFileEndlessly(string filePath, bool eventLock)
        {
            ExecuteApplication("endless_playback", filePath, eventLock);
        }

        public string PlayAndGetDigits(int minDigits, int maxDigits, int tries, long timeout, string terminators, string file, string invalidFile, string regexp, int? digitTimeout)
        {
            string var = RandomVariable;
            SocketEvent ev = ExecuteApplication("play_and_get_digits", minDigits.ToString() + " " + maxDigits.ToString() + " " + tries.ToString() + " " + timeout.ToString() + " " + terminators + " " + file + " " + (invalidFile != null ? invalidFile : "silence_stream://250") + " " + var + " " + (regexp == null ? "\\d+" : regexp) + " " + (digitTimeout.HasValue ? digitTimeout.ToString() : ""), true);
            string ret = ev[var];
            SetVariable(var, "");
            if (ret != null)
            {
                if (ret.Length < minDigits)
                    ret = null;
                else if (ret == "")
                    ret = null;
            }
            return ret;
        }

        public void PlayAudioFile(string filePath, bool mux, int? loop, int? timeLimit, bool eventLock)
        {
            ExecuteApplication("displace_session", filePath + " " + (loop.HasValue ? "loops=" + loop.Value.ToString() : "") + (mux ? " mux" : "") + (timeLimit.HasValue ? "+" + timeLimit.Value.ToString() : ""), eventLock);
        }

        public SocketEvent GenTones(int milliSeconds, int loops, int Hz, bool eventLock)
        {
            return ExecuteApplication("gentones", "%(" + milliSeconds.ToString() + "," + loops.ToString() + "," + Hz.ToString() + ")", eventLock);
        }

        public SocketEvent GenTones(string toneString, int loops, bool eventLock)
        {
            return ExecuteApplication("gentones", toneString + (loops != 0 ? "|" + loops.ToString() : ""), eventLock);
        }

        public SocketEvent PlayAndDetectSpeech(string filePath, string engine, Dictionary<string, string> parameters, string grammar, bool eventLock)
        {
            string spars = "";
            if (parameters != null)
            {
                foreach (string str in parameters.Keys)
                    spars += str + "=" + parameters[str] + ",";
            }
            if (spars.Length > 0)
                spars = " {" + spars.Substring(0, spars.Length - 1) + "}";
            return ExecuteApplication("play_and_detect_speech", filePath + " detect:" + engine + spars + (grammar == null ? "" : " " + grammar), eventLock);
        }

        public void PlayFSV(string filePath, bool eventLock)
        {
            ExecuteApplication("play_fsv", filePath, eventLock);
        }

        public SocketEvent Record(string path, int? timeLimit, int? silenceThreshhold, int? silenceHits, bool eventLock)
        {
            return ExecuteApplication("record", path + " " + (timeLimit.HasValue ? timeLimit.ToString() : "") + " " + (silenceThreshhold.HasValue ? silenceThreshhold.ToString() : "") + " " + (silenceHits.HasValue ? silenceHits.ToString() : ""), eventLock);
        }

        public SocketEvent RecordSession(string path, bool eventLock)
        {
            return ExecuteApplication("record_session", path, eventLock);
        }

        public SocketEvent StopRecordSession(string path, bool eventLock)
        {
            return ExecuteApplication("stop_record_session", path, eventLock);
        }

        public enum SayTypes
        {
            NUMBER,
            ITEMS,
            PERSONS,
            MESSAGES,
            CURRENCY,
            TIME_MEASUREMENT,
            CURRENT_DATE,
            CURRENT_TIME,
            CURRENT_DATE_TIME,
            TELEPHONE_NUMBER,
            TELEPHONE_EXTENSION,
            URL,
            IP_ADDRESS,
            EMAIL_ADDRESS,
            POSTAL_ADDRESS,
            ACCOUNT_NUMBER,
            NAME_SPELLED,
            NAME_PHONETIC,
            SHORT_DATE_TIME
        }

        public enum SayMethods
        {
            N_A,
            PRONOUNCED,
            ITERATED,
            COUNTED
        }

        public enum SayGenders
        {
            FEMININE,
            MASCULINE,
            NEUTER
        }

        public void Say(string language, SayTypes type, SayMethods method, SayGenders gender, string text, bool eventLock)
        {
            ExecuteApplication("say", language + " " + type.ToString() + " " + method.ToString().Replace("_", "/") + " " + gender.ToString() + " " + text, eventLock);
        }

        public enum BroadcastLegs{
            aleg,
            bleg,
            both
        }

        public void ScheduleBroadcast(int timeMS, string path, BroadcastLegs leg,bool eventLock)
        {
            ExecuteApplication("sched_broadcast", "+" + timeMS.ToString() + " " + path + " " + leg.ToString(),eventLock);
        }

        public SocketEvent Speak(string engine, string voice, string text, string timerName,bool eventLock)
        {
            return ExecuteApplication("speak", engine + "|" + voice + "|" + text + (timerName != null ? "|" + timerName : ""), eventLock);
        }

        public void StopDisplace(string path, bool eventLock)
        {
            ExecuteApplication("stop_displace_session", path, eventLock);
        }
        #endregion
        #region DigitActions
        public enum BindDigitTargetLegs
        {
            aleg,
            peer,
            both
        }

        public enum BindDigitsEventLegs
        {
            peer,
            self,
            both
        }

        public void BindDigitAction(string realm, DialableNumber digits, string command, string arguements, BindDigitTargetLegs? targetLeg, BindDigitsEventLegs? eventLeg)
        {
            ExecuteApplication("bind_digit_action", "realm," + digits.ToString() + "," + command + "," + (arguements == null ? "" : arguements) + "," + (targetLeg.HasValue ? targetLeg.Value.ToString() : "aleg") + "," + (eventLeg.HasValue ? eventLeg.Value.ToString() : "self"), false);
        }

        public void BindDigitAction(string realm, string regex, string command, string arguements, BindDigitTargetLegs? targetLeg, BindDigitsEventLegs? eventLeg)
        {
            ExecuteApplication("bind_digit_action", "realm,~" + regex + "," + command + "," + (arguements == null ? "" : arguements) + "," + (targetLeg.HasValue ? targetLeg.Value.ToString() : "aleg") + "," + (eventLeg.HasValue ? eventLeg.Value.ToString() : "self"), false);
        }

        public void ClearDigitAction(string realm, bool eventLock)
        {
            ExecuteApplication("clear_digit_action", (realm == null ? "all" : realm), eventLock);
        }

        public void DigitActionSetRealm(string realm, bool eventLock)
        {
            ExecuteApplication("digit_action_set_realm", (realm == null ? "all" : realm), eventLock);
        }
        #endregion
        #region Meta
        public enum MetaAppLegTypes
        {
            a,
            b,
            ab
        }

        public enum MetaAppFlags
        {
            Respond_A = 'a',
            Respond_B = 'b',
            Respond_Opposite = 'o',
            Respond_Same = 's',
            Execute_Inline = 'i',
            OneTimeUse = '1'
        }

        public void BindMetaApp(DialableNumber digits, MetaAppLegTypes leg, MetaAppFlags[] flags, string application, string pars)
        {
            string sflags = "";
            if (flags != null)
            {
                foreach (MetaAppFlags flg in flags)
                    sflags += (char)flg;
            }
            ExecuteApplication("bind_meta_app", digits.ToString() + " " + leg.ToString() + " " + sflags + " " + application + (pars == null ? "" : "::" + pars), false);
        }

        public void UnBindMetaApp(DialableNumber digits, bool eventLock)
        {
            ExecuteApplication("unbind_meta_app", digits.ToString(), eventLock);
        }
        #endregion
        #region DetectSpeech
        public void DetectSpeech(string modName, string grammarName, string grammarPath, string address, bool eventLock)
        {
            ExecuteApplication("detect_speech", modName + " " + grammarName + " " + grammarPath + (address == null ? "" : " " + address), eventLock);
        }

        public void DetectSpeech_Grammar(string grammarName, string grammarPath, bool eventLock)
        {
            ExecuteApplication("detect_speech", "grammar " + grammarName + (grammarPath == null ? "" : " " + grammarPath), eventLock);
        }

        public void DetectSpeech_GrammarOn(string grammarName, bool eventLock)
        {
            ExecuteApplication("detect_speech", "grammaron " + grammarName, eventLock);
        }

        public void DetectSpeech_GrammarOff(string grammarName, bool eventLock)
        {
            ExecuteApplication("detect_speech", "grammaroff " + grammarName, eventLock);
        }

        public void DetectSpeech_GrammarsAllOff(bool eventLock)
        {
            ExecuteApplication("detect_speech", "grammarsalloff", eventLock);
        }

        public void DetectSpeech_NoGrammar(string grammarName, bool eventLock)
        {
            ExecuteApplication("detect_speech", "nogrammar " + grammarName, eventLock);
        }

        public void DetectSpeech_Param(string paramName, string paramValue, bool eventLock)
        {
            ExecuteApplication("detect_speech", "param " + paramName + " " + paramValue, eventLock);
        }

        public void DetectSpeech_Pause(bool eventLock)
        {
            ExecuteApplication("detect_speech", "pause", eventLock);
        }

        public void DetectSpeech_Resume(bool eventLock)
        {
            ExecuteApplication("detect_speech", "resumse", eventLock);
        }

        public void DetectSpeech_StartInputTimers(bool eventLock)
        {
            ExecuteApplication("detect_speech", "start_input_timers", eventLock);
        }

        public void DetectSpeech_Stop(bool eventLock)
        {
            ExecuteApplication("detect_speech", "stop", eventLock);
        }
        #endregion
        #region DTMF
        public void FlushDTMF(bool eventLock)
        {
            ExecuteApplication("flush_dtmf", eventLock);
        }

        public enum DTMFQueueDelays
        {
            Half_Second = 'w',
            Full_Second = 'W',
            None
        }

        public void QueueDTMF(DTMFQueueDelays delay, string dtmfString, int? durationMS)
        {
            ExecuteApplication("queue_dtmf", (delay == DTMFQueueDelays.None ? "" : ((char)delay).ToString()) + dtmfString + (durationMS.HasValue ? "@" + durationMS.ToString() : ""), true);
        }

        public SocketEvent SendDTMF(DTMFQueueDelays delay, string dtmfString, int? durationMS)
        {
            return ExecuteApplication("send_dtmf", (delay == DTMFQueueDelays.None ? "" : ((char)delay).ToString()) + dtmfString + (durationMS.HasValue ? "@" + durationMS.ToString() : ""), true);
        }

        public void StartDTMF(bool eventLock)
        {
            ExecuteApplication("start_dtmf", eventLock);
        }

        public void StopDTMF(bool eventLock)
        {
            ExecuteApplication("stop_dtmf", eventLock);
        }

        public void StartDTMFGenerate(bool eventLock)
        {
            ExecuteApplication("start_dtmf_generate", eventLock);
        }

        public void StopDTMFGenerate(bool eventLock)
        {
            ExecuteApplication("stop_dtmf_generate", eventLock);
        }
        #endregion
        #region Scripts
        public SocketEvent Javascript(string scriptPath, string[] args, bool eventLock)
        {
            string sargs = "";
            if (args != null)
            {
                foreach (string str in args)
                    sargs += str + " ";
            }
            return ExecuteApplication("javascript", scriptPath + " " + sargs.Trim(), eventLock);
        }

        public SocketEvent Lua(string scriptPath, string[] args, bool eventLock)
        {
            string sargs = "";
            if (args != null)
            {
                foreach (string str in args)
                    sargs += str + " ";
            }
            return ExecuteApplication("lua", scriptPath + " " + sargs.Trim(), eventLock);
        }
        #endregion
        #region Parking
        public void Park(bool eventLock)
        {
            ExecuteApplication("park", eventLock);
        }

        public SocketEvent ValetPark_Put(string parkingLot, int stallNumber,bool eventLock)
        {
            return ExecuteApplication("valet_park", parkingLot + " " + stallNumber.ToString(), eventLock);
        }

        public SocketEvent ValetPark_Put(string parkingLot,int minStall,int maxStall, bool eventLock)
        {
            return ExecuteApplication("valet_park", parkingLot + " auto in "+minStall.ToString()+" "+maxStall.ToString(), eventLock);
        }

        public SocketEvent ValetPark_Ask(string parkingLot, int minDigits, int maxDigits, int timeout, string prompt,bool eventLock)
        {
            return ExecuteApplication("valet_park", parkingLot + " ask " + minDigits.ToString() + " " + maxDigits.ToString() + " " + timeout.ToString() + " " + prompt, eventLock);
        }

        public SocketEvent ValetPark_Get(string parkingLot, int stallNumber, bool eventLock)
        {
            return ExecuteApplication("valet_park", parkingLot + " " + stallNumber.ToString(), eventLock);
        }
        #endregion
        #region Schedule
        public void ScheduleHangup(int seconds, string reason)
        {
            ExecuteApplication("sched_hangup", "+" + seconds.ToString() + " " + reason, true);
        }

        public void ScheduleTransfer(int seconds, sDomainExtensionPair extension, string dialplan, string context)
        {
            ExecuteApplication("sched_transfer", "+" + seconds.ToString() + " " + extension.Extension + "@" + extension.Domain + (dialplan != null ? " " + dialplan + " " + context : ""), true);
        }
        #endregion
        private SocketEvent ExecuteApplication(string applicationName, bool eventLock)
        {
            return ExecuteApplication(applicationName, null, eventLock);
        }

        private SocketEvent ExecuteApplication(string applicationName, string applicationArguements, bool eventLock)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("sendmsg");
            if (eventLock)
                sb.AppendLine("event-lock: true");
            sb.AppendLine("call-command: execute");
            sb.AppendLine("execute-app-name: " + applicationName);
            if (applicationArguements != null)
                sb.AppendLine("execute-app-arg: " + applicationArguements);
            sb.AppendLine("");
            ManualResetEvent mreComm = new ManualResetEvent(false);
            SocketEvent ret = null;
            lock (_awaitingCommands)
            {
                _awaitingCommands.Enqueue(mreComm);
            }
            _sendCommand(sb.ToString());
            if (eventLock)
            {
                lock (_awaitingCommand)
                {
                    _awaitingCommand = applicationName + " " + (applicationArguements == null ? "" : applicationArguements);
                }
            }
            mreComm.WaitOne();
            if (eventLock)
            {
                _awaitingCommandEvent.WaitOne();
                lock (_awaitingCommand)
                {
                    _awaitingCommand = "";
                    ret = _currentEvent;
                    _currentEvent = null;
                    _awaitingCommandEvent.Reset();
                }
            }
            return ret;
        }
        #endregion

        #region Variables
        public Dictionary<string, string>.KeyCollection Keys
        {
            get { return _properties.Keys; }
        }

        public string this[string name]
        {
            get
            {
                string ret = null;
                lock (_properties)
                {
                    if (_properties.ContainsKey(name))
                        ret = _properties[name];
                }
                return ret;
            }
            set
            {
                lock (_properties)
                {
                    _properties.Remove(name);
                    SetVariable(name, value);
                    if (value != null)
                        _properties.Add(name, value);
                }
            }
        }

        public string GetSystemVariable(string name)
        {
            string ret = _IssueAPICommand("eval $${" + name + "}", true);
            ret = (ret != null ? (ret.StartsWith("$") ? ret.Substring(1) : ret) : null);
            return ret;
        }

        public string UUID
        {
            get { return this["Unique-ID"]; }
        }

        public string CallerUUID
        {
            get { return this["Caller-Unique-ID"]; }
        }

        public string DestinationNumber
        {
            get { return this["Channel-Destination-Number"]; }
            set { this["Channel-Destination-Number"] = value; }
        }

        public string Domain
        {
            get { return this["variable_domain_name"]; }
            set
            {
                this["variable_domain_name"] = value;
            }
        }

        public string Context
        {
            get { return this["variable_user_context"]; }
            set { this["variable_user_context"] = value; }
        }

        public string ChannelName
        {
            get { return this["Channel-Name"]; }
        }

        private string _baseDir;
        public string BASE_DIR
        {
            get { return _baseDir; }
            set { _baseDir = value; }
        }
        #endregion

        protected override void _processMessageQueue(Queue<ASocketMessage> messages)
        {
            while (messages.Count > 0)
            {
                ASocketMessage asm = messages.Dequeue();
                if (asm is CommandReplyMessage)
                {
                    if (asm["Job-UUID"] == null)
                    {
                        ManualResetEvent mre = null;
                        lock (_awaitingCommands)
                        {
                            if (_awaitingCommands.Count > 0)
                                mre = _awaitingCommands.Dequeue();
                        }
                        if (mre != null)
                            mre.Set();
                    }
                }
                else if (asm is SocketEvent)
                {
                    SocketEvent se = (SocketEvent)asm;
                    lock (_properties)
                    {
                        se.CopyParameters(ref _properties);
                    }
                    if (se.EventName == EXECUTE_COMPLETE_EVENT_NAME)
                    {
                        if ((se.CallerUUID == CallerUUID) && (se.ChannelName == ChannelName))
                        {
                            lock (_awaitingCommand)
                            {
                                if (_awaitingCommand == se[APP_NAME_VARIABLE_NAME] + " " + (se[APP_DATA_VARIABLE_NAME] == null ? "" : se[APP_DATA_VARIABLE_NAME]))
                                {
                                    _currentEvent = se;
                                    _awaitingCommandEvent.Set();
                                }
                            }
                        }
                    }
                    else if (se.EventName == CHANNEL_END_EVENT_NAME)
                    {
                        if ((se.CallerUUID == CallerUUID) && (se.ChannelName == ChannelName))
                        {
                            _isHungup = true;
                            _currentEvent = se;
                            _awaitingCommandEvent.Set();
                            Close();
                        }
                    }
                }
            }
        }

        protected override void _close()
        {
        }

        #region INIT
        protected override void _preSocketReady()
        {
            _isHungup = false;
            _awaitingCommands = new Queue<ManualResetEvent>();
            BufferedStream _in = new BufferedStream(new NetworkStream(socket));
            _sendCommand("connect");
            _properties = ASocketMessage.ParseProperties(ReadMessage(_in));
            string[] keys = new string[_properties.Count];
            _properties.Keys.CopyTo(keys, 0);
            foreach (string str in keys)
            {
                string val = _properties[str];
                _properties.Remove(str);
                _properties.Add(str, Uri.UnescapeDataString(val));
            }
            _sendCommand("linger");
            ReadMessage(_in);
            _sendCommand("api strftime %Y-%m-%d-%H-%M");
            string[] split = new CommandReplyMessage(ReadMessage(_in)).ReplyMessage.Split('-');
            _startTime = new DateTime(int.Parse(split[0]), int.Parse(split[1]), int.Parse(split[2]), int.Parse(split[3]), int.Parse(split[4]), 0);
            RegisterEvent(CHANNEL_END_EVENT_NAME);
            RegisterEvent(EXECUTE_COMPLETE_EVENT_NAME);
        }

        private string ReadMessage(BufferedStream _in)
        {
            string ret = "";
            string line;
            while ((line = streamReadLine(_in)) != null)
            {
                ret += line + "\n";
                if (ret.ToString().EndsWith("\n\n"))
                    break;
            }
            if (ret.Contains("Content-Length:"))
            {
                int conLen = int.Parse(ASocketMessage.ParseProperties(ret)["Content-Length"]);
                for (int x = 0; x < conLen; x++)
                {
                    ret += Convert.ToChar(_in.ReadByte());
                }
            }
            return ret.Trim();
        }

        private string streamReadLine(BufferedStream _in)
        {
            int next_char;
            string data = "";
            while (true)
            {
                next_char = _in.ReadByte();
                if (next_char == '\n') { break; }
                if (next_char == '\r') { continue; }
                if (next_char == -1) { Thread.Sleep(1); continue; };
                data += Convert.ToChar(next_char);
            }
            return data;
        }
        #endregion
    }
}
