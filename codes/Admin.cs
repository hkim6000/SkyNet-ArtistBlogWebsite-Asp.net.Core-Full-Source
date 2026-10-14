using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArtistBlog.Models;
using SkyNet;

namespace ArtistBlog.codes
{
    public class Admin : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await LoadSite();
            HtmlDoc.SetTitle("Studio admin | Dashboard");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--ad-accent:" + site.Settings.Accent : string.Empty);
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
            response.SetElementContents("ad-cnt-AdminComments", pc > 0 ? pc.ToString(CultureInfo.InvariantCulture) : string.Empty);
            response.SetElementContents("ad-cnt-AdminMessages", um > 0 ? um.ToString(CultureInfo.InvariantCulture) : string.Empty);
            int left = s.Sbx != string.Empty ? Math.Max(1, (int)Math.Ceiling((Made(s.Sbx).AddMinutes(60) - DateTime.UtcNow).TotalMinutes)) : 0;
            response.SetElementContents("ad-demo", s.Sbx != string.Empty ? "<span class=\"ad-dl\">Demo copy &middot; resets in </span><span class=\"ad-ds\">Demo &middot; </span>" + left + " min" : string.Empty);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            response.SetElementContents("ad-main", MainHtml(site, s));
            Chrome(response, site, s);
            response.ExecuteScript("AdminJs.loaded();");
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
            response.ExecuteScript("AdminJs.out();");
            return response;
        }

        private string MainHtml(SiteData site, Session s)
        {
            int pub = site.Posts.Count(p => p.Published);
            int drafts = site.Posts.Count - pub;
            int pc = site.Comments.Count(c => c.Status == "pending");
            int um = site.Messages.Count(m => !m.Done);
            string first = site.Settings.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "there";
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"ad-ptop\"><div><h1>Hello, " + HtmlEncode(first) + "</h1><p>Here is what is happening on " + HtmlEncode(site.Settings.Name) + ".</p></div><div class=\"ad-acts\"><a class=\"ad-btn ad-btn-ghost ad-btn-sm\" href=\"AdminPosts\">Write a post</a><a class=\"ad-btn ad-btn-sm\" href=\"AdminWorks\">Upload photos</a></div></div>");
            sb.Append("<div class=\"ad-stats\">");
            sb.Append("<a class=\"ad-stat\" href=\"AdminWorks\"><small>Works</small><b>" + site.Works.Count + "</b><span>" + site.Series.Count + " series</span></a>");
            sb.Append("<a class=\"ad-stat\" href=\"AdminPosts\"><small>Journal</small><b>" + pub + "</b><span>published &middot; " + drafts + (drafts == 1 ? " draft" : " drafts") + "</span></a>");
            sb.Append("<a class=\"ad-stat" + (pc > 0 ? " ad-warn" : string.Empty) + "\" href=\"AdminComments\"><small>Comments</small><b>" + pc + "</b><span>waiting for review</span></a>");
            sb.Append("<a class=\"ad-stat" + (um > 0 ? " ad-warn" : string.Empty) + "\" href=\"AdminMessages\"><small>Messages</small><b>" + um + "</b><span>unread</span></a>");
            sb.Append("</div><div class=\"ad-grid2\"><div class=\"ad-panel\"><div class=\"ad-ph\"><h2>Waiting for review</h2><a href=\"AdminComments\">All comments</a></div>");
            List<PostComment> pending = site.Comments.Where(c => c.Status == "pending").OrderByDescending(c => c.Date).Take(4).ToList();
            foreach (PostComment c in pending)
            {
                BlogPost? p = site.Posts.FirstOrDefault(x => x.Slug == c.Post);
                sb.Append("<div class=\"ad-row\"><span class=\"ad-dot\"></span><div><b>" + HtmlEncode(c.Name) + " on " + HtmlEncode(p != null ? p.Title : c.Post) + "</b><small>" + HtmlEncode(c.Text) + "</small></div></div>");
            }
            if (pending.Count == 0)
            {
                sb.Append("<p class=\"ad-none\">Nothing waiting.</p>");
            }
            sb.Append("</div><div class=\"ad-panel\"><div class=\"ad-ph\"><h2>Latest messages</h2><a href=\"AdminMessages\">Inbox</a></div>");
            foreach (Message m in site.Messages.Take(4))
            {
                sb.Append("<div class=\"ad-row\">" + (m.Done ? string.Empty : "<span class=\"ad-dot\"></span>") + "<div><b>" + HtmlEncode(m.Name) + " &middot; " + HtmlEncode(m.Topic) + "</b><small>" + HtmlEncode(m.Text) + "</small></div><span class=\"ad-st\">" + HtmlEncode(NiceDate(m.Date)) + "</span></div>");
            }
            if (site.Messages.Count == 0)
            {
                sb.Append("<p class=\"ad-none\">No messages yet.</p>");
            }
            sb.Append("</div></div><div class=\"ad-grid2\"><div class=\"ad-panel\"><h2>Your site</h2><div class=\"ad-links\"><a href=\"AdminLayout\">Pages<small>Arrange the home and about pages</small></a><a href=\"AdminSettings\">Settings<small>Name, colour, theme, contact</small></a><a href=\"Home\" target=\"_blank\" rel=\"noopener\">View site<small>Opens in a new tab</small></a></div></div>");
            bool prev = File.Exists(Path.Combine(Path.GetDirectoryName(SiteFile(s.Sbx)) ?? Root, "site.prev.json"));
            sb.Append("<div class=\"ad-panel\"><h2>Versions</h2><p>Every save keeps the version before it. Restore it if something went wrong.</p><div class=\"ad-ver\"><button type=\"button\" class=\"ad-btn ad-btn-ghost ad-btn-sm\" onclick=\"AdminJs.undo()\"" + (prev ? string.Empty : " disabled") + ">Restore last version</button>");
            if (s.Sbx != string.Empty)
            {
                sb.Append("<button type=\"button\" class=\"ad-btn ad-btn-ghost ad-btn-sm\" onclick=\"AdminJs.reset()\">Reset demo copy</button>");
            }
            sb.Append("</div></div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Undo()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            string file = SiteFile(s.Sbx);
            string prev = Path.Combine(Path.GetDirectoryName(file) ?? Root, "site.prev.json");
            if (!File.Exists(prev))
            {
                response.ExecuteScript("AdminJs.toast('There is no earlier version');");
                return response;
            }
            SiteData old = JsonSerializer.Deserialize<SiteData>(await File.ReadAllTextAsync(prev), ReadOpts) ?? site;
            await SaveSite(old, s.Sbx, true);
            response.SetElementContents("ad-main", MainHtml(old, s));
            Chrome(response, old, s);
            response.ExecuteScript("AdminJs.toast('Previous version restored');");
            return response;
        }

        public async Task<ApiResponse> Reset()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            if (s.Sbx == string.Empty)
            {
                return response;
            }
            SiteData fresh = JsonSerializer.Deserialize<SiteData>(await File.ReadAllTextAsync(Path.Combine(DataDir, "site.json")), ReadOpts) ?? site;
            await SaveSite(fresh, s.Sbx, true);
            response.SetElementContents("ad-main", MainHtml(fresh, s));
            Chrome(response, fresh, s);
            response.ExecuteScript("AdminJs.toast('Demo copy reset');");
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
