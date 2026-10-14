using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArtistBlog.Models;
using SkyNet;

namespace ArtistBlog.codes
{
    public class Home : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await LoadSite();
            HtmlDoc.SetTitle(HtmlDecodeTitle(site.Settings.Name) + " | Photography and field notes");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", site.Settings.Tagline);
            string main = await MainHtml(site, QueryValue(""), string.Empty);
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_brand}", HtmlEncode(site.Settings.Name))
                .Replace("{plhd_name}", HtmlEncode(site.Settings.Name))
                .Replace("{plhd_tagline}", HtmlEncode(site.Settings.Tagline))
                .Replace("{plhd_theme}", site.Settings.Theme == "dark" ? "dark" : "light")
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--hm-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_social}", SocialHtml(site.Settings, "hm", "foot"))
                .Replace("{plhd_main}", main);
        }

        private static string HtmlDecodeTitle(string v)
        {
            return (v ?? string.Empty).Replace("<", string.Empty).Replace(">", string.Empty);
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("HomeJs.hydrated(false);");
                return response;
            }
            SiteData site = await LoadSite(sbx);
            response.SetElementContents("hm-main", await MainHtml(site, GetDataValue("arg"), sbx));
            response.SetElementContents("hm-brand", HtmlEncode(site.Settings.Name));
            response.SetElementContents("hm-foot-name", HtmlEncode(site.Settings.Name));
            response.SetElementContents("hm-foot-tag", HtmlEncode(site.Settings.Tagline));
            response.SetElementContents("hm-foot-soc", SocialHtml(site.Settings, "hm", "foot"));
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("HomeJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
            return response;
        }

        private async Task<string> Writable(ApiResponse response, string requested)
        {
            if (!Demo)
            {
                return string.Empty;
            }
            string sbx = Sandbox(requested);
            if (sbx == string.Empty)
            {
                sbx = NewSandbox();
                response.ExecuteScript("HomeJs.sandboxed('" + sbx + "');");
            }
            await Task.Yield();
            return sbx;
        }

        private async Task<string> MainHtml(SiteData site, string arg, string sbx)
        {
            await Task.Yield();
            StringBuilder sb = new StringBuilder();
            foreach (Block b in site.Pages.Home)
            {
                sb.Append(BlockHtml(b, site, "hm"));
            }
            return sb.ToString();
        }

        private static string Img(string src, string alt, string cls = "")
        {
            return "<img" + (cls != string.Empty ? " class=\"" + cls + "\"" : string.Empty) + " src=\"" + Attr(src) + "\" alt=\"" + Attr(alt) + "\" loading=\"lazy\">";
        }

        private static string TileHtml(Work w, string cls)
        {
            return "<button type=\"button\" class=\"" + cls + "-tile\" onclick=\"HomeJs.view('" + Attr(w.Id) + "')\"><img src=\"" + Attr(w.Thumb) + "\" alt=\"" + Attr(w.Title) + "\" width=\"" + w.W + "\" height=\"" + w.H + "\" loading=\"lazy\"><span>" + HtmlEncode(w.Title) + " &middot; " + w.Year + "</span></button>";
        }

        private static string CardHtml(BlogPost p, string cls)
        {
            return "<a class=\"" + cls + "-card\" href=\"Post?p=" + Uri.EscapeDataString(p.Slug) + "\">" + Img(p.Cover, p.Title) + "<small>" + HtmlEncode(NiceDate(p.Date)) + "</small><h3>" + HtmlEncode(p.Title) + "</h3><p>" + HtmlEncode(p.Excerpt) + "</p></a>";
        }

        private static IEnumerable<Work> Picked(SiteData site, string? source)
        {
            string src = source ?? "featured";
            if (src == "newest")
            {
                return site.Works.Select((w, i) => (w, i)).OrderByDescending(x => x.w.Year).ThenBy(x => x.i).Select(x => x.w);
            }
            if (src.StartsWith("series:", StringComparison.Ordinal))
            {
                return site.Works.Where(w => w.Series == src.Substring(7));
            }
            return site.Works.Any(w => w.Featured) ? site.Works.Where(w => w.Featured) : site.Works;
        }

        private static string BlockHtml(Block b, SiteData site, string cls)
        {
            int n;
            switch (b.Type)
            {
                case "hero":
                    return "<section class=\"" + cls + "-hero\">" + Img(b.Image, b.Title) + "<div class=\"" + cls + "-wrap " + cls + "-hero-in\"><h1>" + HtmlEncode(b.Title) + "</h1>" + (b.Text != string.Empty ? "<p>" + HtmlEncode(b.Text) + "</p>" : string.Empty) + "</div></section>";
                case "text":
                    return "<section class=\"" + cls + "-wrap " + cls + "-sec\"><div class=\"" + cls + "-btext\">" + (b.Title != string.Empty ? "<h2>" + HtmlEncode(b.Title) + "</h2>" : string.Empty) + Paras(b.Text, cls + "-bp") + "</div></section>";
                case "image":
                    return "<section class=\"" + cls + "-wrap " + cls + "-sec " + cls + "-bimg\"><figure>" + Img(b.Image, b.Text) + (b.Text != string.Empty ? "<figcaption>" + HtmlEncode(b.Text) + "</figcaption>" : string.Empty) + "</figure></section>";
                case "quote":
                    return "<section class=\"" + cls + "-wrap " + cls + "-sec\"><div class=\"" + cls + "-quote\"><blockquote>&ldquo;" + HtmlEncode(b.Text) + "&rdquo;</blockquote>" + (b.Title != string.Empty ? "<cite>" + HtmlEncode(b.Title) + "</cite>" : string.Empty) + "</div></section>";
                case "split":
                    return "<section class=\"" + cls + "-wrap " + cls + "-sec\"><div class=\"" + cls + "-split" + (b.Flip ? " " + cls + "-flip" : string.Empty) + "\"><div>" + Img(b.Image, b.Title) + "</div><div>" + (b.Title != string.Empty ? "<h2>" + HtmlEncode(b.Title) + "</h2>" : string.Empty) + Paras(b.Text, cls + "-bp") + "</div></div></section>";
                case "works":
                    n = int.TryParse(b.Text, out int wn) ? Math.Clamp(wn, 3, 24) : 8;
                    StringBuilder ws = new StringBuilder();
                    foreach (Work w in Picked(site, b.Source).Take(n))
                    {
                        ws.Append(TileHtml(w, cls));
                    }
                    string all = (b.Source ?? string.Empty).StartsWith("series:", StringComparison.Ordinal) ? "Works?s=" + Uri.EscapeDataString(b.Source!.Substring(7)) : "Works";
                    return "<section class=\"" + cls + "-wrap " + cls + "-sec\"><div class=\"" + cls + "-sec-h\"><h2>" + HtmlEncode(b.Title) + "</h2><a class=\"" + cls + "-more\" href=\"" + all + "\">All works</a></div><div class=\"" + cls + "-masonry\">" + ws + "</div></section>";
                case "latest":
                    n = int.TryParse(b.Text, out int pn) ? Math.Clamp(pn, 1, 9) : 3;
                    StringBuilder ps = new StringBuilder();
                    foreach (BlogPost p in site.Posts.Where(p => p.Published).OrderByDescending(p => p.Date).Take(n))
                    {
                        ps.Append(CardHtml(p, cls));
                    }
                    return "<section class=\"" + cls + "-wrap " + cls + "-sec\"><div class=\"" + cls + "-sec-h\"><h2>" + HtmlEncode(b.Title) + "</h2><a class=\"" + cls + "-more\" href=\"Journal\">All posts</a></div><div class=\"" + cls + "-posts\">" + ps + "</div></section>";
                case "social":
                    string links = SocialHtml(site.Settings, cls, "list");
                    return links == string.Empty ? string.Empty : "<section class=\"" + cls + "-wrap " + cls + "-sec\"><div class=\"" + cls + "-social\">" + (b.Title != string.Empty ? "<h2>" + HtmlEncode(b.Title) + "</h2>" : string.Empty) + (b.Text != string.Empty ? "<p>" + HtmlEncode(b.Text) + "</p>" : string.Empty) + "<div class=\"" + cls + "-socl\">" + links + "</div></div></section>";
                case "contact":
                    return "<section class=\"" + cls + "-wrap " + cls + "-sec\"><div class=\"" + cls + "-cta\"><h2>" + HtmlEncode(b.Title) + "</h2><p>" + HtmlEncode(b.Text) + "</p><a class=\"" + cls + "-btn\" href=\"Contact\">Get in touch</a></div></section>";
                default:
                    return string.Empty;
            }
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

        private static string SocialHtml(Settings st, string cls, string mode)
        {
            StringBuilder sb = new StringBuilder();
            foreach ((string k, string name) in Networks)
            {
                SocialLink? l = st.Social.FirstOrDefault(x => x.Key == k && x.Show && SafeUrl(x.Url));
                if (l == null)
                {
                    continue;
                }
                string href = Demo ? "#" : Attr(l.Url);
                string extra = Demo ? " onclick=\"return HomeJs.demoLink()\"" : " target=\"_blank\" rel=\"noopener me\"";
                if (mode == "list")
                {
                    sb.Append("<a class=\"" + cls + "-sl\" href=\"" + href + "\"" + extra + ">" + Icon(k) + "<span><b>" + name + "</b><small>" + HtmlEncode(Handle(l.Url)) + "</small></span></a>");
                }
                else
                {
                    sb.Append("<a class=\"" + cls + "-soc\" href=\"" + href + "\" aria-label=\"" + name + "\" title=\"" + name + "\"" + extra + ">" + Icon(k) + "</a>");
                }
            }
            return sb.ToString();
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
