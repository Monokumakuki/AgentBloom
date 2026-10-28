using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
namespace ScreenLine {
 internal static class AgentStartup {
  const string Run=@"Software\Microsoft\Windows\CurrentVersion\Run",Settings=@"Software\AgentBloom";
  internal static bool Enabled {get{using(var key=Registry.CurrentUser.OpenSubKey(Run))return key!=null&&key.GetValue("AgentBloom")!=null;}}
  internal static void ConfigureDefault(){
   if(AgentApplication.Qa)return;
   // Public builds do not opt new users into login startup.
   if(Enabled)SetEnabled(true);
  }

  internal static void SetEnabled(bool enabled){
   if(AgentApplication.Qa)return;
   using(var key=Registry.CurrentUser.CreateSubKey(Run)){if(enabled)key.SetValue("AgentBloom","\""+Assembly.GetEntryAssembly().Location+"\" --startup");else key.DeleteValue("AgentBloom",false);}
   using(var key=Registry.CurrentUser.CreateSubKey(Settings))key.SetValue("StartupOptOut",enabled?0:1,RegistryValueKind.DWord);
  }
  static void EnsureDesktopShortcut(){
   try{
    string desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    string shortcut=Path.Combine(desktop,"拾影花笺.lnk");
    string location=Assembly.GetEntryAssembly().Location;
    {
     Type shellType=Type.GetTypeFromProgID("WScript.Shell");
     if(shellType!=null){
      dynamic shell=Activator.CreateInstance(shellType);
      var link=shell.CreateShortcut(shortcut);
      link.TargetPath=location;
      link.WorkingDirectory=Path.GetDirectoryName(location);
      link.Description="AgentBloom · 让小灵感，有个家。";
      link.IconLocation=location+",0";
      link.Save();
     }
    }
   }catch{}
  }
 }
}
