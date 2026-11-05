using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FlexHours {
public static class BrowserReader {
 public static readonly string Profile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlexHours","BambooBrowser");
 public static string CompanyUrl(string text) {
  if(string.IsNullOrWhiteSpace(text))throw new ArgumentException("Enter your BambooHR company first.");text=text.Trim();if(Regex.IsMatch(text,@"^[a-zA-Z0-9][a-zA-Z0-9-]{0,62}$"))text="https://"+text+".bamboohr.com/";else if(Regex.IsMatch(text,@"^[a-zA-Z0-9][a-zA-Z0-9-]{0,62}\.bamboohr\.com/?$"))text="https://"+text;
  Uri u;if(!Uri.TryCreate(text,UriKind.Absolute,out u)||u.Scheme!="https"||!u.IsDefaultPort||!Regex.IsMatch(u.Host,@"^[a-z0-9][a-z0-9-]{0,62}\.bamboohr\.com$",RegexOptions.IgnoreCase)||!string.IsNullOrEmpty(u.UserInfo)||!string.IsNullOrEmpty(u.Fragment)||!string.IsNullOrEmpty(u.Query))throw new Exception("Enter your company subdomain or its HTTPS BambooHR address.");
  return "https://"+u.Host+"/";
 }
 public static void Open(string company) {
  var candidates=new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Microsoft","Edge","Application","msedge.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Microsoft","Edge","Application","msedge.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Google","Chrome","Application","chrome.exe")};
  string exe=candidates.FirstOrDefault(File.Exists);if(exe==null)throw new Exception("Microsoft Edge or Google Chrome was not found. You can still paste the summary from your browser.");
  Directory.CreateDirectory(Profile);Process.Start(new ProcessStartInfo(exe,"--user-data-dir=\""+Profile+"\" --remote-debugging-port=0 --remote-debugging-address=127.0.0.1 --no-first-run --no-default-browser-check "+CompanyUrl(company)){UseShellExecute=false});
 }
 public static async Task<string> Read(string company, CancellationToken cancellationToken) {
  cancellationToken.ThrowIfCancellationRequested();
  string host=new Uri(CompanyUrl(company)).Host;string portFile=Path.Combine(Profile,"DevToolsActivePort");
  if(!File.Exists(portFile))throw new Exception("Open BambooHR using the button first. If your company disables browser automation, use Paste summary instead.");
  var lines=File.ReadAllLines(portFile);int port;if(lines.Length<1||!int.TryParse(lines[0],out port)||port<1024||port>65535)throw new Exception("Browser connection is not ready. Close its window and open it again.");
  var req=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:"+port+"/json/list");req.Proxy=null;req.AllowAutoRedirect=false;
  string json;using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {timeout.CancelAfter(10000);using(timeout.Token.Register(()=>req.Abort()))using(var response=await req.GetResponseAsync())using(var reader=new StreamReader(response.GetResponseStream()))json=await reader.ReadToEndAsync();}
  var serializer=new JavaScriptSerializer();var targets=serializer.DeserializeObject(json) as object[];if(targets==null)throw new Exception("Unexpected browser response.");
  var matching=new List<Dictionary<string,object>>();
  foreach(var item in targets){var target=item as Dictionary<string,object>;if(target==null||!target.ContainsKey("url")||Convert.ToString(target["type"])!="page")continue;Uri u;if(Uri.TryCreate(Convert.ToString(target["url"]),UriKind.Absolute,out u)&&u.Scheme=="https"&&u.Host==host)matching.Add(target);}
  if(matching.Count==0)throw new Exception("Open your company's BambooHR Timesheet page in the separate browser window first.");
  if(matching.Count>1)throw new Exception("Keep just one BambooHR tab open in the separate browser window, showing the month to import.");
  var endpoint=new Uri(Convert.ToString(matching[0]["webSocketDebuggerUrl"]));if(endpoint.Scheme!="ws"||endpoint.Host!="127.0.0.1"||endpoint.Port!=port)throw new Exception("Unexpected browser connection address.");
  using(var ws=new ClientWebSocket())using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
   deadline.CancelAfter(15000);
   await ws.ConnectAsync(endpoint,deadline.Token);
   string expression="(()=>{if(location.protocol!=='https:'||location.hostname!=="+serializer.Serialize(host)+")throw Error('Wrong page');return document.body.innerText.slice(0,200000);})()";
   var payload=Encoding.UTF8.GetBytes(serializer.Serialize(new{id=1,method="Runtime.evaluate",@params=new{expression=expression,returnByValue=true}}));await ws.SendAsync(new ArraySegment<byte>(payload),WebSocketMessageType.Text,true,deadline.Token);
   var buffer=new byte[8192];while(true){using(var memory=new MemoryStream()){WebSocketReceiveResult result;do{result=await ws.ReceiveAsync(new ArraySegment<byte>(buffer),deadline.Token);if(result.MessageType==WebSocketMessageType.Close)throw new Exception("Browser connection closed.");memory.Write(buffer,0,result.Count);if(memory.Length>2000000)throw new Exception("Browser response too large.");}while(!result.EndOfMessage);
    var message=serializer.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(memory.ToArray()));if(!message.ContainsKey("id")||Convert.ToInt32(message["id"])!=1)continue;
    if(message.ContainsKey("error"))throw new Exception("The browser could not read the page.");var outer=message["result"] as Dictionary<string,object>;if(outer==null||outer.ContainsKey("exceptionDetails"))throw new Exception("The page changed while reading. Open your Timesheet and try again.");var inner=outer["result"] as Dictionary<string,object>;if(inner==null||!inner.ContainsKey("value"))throw new Exception("No visible summary text was returned.");return Convert.ToString(inner["value"]);
   }}
  }
 }
}
}
