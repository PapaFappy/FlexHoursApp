using System;
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
public class Store { public Dictionary<string,MonthData> Months { get; set; } public ViewState View {get;set;} public DateTime? SavedAtUtc {get;set;} public AppearanceSettings Appearance {get;set;} public ChromeSyncSettings ChromeSync {get;set;}
public Store(){ChromeSync=new ChromeSyncSettings();Appearance=AppearanceSettings.Defaults(true);Months=new Dictionary<string,MonthData>();View=new ViewState();} }
}
