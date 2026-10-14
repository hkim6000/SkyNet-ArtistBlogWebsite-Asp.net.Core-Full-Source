using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArtistBlog.Models;
using SkyNet;

namespace ArtistBlog.codes
{
    public class AdminPosts : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await LoadSite();
            HtmlDoc.SetTitle("Studio admin | Journal");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--ap-accent:" + site.Settings.Accent : string.Empty);
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
            response.SetElementContents("ap-cnt-AdminComments", pc > 0 ? pc.ToString(CultureInfo.InvariantCulture) : string.Empty);
            response.SetElementContents("ap-cnt-AdminMessages", um > 0 ? um.ToString(CultureInfo.InvariantCulture) : string.Empty);
            int left = s.Sbx != string.Empty ? Math.Max(1, (int)Math.Ceiling((Made(s.Sbx).AddMinutes(60) - DateTime.UtcNow).TotalMinutes)) : 0;
            response.SetElementContents("ap-demo", s.Sbx != string.Empty ? "<span class=\"ap-dl\">Demo copy &middot; resets in </span><span class=\"ap-ds\">Demo &middot; </span>" + left + " min" : string.Empty);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminPostsJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            response.SetElementContents("ap-main", MainHtml(site, s));
            Chrome(response, site, s);
            response.ExecuteScript("AdminPostsJs.loaded();");
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
            response.ExecuteScript("AdminPostsJs.out();");
            return response;
        }

        private static string Picker(SiteData site)
        {
            StringBuilder sb = new StringBuilder("<h2>Choose a photo</h2><div class=\"ap-pick\">");
            foreach (Work w in site.Works)
            {
                sb.Append("<button type=\"button\" onclick=\"AdminPostsJs.pick('" + Attr(w.Full) + "','" + Attr(w.Title.Replace("'", " ")) + "')\"><img src=\"" + Attr(w.Thumb) + "\" alt=\"\" loading=\"lazy\"><span>" + HtmlEncode(w.Title) + "</span></button>");
            }
            foreach (string k in new[] { "hero", "portrait", "studio" })
            {
                sb.Append("<button type=\"button\" onclick=\"AdminPostsJs.pick('images/site/" + k + ".webp','')\"><img src=\"images/site/" + k + "-thumb.webp\" alt=\"\" loading=\"lazy\"><span>Site: " + k + "</span></button>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static bool SafeImage(string src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(webp|jpg|jpeg|png)$") && !src!.Contains("..");
        }

        private static string MainHtml(SiteData site, Session s)
        {
            int pub = site.Posts.Count(p => p.Published);
            StringBuilder sb = new StringBuilder("<div class=\"ap-ptop\"><div><h1>Journal</h1><p>" + pub + " published, " + (site.Posts.Count - pub) + " drafts.</p></div><div class=\"ap-acts\"><button type=\"button\" class=\"ap-btn ap-btn-sm\" onclick=\"AdminPostsJs.edit('')\">New post</button></div></div>");
            sb.Append("<div class=\"ap-tools\"><div class=\"ap-chips\"><button type=\"button\" class=\"ap-chip ap-act\" data-f=\"all\" onclick=\"AdminPostsJs.show('all')\">All</button><button type=\"button\" class=\"ap-chip\" data-f=\"live\" onclick=\"AdminPostsJs.show('live')\">Published</button><button type=\"button\" class=\"ap-chip\" data-f=\"draft\" onclick=\"AdminPostsJs.show('draft')\">Drafts</button></div><input type=\"search\" class=\"ap-find\" id=\"ap-find\" placeholder=\"Find a post\" oninput=\"AdminPostsJs.list()\" aria-label=\"Find a post\"></div>");
            sb.Append("<div class=\"ap-panel ap-pl\">");
            foreach (BlogPost p in site.Posts.OrderByDescending(p => p.Date))
            {
                int cc = site.Comments.Count(c => c.Post == p.Slug && c.Status == "approved");
                string t = Attr(p.Title.Replace("'", " "));
                sb.Append("<div class=\"ap-row\" data-st=\"" + (p.Published ? "live" : "draft") + "\" data-t=\"" + Attr(p.Title.ToLowerInvariant()) + "\">" + (p.Cover != string.Empty ? "<img src=\"" + Attr(p.Cover) + "\" alt=\"\" loading=\"lazy\">" : "<img src=\"images/site/studio-thumb.webp\" alt=\"\" loading=\"lazy\">"));
                sb.Append("<div><b>" + HtmlEncode(p.Title) + "</b><small>" + HtmlEncode(NiceDate(p.Date)) + " &middot; " + HtmlEncode(string.Join(", ", p.Tags)) + "</small></div>");
                sb.Append("<div class=\"ap-meta\"><span class=\"ap-st" + (p.Published ? " ap-live" : string.Empty) + "\">" + (p.Published ? "Published" : "Draft") + "</span><span>" + p.Likes + " likes</span><span>" + cc + (cc == 1 ? " comment" : " comments") + "</span></div>");
                sb.Append("<div class=\"ap-acts\"><button type=\"button\" class=\"ap-btn ap-btn-ghost ap-btn-sm\" onclick=\"AdminPostsJs.edit('" + Attr(p.Slug) + "')\">Edit</button><button type=\"button\" class=\"ap-btn ap-btn-danger ap-btn-sm\" onclick=\"AdminPostsJs.remove('" + Attr(p.Slug) + "','" + t + "')\">Delete</button></div></div>");
            }
            if (site.Posts.Count == 0)
            {
                sb.Append("<p class=\"ap-none\">No posts yet. Write your first one.</p>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string EditorHtml(SiteData site, BlogPost p, bool isNew)
        {
            StringBuilder sb = new StringBuilder("<div class=\"ap-ptop\"><div><button type=\"button\" class=\"ap-btn ap-btn-ghost ap-btn-sm\" onclick=\"AdminPostsJs.back()\">&larr; All posts</button></div><div class=\"ap-acts\"><a class=\"ap-btn ap-btn-ghost ap-btn-sm\" id=\"ap-viewlink\" href=\"Post?p=" + Uri.EscapeDataString(p.Slug) + "\" target=\"_blank\" rel=\"noopener\"" + (p.Published && !isNew ? string.Empty : " style=\"display:none\"") + ">View</a><button type=\"button\" class=\"ap-btn ap-btn-sm\" onclick=\"AdminPostsJs.save()\">Save</button></div></div>");
            sb.Append("<input type=\"hidden\" id=\"ap-orig\" value=\"" + (isNew ? string.Empty : Attr(p.Slug)) + "\"><input type=\"hidden\" id=\"ap-cover\" value=\"" + Attr(p.Cover) + "\">");
            sb.Append("<div class=\"ap-ed\"><div><input type=\"text\" class=\"ap-ttl\" id=\"ap-title\" maxlength=\"120\" placeholder=\"Post title\" value=\"" + Attr(p.Title) + "\" oninput=\"AdminPostsJs.slugify()\" aria-label=\"Title\"><em class=\"ap-err\" id=\"ap-e-title\"></em>");
            sb.Append("<div class=\"ap-bar\" role=\"toolbar\" aria-label=\"Formatting\" onmousedown=\"if(event.target.closest('button'))event.preventDefault()\">");
            sb.Append("<button type=\"button\" onclick=\"AdminPostsJs.cmd('bold')\" aria-label=\"Bold\"><b>B</b></button><button type=\"button\" onclick=\"AdminPostsJs.cmd('italic')\" aria-label=\"Italic\"><em>I</em></button><i></i>");
            sb.Append("<button type=\"button\" onclick=\"AdminPostsJs.cmd('formatBlock','<h2>')\">H2</button><button type=\"button\" onclick=\"AdminPostsJs.cmd('formatBlock','<h3>')\">H3</button><button type=\"button\" onclick=\"AdminPostsJs.cmd('formatBlock','<p>')\">&para;</button><button type=\"button\" onclick=\"AdminPostsJs.cmd('formatBlock','<blockquote>')\" aria-label=\"Quote\">&ldquo;&rdquo;</button><i></i>");
            sb.Append("<button type=\"button\" onclick=\"AdminPostsJs.cmd('insertUnorderedList')\" aria-label=\"Bulleted list\">&bull; List</button><button type=\"button\" onclick=\"AdminPostsJs.cmd('insertOrderedList')\" aria-label=\"Numbered list\">1. List</button><i></i>");
            sb.Append("<button type=\"button\" onclick=\"AdminPostsJs.cmd('link')\">Link</button><button type=\"button\" onclick=\"AdminPostsJs.picker('body')\">Photo</button><button type=\"button\" onclick=\"AdminPostsJs.cmd('clear')\">Clear</button></div>");
            sb.Append("<div class=\"ap-body\" id=\"ap-body\" contenteditable=\"true\" role=\"textbox\" aria-multiline=\"true\" aria-label=\"Post text\">" + (p.Body != string.Empty ? p.Body : "<p><br></p>") + "</div><em class=\"ap-err\" id=\"ap-e-body\"></em></div>");
            sb.Append("<div class=\"ap-side2\"><div class=\"ap-panel\"><label class=\"ap-tog\" for=\"ap-pub\">Published<input type=\"checkbox\" id=\"ap-pub\"" + (p.Published ? " checked" : string.Empty) + "></label><p class=\"ap-hint\">Drafts are only visible here.</p></div>");
            sb.Append("<div class=\"ap-panel\"><div class=\"ap-form2\"><div class=\"ap-field\"><span>Cover photo</span><div class=\"ap-cov\" id=\"ap-cov\">" + (p.Cover != string.Empty ? "<img src=\"" + Attr(p.Cover) + "\" alt=\"\">" : "No cover yet") + "</div><button type=\"button\" class=\"ap-btn ap-btn-ghost ap-btn-sm\" onclick=\"AdminPostsJs.picker('cover')\">Choose cover</button><em class=\"ap-err\" id=\"ap-e-cover\"></em></div>");
            sb.Append("<label class=\"ap-field\" for=\"ap-excerpt\"><span>Excerpt</span><textarea id=\"ap-excerpt\" rows=\"3\" maxlength=\"200\">" + HtmlEncode(p.Excerpt) + "</textarea><em class=\"ap-err\" id=\"ap-e-excerpt\"></em></label>");
            sb.Append("<label class=\"ap-field\" for=\"ap-date\"><span>Date</span><input type=\"date\" id=\"ap-date\" value=\"" + Attr(p.Date) + "\"><em class=\"ap-err\" id=\"ap-e-date\"></em></label>");
            sb.Append("<label class=\"ap-field\" for=\"ap-tags\"><span>Tags</span><input type=\"text\" id=\"ap-tags\" maxlength=\"120\" value=\"" + Attr(string.Join(", ", p.Tags)) + "\"><small class=\"ap-hint\">Separate with commas</small><em class=\"ap-err\" id=\"ap-e-tags\"></em></label>");
            sb.Append("<label class=\"ap-field\" for=\"ap-slug\"><span>Web address</span><input type=\"text\" id=\"ap-slug\" maxlength=\"80\" value=\"" + Attr(isNew ? string.Empty : p.Slug) + "\" oninput=\"this.setAttribute('data-touched','1')\"><small class=\"ap-hint\">Post?p=&hellip; lower case and dashes</small><em class=\"ap-err\" id=\"ap-e-slug\"></em></label></div></div></div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Edit()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminPostsJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            string slug = Clip(GetDataValue("slug"), 120);
            BlogPost? p = site.Posts.FirstOrDefault(x => x.Slug == slug);
            bool isNew = p == null;
            p ??= new BlogPost { Date = Stamp() };
            response.SetElementContents("ap-main", EditorHtml(site, p, isNew));
            response.SetElementContents("ap-mbody", Picker(site));
            response.ExecuteScript("AdminPostsJs.loaded();window.scrollTo(0,0);" + (isNew ? "document.getElementById('ap-title').focus();" : string.Empty));
            return response;
        }

        private static readonly HashSet<string> Allowed = new HashSet<string> { "p", "h2", "h3", "strong", "em", "a", "ul", "ol", "li", "blockquote", "img", "figure", "figcaption", "br" };

        private static string AttrValue(string attrs, string name)
        {
            Match m = Regex.Match(attrs, "\\b" + name + "\\s*=\\s*(\"([^\"]*)\"|'([^']*)'|([^\\s>]+))", RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                return string.Empty;
            }
            string v = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value;
            return System.Net.WebUtility.HtmlDecode(v).Trim();
        }

        private static bool SafeHref(string h)
        {
            return Regex.IsMatch(h, "^(https?://[^\\s\"'<>]+|mailto:[^\\s\"'<>]+|[A-Za-z][A-Za-z0-9]*(\\?[A-Za-z0-9=&%._-]*)?)$");
        }

        private static string Plain(string t)
        {
            return HtmlEncode(System.Net.WebUtility.HtmlDecode(t));
        }

        private static string Sanitize(string html)
        {
            string s = Regex.Replace(html ?? string.Empty, "<(script|style|iframe|object|embed|svg|math|template|textarea|select)\\b[^>]*>[\\s\\S]*?</\\1\\s*>", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, "<!--[\\s\\S]*?-->", string.Empty);
            StringBuilder sb = new StringBuilder();
            List<string> open = new List<string>();
            int pos = 0;
            foreach (Match m in Regex.Matches(s, "<(/?)([a-zA-Z][a-zA-Z0-9]*)\\b([^>]*)>"))
            {
                sb.Append(Plain(s.Substring(pos, m.Index - pos)));
                pos = m.Index + m.Length;
                string name = m.Groups[2].Value.ToLowerInvariant();
                name = name switch { "b" => "strong", "i" => "em", "div" => "p", "h1" => "h2", "h4" or "h5" or "h6" => "h3", _ => name };
                if (!Allowed.Contains(name))
                {
                    continue;
                }
                bool close = m.Groups[1].Value == "/";
                if (name == "br")
                {
                    sb.Append(close ? string.Empty : "<br>");
                    continue;
                }
                if (name == "img")
                {
                    string src = AttrValue(m.Groups[3].Value, "src");
                    if (!close && SafeImage(src))
                    {
                        sb.Append("<img src=\"" + Attr(src) + "\" alt=\"" + Attr(AttrValue(m.Groups[3].Value, "alt")) + "\">");
                    }
                    continue;
                }
                if (close)
                {
                    int at = open.LastIndexOf(name);
                    if (at >= 0)
                    {
                        for (int i = open.Count - 1; i >= at; i--)
                        {
                            sb.Append("</" + open[i] + ">");
                        }
                        open.RemoveRange(at, open.Count - at);
                    }
                    continue;
                }
                if ((name == "p" || name == "h2" || name == "h3" || name == "figure") && open.Count > 0 && (open[^1] == "p" || open[^1] == "h2" || open[^1] == "h3"))
                {
                    sb.Append("</" + open[^1] + ">");
                    open.RemoveAt(open.Count - 1);
                }
                if (name == "a")
                {
                    string href = AttrValue(m.Groups[3].Value, "href");
                    if (!SafeHref(href))
                    {
                        continue;
                    }
                    sb.Append("<a href=\"" + Attr(href) + "\"" + (href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">");
                    open.Add("a");
                    continue;
                }
                sb.Append("<" + name + ">");
                open.Add(name);
            }
            sb.Append(Plain(s.Substring(pos)));
            for (int i = open.Count - 1; i >= 0; i--)
            {
                sb.Append("</" + open[i] + ">");
            }
            string r = sb.ToString();
            string before;
            do
            {
                before = r;
                r = Regex.Replace(r, "<(p|h2|h3|strong|em|blockquote|li|ul|ol|figcaption|a)>(\\s|&#160;|<br>)*</\\1>", string.Empty);
            }
            while (r != before);
            return r.Trim();
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminPostsJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            string orig = Clip(GetDataValue("orig"), 120);
            string title = Clip(GetDataValue("title"), 200);
            string slug = Regex.Replace(Clip(GetDataValue("slug"), 120).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (slug == string.Empty)
            {
                slug = Regex.Replace(title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            }
            slug = Clip(slug, 80).Trim('-');
            string date = Clip(GetDataValue("date"), 10);
            List<string> tags = GetDataValue("tags").Split(',').Select(t => Clip(t, 24).ToLowerInvariant()).Where(t => t != string.Empty).Distinct().Take(6).ToList();
            string excerpt = Clip(GetDataValue("excerpt"), 400);
            string cover = Clip(GetDataValue("cover"), 200);
            string body = Sanitize(GetDataValue("body"));
            bool live = GetDataValue("published") == "1";
            BlogPost? cur = site.Posts.FirstOrDefault(x => x.Slug == orig && orig != string.Empty);
            string eTitle = title.Length < 3 || title.Length > 120 ? "Use 3 to 120 characters." : string.Empty;
            string eSlug = slug.Length < 3 ? "Use at least 3 letters or numbers." : site.Posts.Any(x => x.Slug == slug && x != cur) ? "Another post already uses this address." : string.Empty;
            string eDate = !DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? "Choose a date." : string.Empty;
            string eExcerpt = excerpt.Length > 200 ? "Keep it under 200 characters." : live && excerpt.Length < 10 ? "Write a short excerpt for the journal list." : string.Empty;
            string eCover = cover != string.Empty && !SafeImage(cover) ? "Choose a photo from the library." : live && cover == string.Empty ? "Choose a cover photo." : string.Empty;
            string eBody = live && Regex.Replace(body, "<[^>]+>", string.Empty).Trim().Length < 20 ? "Write a little more before publishing." : body.Length > 60000 ? "This post is too long." : string.Empty;
            response.SetElementContents("ap-e-title", eTitle);
            response.SetElementContents("ap-e-slug", eSlug);
            response.SetElementContents("ap-e-date", eDate);
            response.SetElementContents("ap-e-excerpt", eExcerpt);
            response.SetElementContents("ap-e-cover", eCover);
            response.SetElementContents("ap-e-body", eBody);
            if (eTitle + eSlug + eDate + eExcerpt + eCover + eBody != string.Empty)
            {
                response.ExecuteScript("AdminPostsJs.toast('Please check the highlighted fields');");
                return response;
            }
            if (cur == null)
            {
                if (site.Posts.Count >= 300)
                {
                    response.ExecuteScript("AdminPostsJs.toast('The journal is full');");
                    return response;
                }
                cur = new BlogPost();
                site.Posts.Add(cur);
            }
            else if (cur.Slug != slug)
            {
                foreach (PostComment c in site.Comments.Where(c => c.Post == cur.Slug))
                {
                    c.Post = slug;
                }
            }
            cur.Slug = slug;
            cur.Title = title;
            cur.Date = date;
            cur.Tags = tags;
            cur.Excerpt = excerpt;
            cur.Cover = cover;
            cur.Body = body;
            cur.Published = live;
            await SaveSite(site, s.Sbx, true);
            response.SetElementContents("ap-body", body != string.Empty ? body : "<p><br></p>");
            response.ExecuteScript("AdminPostsJs.saved('" + slug + "'," + (live ? "true" : "false") + ");");
            return response;
        }

        public async Task<ApiResponse> Delete()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminPostsJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            string slug = Clip(GetDataValue("slug"), 120);
            if (site.Posts.RemoveAll(p => p.Slug == slug) > 0)
            {
                site.Comments.RemoveAll(c => c.Post == slug);
                await SaveSite(site, s.Sbx, true);
            }
            response.SetElementContents("ap-main", MainHtml(site, s));
            Chrome(response, site, s);
            response.ExecuteScript("AdminPostsJs.toast('Post deleted');");
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
