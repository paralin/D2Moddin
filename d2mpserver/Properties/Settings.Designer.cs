﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.34014
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace d2mpserver.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "12.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("ws://ddp2.d2modd.in:4502/ServerController")]
        public string serverIP {
            get {
                return ((string)(this["serverIP"]));
            }
            set {
                this["serverIP"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("10")]
        public int serverCount {
            get {
                return ((int)(this["serverCount"]));
            }
            set {
                this["serverCount"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("{{exeloc}}\\dotaserver\\")]
        public string workingDir {
            get {
                return ((string)(this["workingDir"]));
            }
            set {
                this["workingDir"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("https://s3-us-west-2.amazonaws.com/d2mpclient/steamcmd.exe")]
        public string steamcmd {
            get {
                return ((string)(this["steamcmd"]));
            }
            set {
                this["steamcmd"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("d2modding")]
        public string SteamCMDLogin {
            get {
                return ((string)(this["SteamCMDLogin"]));
            }
            set {
                this["SteamCMDLogin"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Quantum1337")]
        public string SteamCMDPass {
            get {
                return ((string)(this["SteamCMDPass"]));
            }
            set {
                this["SteamCMDPass"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("https://s3-us-west-2.amazonaws.com/d2mpclient/srcds.exe")]
        public string srcds {
            get {
                return ((string)(this["srcds"]));
            }
            set {
                this["srcds"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("kwxmMKDcuVjQNutZOwZy")]
        public string connectPassword {
            get {
                return ((string)(this["connectPassword"]));
            }
            set {
                this["connectPassword"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string devArgs {
            get {
                return ((string)(this["devArgs"]));
            }
            set {
                this["devArgs"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool disableUpdate {
            get {
                return ((bool)(this["disableUpdate"]));
            }
            set {
                this["disableUpdate"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool headlessSRCDS {
            get {
                return ((bool)(this["headlessSRCDS"]));
            }
            set {
                this["headlessSRCDS"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool steamVerify {
            get {
                return ((bool)(this["steamVerify"]));
            }
            set {
                this["steamVerify"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("-console -condebug -game dota -nocrashdialog")]
        public string args {
            get {
                return ((string)(this["args"]));
            }
            set {
                this["args"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("28000")]
        public int portRangeStart {
            get {
                return ((int)(this["portRangeStart"]));
            }
            set {
                this["portRangeStart"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("29000")]
        public int portRangeEnd {
            get {
                return ((int)(this["portRangeEnd"]));
            }
            set {
                this["portRangeEnd"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("UNNAMED")]
        public string serverName {
            get {
                return ((string)(this["serverName"]));
            }
            set {
                this["serverName"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("UNKNOWN")]
        public global::d2mpserver.ServerRegion serverRegion {
            get {
                return ((global::d2mpserver.ServerRegion)(this["serverRegion"]));
            }
            set {
                this["serverRegion"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("8")]
        public int ReadyDelay {
            get {
                return ((int)(this["ReadyDelay"]));
            }
            set {
                this["ReadyDelay"] = value;
            }
        }
    }
}
