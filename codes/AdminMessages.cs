using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArtistBlog.Models;
using SkyNet;

namespace ArtistBlog.codes
{
    public class AdminMessages : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await LoadSite();
            HtmlDoc.SetTitle("Studio admin | Messages");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--am-accent:" + site.Settings.Accent : string.Empty);
        }

        private static readonly SemaphoreSlim SessGate = new SemaphoreSlim(1, 1);

        private static string SessionFile => Path.Combine(Root, "temp", "admin", "sessions.json");

        private static async Task<List<Session>> ReadSessions()
        {
            if (!File.Exists(SessionFile))
            {
                return new List<Session>();
            }
            try
            {
                return JsonSerializer.Deserialize<List<Session>>(await File.ReadAllTextAsync(SessionFile), ReadOpts) ?? new List<Session>();
            }
            catch (JsonException)
            {
                return new List<Session>();
            }
        }

        private static async Task WriteSessions(List<Session> list)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SessionFile) ?? Root);
            string tmp = SessionFile + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(list, WriteOpts));
            File.Move(tmp, SessionFile, true);
        }

        private async Task<Session?> Auth()
        {
            string tok = CleanId(GetDataValue("tok"));
            if (tok == string.Empty)
            {
                return null;
            }
            await SessGate.WaitAsync();
            try
            {
                List<Session> list = await ReadSessions();
                int before = list.Count;
                list.RemoveAll(x => x.Exp < DateTime.UtcNow || (x.Sbx != string.Empty && Sandbox(x.Sbx) == string.Empty) || (x.Sbx == string.Empty && Demo));
                Session? s = list.FirstOrDefault(x => x.Tok == tok);
                bool slide = s != null && s.Sbx == string.Empty;
                if (s != null && slide)
                {
                    s.Exp = DateTime.UtcNow.AddHours(8);
                }
                if (list.Count != before || slide)
                {
                    await WriteSessions(list);
                }
                return s;
            }
            finally
            {
                SessGate.Release();
            }
        }

        private static void Chrome(ApiResponse response, SiteData site, Session s)
        {
            int pc = site.Comments.Count(c => c.Status == "pending");
            int um = site.Messages.Count(m => !m.Done);
            response.SetElementContents("am-cnt-AdminComments", pc > 0 ? pc.ToString(CultureInfo.InvariantCulture) : string.Empty);
            response.SetElementContents("am-cnt-AdminMessages", um > 0 ? um.ToString(CultureInfo.InvariantCulture) : string.Empty);
            int left = s.Sbx != string.Empty ? Math.Max(1, (int)Math.Ceiling((Made(s.Sbx).AddMinutes(60) - DateTime.UtcNow).TotalMinutes)) : 0;
            response.SetElementContents("am-demo", s.Sbx != string.Empty ? "<span class=\"am-dl\">Demo copy &middot; resets in </span><span class=\"am-ds\">Demo &middot; </span>" + left + " min" : string.Empty);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminMessagesJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            response.SetElementContents("am-main", MainHtml(site, s));
            Chrome(response, site, s);
            response.ExecuteScript("AdminMessagesJs.loaded();");
            return response;
        }

        public async Task<ApiResponse> SignOut()
        {
            ApiResponse response = new ApiResponse();
            string tok = CleanId(GetDataValue("tok"));
            await SessGate.WaitAsync();
            try
            {
                List<Session> list = await ReadSessions();
                if (list.RemoveAll(x => x.Tok == tok) > 0)
                {
                    await WriteSessions(list);
                }
            }
            finally
            {
                SessGate.Release();
            }
            response.ExecuteScript("AdminMessagesJs.out();");
            return response;
        }

        private static string Topic(string t)
        {
            return t switch { "commission" => "Commission", "print" => "Print", "workshop" => "Workshop", _ => "Hello" };
        }

        private static string Filter(string v)
        {
            return v == "done" || v == "all" ? v : "open";
        }

        private static string InboxHtml(SiteData site, string filter, string id)
        {
            List<Message> list = site.Messages.Where(m => filter == "all" || (filter == "done") == m.Done).OrderByDescending(m => m.Date).ToList();
            Message? cur = list.FirstOrDefault(m => m.Id == id) ?? list.FirstOrDefault();
            int no = site.Messages.Count(m => !m.Done);
            StringBuilder sb = new StringBuilder("<div class=\"am-ptop\"><div><h1>Messages</h1><p>From the contact form. Mark a message done when you have answered it.</p></div></div><div class=\"am-tabs\" role=\"tablist\">");
            foreach ((string k, string label) in new[] { ("open", "Open (" + no + ")"), ("done", "Done"), ("all", "All") })
            {
                sb.Append("<button type=\"button\" role=\"tab\" class=\"" + (k == filter ? "am-act" : string.Empty) + "\" onclick=\"AdminMessagesJs.show('" + k + "')\">" + label + "</button>");
            }
            sb.Append("</div><div class=\"am-inbox\"><div class=\"am-panel am-mlist\">");
            foreach (Message m in list)
            {
                sb.Append("<button type=\"button\" class=\"am-mi" + (m == cur ? " am-on" : string.Empty) + (m.Done ? string.Empty : " am-new") + "\" onclick=\"AdminMessagesJs.open('" + Attr(m.Id) + "')\"><span class=\"am-dot\"></span><b>" + HtmlEncode(m.Name) + "</b><time>" + HtmlEncode(NiceDate(m.Date)) + "</time><small>" + Topic(m.Topic) + " &middot; " + HtmlEncode(m.Text) + "</small></button>");
            }
            if (list.Count == 0)
            {
                sb.Append("<p class=\"am-none\" style=\"padding:14px\">" + (filter == "open" ? "No open messages." : "Nothing here.") + "</p>");
            }
            sb.Append("</div><div class=\"am-panel am-reader\" id=\"am-reader\">");
            if (cur == null)
            {
                sb.Append("<p class=\"am-none\">Select a message to read it.</p>");
            }
            else
            {
                string mid = Attr(cur.Id);
                string subject = Uri.EscapeDataString("Re: your " + Topic(cur.Topic).ToLowerInvariant() + " enquiry");
                sb.Append("<div class=\"am-rhead\"><div><h2>" + HtmlEncode(cur.Name) + "</h2><small><a href=\"mailto:" + Attr(cur.Email) + "?subject=" + subject + "\">" + HtmlEncode(cur.Email) + "</a> &middot; " + HtmlEncode(NiceDate(cur.Date)) + "</small></div><span class=\"am-topic\">" + Topic(cur.Topic) + "</span></div>");
                sb.Append("<div class=\"am-rbody\">" + HtmlEncode(cur.Text) + "</div><div class=\"am-acts\"><a class=\"am-btn am-btn-sm\" href=\"mailto:" + Attr(cur.Email) + "?subject=" + subject + "\">Reply by email</a>");
                sb.Append(cur.Done ? "<button type=\"button\" class=\"am-btn am-btn-ghost am-btn-sm\" onclick=\"AdminMessagesJs.act('" + mid + "','open')\">Mark as open</button>" : "<button type=\"button\" class=\"am-btn am-btn-ghost am-btn-sm\" onclick=\"AdminMessagesJs.act('" + mid + "','done')\">Mark as done</button>");
                sb.Append("<button type=\"button\" class=\"am-btn am-btn-danger am-btn-sm\" onclick=\"AdminMessagesJs.act('" + mid + "','delete')\">Delete</button></div>");
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private static string MainHtml(SiteData site, Session s)
        {
            return InboxHtml(site, "open", string.Empty);
        }

        public async Task<ApiResponse> Show()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminMessagesJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            response.SetElementContents("am-main", InboxHtml(site, Filter(GetDataValue("filter")), Clip(GetDataValue("id"), 40)));
            if (GetDataValue("id") != string.Empty)
            {
                response.ExecuteScript("AdminMessagesJs.opened();");
            }
            return response;
        }

        public async Task<ApiResponse> Act()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminMessagesJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            string id = Clip(GetDataValue("id"), 40);
            string act = GetDataValue("act");
            Message? m = site.Messages.FirstOrDefault(x => x.Id == id);
            string next = id;
            if (m != null && (act == "done" || act == "open" || act == "delete"))
            {
                if (act == "delete")
                {
                    site.Messages.Remove(m);
                    next = string.Empty;
                }
                else
                {
                    m.Done = act == "done";
                }
                await SaveSite(site, s.Sbx, true);
            }
            string filter = Filter(GetDataValue("filter"));
            if (m != null && filter != "all" && act != "delete" && (filter == "done") != m.Done)
            {
                next = string.Empty;
            }
            response.SetElementContents("am-main", InboxHtml(site, filter, next));
            Chrome(response, site, s);
            response.ExecuteScript("AdminMessagesJs.toast('" + (act == "done" ? "Marked as done" : act == "open" ? "Marked as open" : "Deleted") + "');");
            return response;
        }

        private static readonly JsonSerializerOptions ReadOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions WriteOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);

        private static string Root => Directory.GetCurrentDirectory();

        private static string DataDir => Path.Combine(Root, "data");

        private static string Cfg(string key)
        {
            string file = Path.Combine(Root, "appConfig", "application.cfg");
            if (!File.Exists(file))
            {
                return string.Empty;
            }
            foreach (string line in File.ReadAllLines(file))
            {
                int i = line.IndexOf('|');
                if (i > 0 && line.Substring(0, i).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(i + 1).Trim();
                }
            }
            return string.Empty;
        }

        private static bool Demo => Cfg("app.app.runmode") != "1";

        private static string CleanId(string? value)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return Regex.IsMatch(v, "^[a-f0-9]{32}$") ? v : string.Empty;
        }

        private static string SandboxDir(string sbx)
        {
            return Path.Combine(Root, "temp", "sandbox", sbx);
        }

        private static string Sandbox(string? value)
        {
            string id = CleanId(value);
            return id != string.Empty && Demo && File.Exists(Path.Combine(SandboxDir(id), "site.json")) && Made(id) > DateTime.UtcNow.AddMinutes(-60) ? id : string.Empty;
        }

        private string SiteFile(string sbx)
        {
            return sbx == string.Empty ? Path.Combine(DataDir, "site.json") : Path.Combine(SandboxDir(sbx), "site.json");
        }

        private async Task<SiteData> LoadSite(string sbx = "")
        {
            string file = SiteFile(sbx);
            if (!File.Exists(file))
            {
                file = Path.Combine(Root, "data", "site.json");
            }
            if (!File.Exists(file))
            {
                return new SiteData();
            }
            string json = await File.ReadAllTextAsync(file);
            SiteData site = JsonSerializer.Deserialize<SiteData>(json, ReadOpts) ?? new SiteData();
            if (!string.IsNullOrWhiteSpace(site.Settings.Instagram) && !site.Settings.Social.Any(x => x.Key == "instagram"))
            {
                site.Settings.Social.Insert(0, new SocialLink { Key = "instagram", Url = "https://www.instagram.com/" + Uri.EscapeDataString(site.Settings.Instagram.Trim().TrimStart('@')) });
            }
            site.Settings.Instagram = null;
            return site;
        }

        private async Task SaveSite(SiteData site, string sbx, bool keepPrevious = true)
        {
            string file = SiteFile(sbx);
            await Gate.WaitAsync();
            try
            {
                if (keepPrevious && File.Exists(file))
                {
                    File.Copy(file, Path.Combine(Path.GetDirectoryName(file) ?? Root, "site.prev.json"), true);
                }
                string tmp = file + ".tmp";
                await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(site, WriteOpts));
                File.Move(tmp, file, true);
            }
            finally
            {
                Gate.Release();
            }
        }

        private string NewSandbox()
        {
            Sweep();
            string id = Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(SandboxDir(id));
            File.WriteAllText(Path.Combine(SandboxDir(id), "made"), DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            File.Copy(SiteFile(string.Empty), Path.Combine(SandboxDir(id), "site.json"));
            return id;
        }

        private static DateTime Made(string sbx)
        {
            string file = Path.Combine(SandboxDir(sbx), "made");
            return File.Exists(file) && long.TryParse(File.ReadAllText(file), NumberStyles.Integer, CultureInfo.InvariantCulture, out long t) ? new DateTime(t, DateTimeKind.Utc) : Directory.GetCreationTimeUtc(SandboxDir(sbx));
        }

        private static void Sweep()
        {
            DateTime limit = DateTime.UtcNow.AddMinutes(-60);
            string boxes = Path.Combine(Root, "temp", "sandbox");
            string uploads = Path.Combine(Root, "images", "uploads", "sandbox");
            try
            {
                if (Directory.Exists(boxes))
                {
                    foreach (string dir in Directory.GetDirectories(boxes))
                    {
                        if (Made(Path.GetFileName(dir)) < limit)
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                }
                if (Directory.Exists(uploads))
                {
                    foreach (string dir in Directory.GetDirectories(uploads))
                    {
                        if (!Directory.Exists(SandboxDir(Path.GetFileName(dir))))
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string Stamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string NiceDate(string iso)
        {
            return DateTime.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d) ? d.ToString("d MMMM yyyy", CultureInfo.InvariantCulture) : iso;
        }

        private static string Attr(string value)
        {
            return HtmlEncode(value ?? string.Empty);
        }

        private static string Clip(string? value, int max)
        {
            string v = (value ?? string.Empty).Trim();
            return v.Length > max ? v.Substring(0, max) : v;
        }

        private static string Paras(string text, string cls)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string p in (text ?? string.Empty).Replace("\r", string.Empty).Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
            {
                sb.Append("<p class=\"" + cls + "\">" + HtmlEncode(p.Trim()).Replace("\n", "<br>") + "</p>");
            }
            return sb.ToString();
        }

        private static bool SafeColor(string v)
        {
            return Regex.IsMatch(v ?? string.Empty, "^#[0-9a-fA-F]{6}$");
        }

        private static bool IsEmail(string v)
        {
            if (v.Length < 5 || v.Length > 80 || v.Contains(' '))
            {
                return false;
            }
            int at = v.IndexOf('@');
            int dot = v.LastIndexOf('.');
            return at > 0 && at == v.LastIndexOf('@') && dot > at + 1 && dot < v.Length - 1;
        }
    }
}
