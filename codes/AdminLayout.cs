using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArtistBlog.Models;
using SkyNet;

namespace ArtistBlog.codes
{
    public class AdminLayout : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await LoadSite();
            HtmlDoc.SetTitle("Studio admin | Pages");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--ay-accent:" + site.Settings.Accent : string.Empty);
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
            response.SetElementContents("ay-cnt-AdminComments", pc > 0 ? pc.ToString(CultureInfo.InvariantCulture) : string.Empty);
            response.SetElementContents("ay-cnt-AdminMessages", um > 0 ? um.ToString(CultureInfo.InvariantCulture) : string.Empty);
            int left = s.Sbx != string.Empty ? Math.Max(1, (int)Math.Ceiling((Made(s.Sbx).AddMinutes(60) - DateTime.UtcNow).TotalMinutes)) : 0;
            response.SetElementContents("ay-demo", s.Sbx != string.Empty ? "<span class=\"ay-dl\">Demo copy &middot; resets in </span><span class=\"ay-ds\">Demo &middot; </span>" + left + " min" : string.Empty);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminLayoutJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            response.SetElementContents("ay-main", MainHtml(site, s));
            Chrome(response, site, s);
            response.ExecuteScript("AdminLayoutJs.loaded();");
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
            response.ExecuteScript("AdminLayoutJs.out();");
            return response;
        }

        private static string Picker(SiteData site)
        {
            StringBuilder sb = new StringBuilder("<h2>Choose a photo</h2><div class=\"ay-pick\">");
            foreach (Work w in site.Works)
            {
                sb.Append("<button type=\"button\" onclick=\"AdminLayoutJs.pick('" + Attr(w.Full) + "','" + Attr(w.Title.Replace("'", " ")) + "')\"><img src=\"" + Attr(w.Thumb) + "\" alt=\"\" loading=\"lazy\"><span>" + HtmlEncode(w.Title) + "</span></button>");
            }
            foreach (string k in new[] { "hero", "portrait", "studio" })
            {
                sb.Append("<button type=\"button\" onclick=\"AdminLayoutJs.pick('images/site/" + k + ".webp','')\"><img src=\"images/site/" + k + "-thumb.webp\" alt=\"\" loading=\"lazy\"><span>Site: " + k + "</span></button>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static bool SafeImage(string src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(webp|jpg|jpeg|png)$") && !src!.Contains("..");
        }

        private static readonly (string Key, string Name)[] Networks = { ("instagram", "Instagram"), ("youtube", "YouTube"), ("x", "X"), ("facebook", "Facebook"), ("linkedin", "LinkedIn"), ("tiktok", "TikTok"), ("pinterest", "Pinterest"), ("threads", "Threads"), ("behance", "Behance"), ("flickr", "Flickr"), ("vimeo", "Vimeo") };

        private static string Icon(string key)
        {
            return key switch
            {
                "instagram" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"3.5\" width=\"17\" height=\"17\" rx=\"5\"/><circle cx=\"12\" cy=\"12\" r=\"4\"/><circle cx=\"17.2\" cy=\"6.8\" r=\"0.6\" fill=\"currentColor\"/></svg>",
                "youtube" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"currentColor\" aria-hidden=\"true\"><path fill-rule=\"evenodd\" d=\"M4.6 5.6C7 5.2 9.5 5 12 5s5 .2 7.4.6c1.2.2 2.1 1.1 2.3 2.3.2 1.4.3 2.7.3 4.1s-.1 2.7-.3 4.1c-.2 1.2-1.1 2.1-2.3 2.3-2.4.4-4.9.6-7.4.6s-5-.2-7.4-.6c-1.2-.2-2.1-1.1-2.3-2.3C2.1 14.7 2 13.4 2 12s.1-2.7.3-4.1c.2-1.2 1.1-2.1 2.3-2.3zM10 9v6l5.2-3z\"/></svg>",
                "x" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 4l16 16M20 4l-6.4 6.4M10.4 13.6L4 20\" stroke-width=\"2\"/></svg>",
                "facebook" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"3.5\" width=\"17\" height=\"17\" rx=\"4\"/><path d=\"M15.5 8h-1.6c-1.1 0-1.9.9-1.9 2V20.5M9.5 12.5h5.5\"/></svg>",
                "linkedin" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><rect x=\"3.5\" y=\"3.5\" width=\"17\" height=\"17\" rx=\"3\"/><path d=\"M8 10.5V16.5M12 16.5V10.5M12 13.3c0-1.6 1-2.8 2.4-2.8s2.1 1 2.1 2.6v3.4\"/><circle cx=\"8\" cy=\"7.6\" r=\"0.6\" fill=\"currentColor\"/></svg>",
                "tiktok" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M13 4v11a3.5 3.5 0 1 1-3.5-3.5M13 4c.4 2.6 2.2 4.3 5 4.5\"/></svg>",
                "pinterest" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M11 20.3l1.8-7.6M10.2 13.6c-.9-2.6.6-5.6 3.2-5.6 2 0 3.1 1.3 3.1 3 0 2.6-1.6 4.4-3.2 4.4-1 0-1.6-.6-1.4-1.5\"/></svg>",
                "threads" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M16.6 10.8c-.6-2.6-2.4-3.8-4.6-3.8-3 0-5 2.3-5 5.3 0 3.2 2 5.5 5.2 5.5 2.6 0 4.6-1.5 4.6-3.6 0-2-1.8-3-4-3-1.8 0-3 .8-3 2 0 1 .8 1.8 2.1 1.8 2.1 0 3-1.9 2.8-4.6\"/><path d=\"M19.5 7.5C18 4.8 15.4 3.5 12.2 3.5 7.1 3.5 4 7.2 4 12.2s3.1 8.3 8.2 8.3c3.4 0 6-1.6 7.2-4.3\"/></svg>",
                "behance" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M3 6.5h4.5a2.5 2.5 0 0 1 0 5H3zM3 11.5h5.2a2.8 2.8 0 0 1 0 5.6H3zM3 6.5v10.6\"/><path d=\"M14 13.5h7c0-2.3-1.5-4-3.5-4s-3.5 1.7-3.5 3.9 1.5 3.9 3.6 3.9c1.3 0 2.4-.6 3-1.6M15 6.8h5\"/></svg>",
                "flickr" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" aria-hidden=\"true\"><circle cx=\"7.2\" cy=\"12\" r=\"4\" fill=\"currentColor\"/><circle cx=\"16.8\" cy=\"12\" r=\"3.3\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\"/></svg>",
                "vimeo" => "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M3.5 8.5l1.5-1c1-.7 1.8-.4 2.2.8l1.9 7c.4 1.3 1 1.6 1.9.6 2.2-2.4 4.9-6.6 5.2-8.7.3-2.2-1.6-2.8-3.6-1.7 1.4-3 6.2-4.3 6.3-.2.1 3.3-3.5 9.3-7.2 12.5-1.5 1.3-3 1.2-3.7-.9L6 9.6\"/></svg>",
                _ => string.Empty
            };
        }

        private static bool SafeUrl(string url)
        {
            return url.Length <= 200 && Regex.IsMatch(url, "^https://[A-Za-z0-9.-]+\\.[A-Za-z]{2,}(/[^\\s\"'<>]*)?$");
        }

        private static string Handle(string url)
        {
            string path = Regex.Replace(url, "^https://[^/]+", string.Empty).Trim('/');
            string last = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
            last = Uri.UnescapeDataString(last).TrimStart('@');
            return last == string.Empty ? Regex.Replace(url, "^https://(www\\.)?", string.Empty) : "@" + last;
        }

        private static readonly string[] Types = { "hero", "text", "image", "quote", "split", "works", "latest", "social", "contact" };

        private static string Label(string t)
        {
            return t switch
            {
                "hero" => "Hero photo",
                "text" => "Text",
                "image" => "Wide photo",
                "quote" => "Quote",
                "split" => "Photo and text",
                "works" => "Selected works",
                "latest" => "Latest posts",
                "social" => "Follow links",
                _ => "Contact call"
            };
        }

        private static string Ce(string field, string value, string cls, string ph)
        {
            return "<div class=\"ay-ce " + cls + "\" contenteditable=\"plaintext-only\" data-f=\"" + field + "\" data-ph=\"" + Attr(ph) + "\" role=\"textbox\" aria-label=\"" + Attr(ph) + "\">" + HtmlEncode(value) + "</div>";
        }

        private static string Count(string value, int min, int max, string noun)
        {
            int n = int.TryParse(value, out int v) ? Math.Clamp(v, min, max) : min;
            StringBuilder sb = new StringBuilder("<label class=\"ay-cnt-sel\">Show <select data-f=\"text\" onchange=\"AdminLayoutJs.touch()\">");
            for (int i = min; i <= max; i++)
            {
                sb.Append("<option value=\"" + i + "\"" + (i == n ? " selected" : string.Empty) + ">" + i + "</option>");
            }
            sb.Append("</select> " + noun + "</label>");
            return sb.ToString();
        }

        private static string BlockHtml(Block b, SiteData site)
        {
            bool img = b.Type == "hero" || b.Type == "image" || b.Type == "split";
            StringBuilder sb = new StringBuilder("<section class=\"ay-blk ay-t-" + b.Type + "\" data-id=\"" + Attr(b.Id) + "\" data-type=\"" + b.Type + "\" data-image=\"" + Attr(b.Image) + "\" data-flip=\"" + (b.Flip ? "1" : "0") + "\">");
            sb.Append("<div class=\"ay-btool\"><button type=\"button\" class=\"ay-grip\" aria-label=\"Drag to move\" onpointerdown=\"AdminLayoutJs.grab(event,this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"currentColor\" aria-hidden=\"true\"><circle cx=\"9\" cy=\"6\" r=\"1.6\"/><circle cx=\"15\" cy=\"6\" r=\"1.6\"/><circle cx=\"9\" cy=\"12\" r=\"1.6\"/><circle cx=\"15\" cy=\"12\" r=\"1.6\"/><circle cx=\"9\" cy=\"18\" r=\"1.6\"/><circle cx=\"15\" cy=\"18\" r=\"1.6\"/></svg></button><span class=\"ay-bname\">" + Label(b.Type) + "</span>");
            if (img)
            {
                sb.Append("<button type=\"button\" onclick=\"AdminLayoutJs.photo(this)\">Photo</button>");
            }
            if (b.Type == "split")
            {
                sb.Append("<button type=\"button\" onclick=\"AdminLayoutJs.flip(this)\">Flip</button>");
            }
            sb.Append("<button type=\"button\" aria-label=\"Move up\" onclick=\"AdminLayoutJs.move(this,-1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 15l6-6 6 6\"/></svg></button><button type=\"button\" aria-label=\"Move down\" onclick=\"AdminLayoutJs.move(this,1)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M6 9l6 6 6-6\"/></svg></button><button type=\"button\" class=\"ay-del\" aria-label=\"Remove block\" onclick=\"AdminLayoutJs.drop(this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3\"/></svg><span class=\"ay-tl\">Remove</span></button></div><div class=\"ay-bview\">");
            switch (b.Type)
            {
                case "hero":
                    sb.Append("<div class=\"ay-vhero\"><img data-main src=\"" + Attr(b.Image) + "\" alt=\"\"><div>" + Ce("title", b.Title, "ay-h1", "Headline") + Ce("text", b.Text, "ay-sub", "Short line under the headline") + "</div></div>");
                    break;
                case "text":
                    sb.Append(Ce("title", b.Title, "ay-h2", "Heading (optional)") + Ce("text", b.Text, "ay-p", "Write here. Leave an empty line between paragraphs."));
                    break;
                case "image":
                    sb.Append("<img class=\"ay-bimg\" data-main src=\"" + Attr(b.Image) + "\" alt=\"\">" + Ce("text", b.Text, "ay-cap", "Caption (optional)"));
                    break;
                case "quote":
                    sb.Append(Ce("text", b.Text, "ay-q", "A line worth repeating") + Ce("title", b.Title, "ay-cite", "Who said it"));
                    break;
                case "split":
                    sb.Append("<div class=\"ay-vsplit" + (b.Flip ? " ay-flip" : string.Empty) + "\"><div><img data-main src=\"" + Attr(b.Image) + "\" alt=\"\"></div><div>" + Ce("title", b.Title, "ay-h2", "Heading") + Ce("text", b.Text, "ay-p", "Write here. Leave an empty line between paragraphs.") + "</div></div>");
                    break;
                case "works":
                    string src = Source(b.Source, site);
                    sb.Append(Ce("title", b.Title, "ay-h2", "Heading") + "<div class=\"ay-wopts\"><label class=\"ay-cnt-sel\">From <select data-f=\"source\" onchange=\"AdminLayoutJs.strip(this)\"><option value=\"featured\"" + (src == "featured" ? " selected" : string.Empty) + ">Featured photos</option><option value=\"newest\"" + (src == "newest" ? " selected" : string.Empty) + ">Newest photos</option>");
                    foreach (Series x in site.Series)
                    {
                        sb.Append("<option value=\"series:" + Attr(x.Key) + "\"" + (src == "series:" + x.Key ? " selected" : string.Empty) + ">Series: " + HtmlEncode(x.Title) + "</option>");
                    }
                    sb.Append("</select></label>" + Count(b.Text, 3, 24, "photographs").Replace("onchange=\"AdminLayoutJs.touch()\"", "onchange=\"AdminLayoutJs.strip(this)\"") + "</div><div class=\"ay-strip\">");
                    int pos = 0;
                    foreach (Work w in site.Works)
                    {
                        sb.Append("<img src=\"" + Attr(w.Thumb) + "\" alt=\"\" data-i=\"" + pos++ + "\" data-ft=\"" + (w.Featured ? "1" : "0") + "\" data-s=\"" + Attr(w.Series) + "\" data-y=\"" + w.Year + "\">");
                    }
                    sb.Append("</div><small class=\"ay-hint\">Star photos in Works to feature them. The preview shows the first six.</small>");
                    break;
                case "latest":
                    sb.Append(Ce("title", b.Title, "ay-h2", "Heading") + Count(b.Text, 1, 9, "posts") + "<div class=\"ay-cards\">");
                    foreach (BlogPost p in site.Posts.Where(p => p.Published).OrderByDescending(p => p.Date).Take(3))
                    {
                        sb.Append("<div><img src=\"" + Attr(p.Cover) + "\" alt=\"\" loading=\"lazy\">" + HtmlEncode(p.Title) + "</div>");
                    }
                    sb.Append("</div>");
                    break;
                case "social":
                    sb.Append("<div class=\"ay-vcta\">" + Ce("title", b.Title, "ay-h2", "Heading") + Ce("text", b.Text, "ay-p", "One line about what you share") + "<div class=\"ay-vsoc\">");
                    int shown = 0;
                    foreach ((string k, string name) in Networks)
                    {
                        if (site.Settings.Social.Any(x => x.Key == k && x.Show && x.Url != string.Empty))
                        {
                            sb.Append("<span title=\"" + name + "\">" + Icon(k) + "</span>");
                            shown++;
                        }
                    }
                    sb.Append(shown == 0 ? "<small>Add your links in Settings</small>" : string.Empty);
                    sb.Append("</div><small class=\"ay-hint\">Links come from Settings &rarr; Social links</small></div>");
                    break;
                default:
                    sb.Append("<div class=\"ay-vcta\">" + Ce("title", b.Title, "ay-h2", "Heading") + Ce("text", b.Text, "ay-p", "One line inviting people to write") + "<span class=\"ay-fake\">Get in touch</span></div>");
                    break;
            }
            sb.Append("</div></section>");
            return sb.ToString();
        }

        private static string Editor(SiteData site, string pg)
        {
            List<Block> list = pg == "about" ? site.Pages.About : site.Pages.Home;
            StringBuilder sb = new StringBuilder("<div class=\"ay-ptop\"><div><h1>Pages</h1><p>Drag blocks to arrange the page. Click any text to change it.</p></div></div>");
            sb.Append("<div class=\"ay-lbar\"><div class=\"ay-tabs\" role=\"tablist\"><button type=\"button\" role=\"tab\" class=\"" + (pg == "home" ? "ay-act" : string.Empty) + "\" onclick=\"AdminLayoutJs.tab('home')\">Home</button><button type=\"button\" role=\"tab\" class=\"" + (pg == "about" ? "ay-act" : string.Empty) + "\" onclick=\"AdminLayoutJs.tab('about')\">About</button></div>");
            sb.Append("<div class=\"ay-acts\"><span class=\"ay-dirty\" id=\"ay-dirty\"></span><button type=\"button\" class=\"ay-btn ay-btn-ghost ay-btn-sm\" onclick=\"AdminLayoutJs.undo()\">Restore last version</button><a class=\"ay-btn ay-btn-ghost ay-btn-sm\" href=\"" + (pg == "about" ? "About" : "Home") + "\" target=\"_blank\" rel=\"noopener\">View page</a><button type=\"button\" class=\"ay-btn ay-btn-sm\" onclick=\"AdminLayoutJs.save()\">Save page</button></div></div>");
            sb.Append("<div class=\"ay-canvas\" id=\"ay-canvas\" data-page=\"" + pg + "\">");
            foreach (Block b in list)
            {
                sb.Append(BlockHtml(b, site));
            }
            sb.Append("</div><div class=\"ay-add\"><b>Add a block</b><div>");
            foreach (string t in Types)
            {
                sb.Append("<button type=\"button\" onclick=\"AdminLayoutJs.add('" + t + "')\">+ " + Label(t) + "</button>");
            }
            sb.Append("</div></div><div class=\"ay-holder\" id=\"ay-holder\"></div><template id=\"ay-picks\">" + Picker(site) + "</template>");
            return sb.ToString();
        }

        private static string MainHtml(SiteData site, Session s)
        {
            return Editor(site, "home");
        }

        private static string Source(string? v, SiteData site)
        {
            string s = v ?? "featured";
            return s == "newest" || (s.StartsWith("series:", StringComparison.Ordinal) && site.Series.Any(x => "series:" + x.Key == s)) ? s : "featured";
        }

        private static string Page(string v)
        {
            return v == "about" ? "about" : "home";
        }

        public async Task<ApiResponse> Show()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminLayoutJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            response.SetElementContents("ay-main", Editor(site, Page(GetDataValue("pg"))));
            response.ExecuteScript("AdminLayoutJs.loaded();");
            return response;
        }

        public async Task<ApiResponse> Add()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminLayoutJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            string type = Types.Contains(GetDataValue("type")) ? GetDataValue("type") : "text";
            string photo = site.Works.Count > 0 ? site.Works[0].Full : "images/site/hero.webp";
            Block b = new Block { Id = "b" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant(), Type = type };
            switch (type)
            {
                case "hero":
                    b.Title = "A new headline";
                    b.Image = photo;
                    break;
                case "text":
                    b.Title = "New section";
                    b.Text = "Write something here.";
                    break;
                case "image":
                    b.Image = photo;
                    break;
                case "quote":
                    b.Text = "A line worth repeating.";
                    b.Title = site.Settings.Name;
                    break;
                case "split":
                    b.Title = "New section";
                    b.Text = "Write something here.";
                    b.Image = photo;
                    break;
                case "works":
                    b.Title = "Selected works";
                    b.Text = "6";
                    break;
                case "latest":
                    b.Title = "From the journal";
                    b.Text = "3";
                    break;
                case "social":
                    b.Title = "Follow along";
                    b.Text = "New work and behind-the-scenes clips between posts.";
                    break;
                default:
                    b.Title = "Say hello";
                    b.Text = "Tell me about your project.";
                    break;
            }
            response.SetElementContents("ay-holder", BlockHtml(b, site));
            response.ExecuteScript("AdminLayoutJs.added();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminLayoutJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            string pg = Page(GetDataValue("pg"));
            List<Block> input;
            try
            {
                input = JsonSerializer.Deserialize<List<Block>>(GetDataValue("blocks"), ReadOpts) ?? new List<Block>();
            }
            catch (JsonException)
            {
                response.ExecuteScript("AdminLayoutJs.toast('Could not read the page. Please try again');");
                return response;
            }
            if (input.Count == 0 || input.Count > 20)
            {
                response.ExecuteScript("AdminLayoutJs.toast('A page needs 1 to 20 blocks');");
                return response;
            }
            List<Block> clean = new List<Block>();
            HashSet<string> ids = new HashSet<string>();
            foreach (Block b in input)
            {
                if (!Types.Contains(b.Type))
                {
                    continue;
                }
                string id = Regex.IsMatch(b.Id ?? string.Empty, "^[a-z0-9]{1,16}$") && !ids.Contains(b.Id!) ? b.Id! : "b" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
                ids.Add(id);
                string title = Clip(b.Title, 120);
                string text = Clip((b.Text ?? string.Empty).Replace("\r", string.Empty), 2000);
                string image = Clip(b.Image, 200);
                bool needsImg = b.Type == "hero" || b.Type == "image" || b.Type == "split";
                if (needsImg && !SafeImage(image))
                {
                    response.ExecuteScript("AdminLayoutJs.toast('Choose a photo for every " + Label(b.Type).ToLowerInvariant() + " block');");
                    return response;
                }
                if (b.Type == "works")
                {
                    text = int.TryParse(text, out int n) ? Math.Clamp(n, 3, 24).ToString(CultureInfo.InvariantCulture) : "8";
                }
                if (b.Type == "latest")
                {
                    text = int.TryParse(text, out int n) ? Math.Clamp(n, 1, 9).ToString(CultureInfo.InvariantCulture) : "3";
                }
                clean.Add(new Block { Id = id, Type = b.Type, Title = title, Text = text, Image = needsImg ? image : string.Empty, Flip = b.Type == "split" && b.Flip, Source = b.Type == "works" ? Source(b.Source, site) : "featured" });
            }
            if (pg == "about")
            {
                site.Pages.About = clean;
            }
            else
            {
                site.Pages.Home = clean;
            }
            await SaveSite(site, s.Sbx, true);
            response.ExecuteScript("AdminLayoutJs.saved();");
            return response;
        }

        public async Task<ApiResponse> Undo()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminLayoutJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            string file = SiteFile(s.Sbx);
            string prev = Path.Combine(Path.GetDirectoryName(file) ?? Root, "site.prev.json");
            if (!File.Exists(prev))
            {
                response.ExecuteScript("AdminLayoutJs.toast('There is no earlier version');");
                return response;
            }
            SiteData old = JsonSerializer.Deserialize<SiteData>(await File.ReadAllTextAsync(prev), ReadOpts) ?? site;
            await SaveSite(old, s.Sbx, true);
            response.SetElementContents("ay-main", Editor(old, Page(GetDataValue("pg"))));
            Chrome(response, old, s);
            response.ExecuteScript("AdminLayoutJs.loaded();AdminLayoutJs.toast('Previous version restored');");
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
