using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ScreenLine {
 internal sealed class AgentNote { public string Text {get;set;} public bool Favorite {get;set;} public string Agent {get;set;} public string Category {get;set;} public bool Archived {get;set;} }
 internal sealed class AgentProject {
  public string Id {get;set;} public string Name {get;set;} public string Color {get;set;}
  public string Decoration {get;set;}
  public Dictionary<string,AgentNote> Notes {get;set;}
  public AgentProject(){ Notes=new Dictionary<string,AgentNote>(); }
 }
 internal sealed class AgentIndex {
  public int Version {get;set;} public string Active {get;set;} public bool Quiet {get;set;}
  public string Decoration {get;set;} public List<AgentProject> Projects {get;set;}
  public string InboxId {get;set;} public bool UnifiedInbox {get;set;} public string SelectedAgent {get;set;} public List<string> LastClear {get;set;}
  public bool SkipClearConfirm {get;set;}
  public double? BookmarkPosition {get;set;}
  public uint CaptureKey {get;set;}public uint CaptureModifiers {get;set;}
  public AgentIndex(){Version=1;Decoration="flowers";Projects=new List<AgentProject>();CaptureKey=0xC0;}
 }
 internal sealed class AgentProjects {
  internal static readonly string[] AgentNames={"gemini","grok","claude","deepseek","qwen","gpt","kimi"};
  internal static readonly string[] Decorations={"flowers","stars","floating"};
  internal readonly string Root; internal readonly AgentIndex Index;
  internal AgentProject Current {get{return Index.Projects.First(p=>p.Id==Index.Active);}}
  internal AgentProject Inbox {get{return Index.Projects.FirstOrDefault(p=>p.Id==Index.InboxId)??Index.Projects.FirstOrDefault(p=>p.Name=="收集箱")??Index.Projects[0];}}
  internal readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=16*1024*1024};
  static readonly string[] colors={"#8B7AD7","#75A3AA","#D89BAC","#D5AC79","#8EA88C","#809AC7","#AC93BA"};
  internal AgentProjects(string root){
   Root=Path.GetFullPath(root);Directory.CreateDirectory(Root);string path=Path.Combine(Root,"projects.json");
   if(File.Exists(path)){
    try{Index=Json.Deserialize<AgentIndex>(File.ReadAllText(path,Encoding.UTF8));Validate();}
    catch(Exception error){throw new InvalidDataException("项目索引无法读取，已保留原文件，请检查 projects.json 和 .previous 备份。",error);}
   }else{Index=new AgentIndex();Create("收集箱");}
  }
  void Validate(){
   if(Index==null||Index.Version!=1||Index.Projects==null||Index.Projects.Count==0)throw new InvalidDataException();
   var ids=new HashSet<string>();foreach(var p in Index.Projects){Guid id;if(!Guid.TryParseExact(p.Id,"N",out id)||!ids.Add(p.Id)||string.IsNullOrWhiteSpace(p.Name))throw new InvalidDataException();if(p.Notes==null)p.Notes=new Dictionary<string,AgentNote>();}
   if(!ids.Contains(Index.Active))throw new InvalidDataException();
   foreach(var p in Index.Projects)if(!Decorations.Contains(p.Decoration))p.Decoration=Decorations.Contains(Index.Decoration)?Index.Decoration:"flowers";
  }
  internal AgentProject Create(string name){
   name=(name??"").Trim();if(name.Length==0||name.Length>60)throw new ArgumentException("项目名称请输入 1–60 个字符。");
   if(Index.Projects.Any(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("已经有同名项目了，换个名字吧。");
   var project=new AgentProject{Id=Guid.NewGuid().ToString("N"),Name=name,Color=colors[Index.Projects.Count%colors.Length],Decoration="flowers"};
   Index.Projects.Add(project);Index.Active=project.Id;Save();return project;
  }
  internal string Folder(AgentProject p){return Path.Combine(Root,"projects",p.Id);}
  internal void Save(){
   string path=Path.Combine(Root,"projects.json"),temp=path+".tmp";
   File.WriteAllText(temp,Json.Serialize(Index),new UTF8Encoding(false));
   if(File.Exists(path))File.Replace(temp,path,path+".previous");else File.Move(temp,path);
  }
  internal static string ShotId(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToLowerInvariant()))).Replace("-","").Substring(0,24);}
  internal AgentNote Note(string id){return Note(Index.UnifiedInbox?Inbox:Current,id);}
  internal AgentNote Note(AgentProject project,string id){
   AgentNote note;if(!project.Notes.TryGetValue(id,out note)||note==null){note=new AgentNote{Text=""};project.Notes[id]=note;}
   if(!AgentNames.Contains(note.Agent)){
    // Derive the default from identity, never gallery order: new arrivals must not swap existing faces.
    using(var sha=SHA256.Create())note.Agent=AgentNames[sha.ComputeHash(Encoding.UTF8.GetBytes(id))[0]%AgentNames.Length];
   }
   return note;
  }
  internal void SetAgent(string id,string agent){if(!AgentNames.Contains(agent))throw new ArgumentException("请选择已有的小伙伴。");Note(id).Agent=agent;Save();}
  internal void SetDecoration(string decoration){if(!Decorations.Contains(decoration))throw new ArgumentException("请选择花枝、星环或浮空。");string previous=Current.Decoration;Current.Decoration=decoration;try{Save();}catch{Current.Decoration=previous;throw;}}
  internal void UnifyInbox(){
   if(Index.UnifiedInbox)return;
   var inbox=Inbox;var store=new StateStore(Folder(inbox));var paths=new HashSet<string>(store.State.Collection,StringComparer.OrdinalIgnoreCase);
   foreach(var project in Index.Projects){
    var source=project==inbox?store:new StateStore(Folder(project));
    foreach(string path in source.State.Collection.Concat(source.State.Images)){
     paths.Add(path);string id=ShotId(path);AgentNote note;
     if(!inbox.Notes.TryGetValue(id,out note)||note==null){
      var original=Note(project,id);note=new AgentNote{Text=original.Text,Favorite=original.Favorite,Agent=original.Agent,Archived=original.Archived,Category=original.Category};inbox.Notes[id]=note;
     }
     if(project!=inbox&&string.IsNullOrEmpty(note.Category))note.Category=project.Id;
    }
   }
   store.State.Collection=paths.ToList();store.State.Images.Clear();store.Save();
   Index.InboxId=inbox.Id;Index.UnifiedInbox=true;if(!AgentNames.Contains(Index.SelectedAgent))Index.SelectedAgent="gemini";Save();
  }
  internal void SetCategory(string id,string category){if(category!=Inbox.Id&&!Index.Projects.Any(p=>p.Id==category))throw new ArgumentException("找不到这个分类。");Note(id).Category=category==Inbox.Id?null:category;Save();}
  internal void ClearHistory(IEnumerable<string> ids){var batch=ids.Distinct().Where(id=>!Note(id).Favorite).ToList();if(batch.Count==0)return;Index.LastClear=batch.Concat(Index.LastClear??new List<string>()).Distinct().ToList();foreach(string id in batch)Note(id).Archived=true;Save();}
  internal void RestoreHistory(){if(Index.LastClear==null)return;foreach(string id in Index.LastClear)Note(id).Archived=false;Index.LastClear=null;Save();}
  internal async Task<string> Export(Gallery gallery,string folder){
   string parent=Path.GetFullPath(folder);if(!Directory.Exists(parent))throw new DirectoryNotFoundException("导出文件夹不存在。");
   var project=Current;var items=gallery.Collected.Where(i=>!Note(ShotId(i.Path)).Archived).ToArray();
   var rows=items.Select((item,index)=>{var note=Note(ShotId(item.Path));return new{file=(index+1).ToString("D4")+Path.GetExtension(item.Path),name=Path.GetFileName(item.Path),note=note.Text,favorite=note.Favorite,agent=note.Agent,category=note.Category,hung=gallery.Contains(item.Path),writtenUtc=item.Written.ToUniversalTime().ToString("o"),width=item.Image.PixelWidth,height=item.Image.PixelHeight};}).ToArray();
   string manifest=Json.Serialize(new{format="AgentBloom",version=1,name=project.Name,decoration=Index.UnifiedInbox?Index.SelectedAgent:project.Decoration,exportedUtc=DateTime.UtcNow.ToString("o"),images=rows});
   string token=Guid.NewGuid().ToString("N");string stage=Path.GetFullPath(Path.Combine(parent,".AgentBloom-export-"+token+".partial"));
   string target=Path.GetFullPath(Path.Combine(parent,"AgentBloom-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+token));
   string boundary=parent.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   if(!stage.StartsWith(boundary,StringComparison.OrdinalIgnoreCase)||!target.StartsWith(boundary,StringComparison.OrdinalIgnoreCase))throw new IOException("导出路径超出所选文件夹。");
   return await Task.Run(delegate{
    Directory.CreateDirectory(stage);
    try{
     for(int i=0;i<items.Length;i++)File.Copy(items[i].Path,Path.Combine(stage,rows[i].file));
     File.WriteAllText(Path.Combine(stage,"project.json"),manifest,new UTF8Encoding(false));
     File.WriteAllText(Path.Combine(stage,"README.txt"),"AgentBloom 项目副本\r\n\r\n图片为独立文件；project.json 记录备注、收藏、角色、挂饰和悬挂状态。\r\n此目录可直接浏览、复制或归档。当前版本暂不支持将整包元数据导回应用。\r\n",new UTF8Encoding(false));
     Directory.Move(stage,target);return target;
    }catch{
     // Both paths are generated children of the selected directory, checked above.
     try{Directory.Delete(stage,true);}catch{}
     throw;
    }
   });
  }
 }
}
