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
public class ViewState {
 public DateTime? SelectedMonth {get;set;}
 public DateTime? AsOfDate {get;set;}
 public bool FollowToday {get;set;}
 public ViewState(){FollowToday=true;}
}
}
