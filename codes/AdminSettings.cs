using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArtistBlog.Models;
using SkyNet;

namespace ArtistBlog.codes
{
    public class AdminSettings : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await LoadSite();
            HtmlDoc.SetTitle("Studio admin | Settings");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--as-accent:" + site.Settings.Accent : string.Empty);
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
            response.SetElementContents("as-cnt-AdminComments", pc > 0 ? pc.ToString(CultureInfo.InvariantCulture) : string.Empty);
            response.SetElementContents("as-cnt-AdminMessages", um > 0 ? um.ToString(CultureInfo.InvariantCulture) : string.Empty);
            int left = s.Sbx != string.Empty ? Math.Max(1, (int)Math.Ceiling((Made(s.Sbx).AddMinutes(60) - DateTime.UtcNow).TotalMinutes)) : 0;
            response.SetElementContents("as-demo", s.Sbx != string.Empty ? "<span class=\"as-dl\">Demo copy &middot; resets in </span><span class=\"as-ds\">Demo &middot; </span>" + left + " min" : string.Empty);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            response.SetElementContents("as-main", MainHtml(site, s));
            Chrome(response, site, s);
            response.ExecuteScript("AdminSettingsJs.loaded();");
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
            response.ExecuteScript("AdminSettingsJs.out();");
            return response;
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

        private static string MockIcons()
        {
            StringBuilder sb = new StringBuilder();
            foreach ((string k, string name) in Networks)
            {
                sb.Append("<i id=\"as-mi-" + k + "\" title=\"" + name + "\">" + Icon(k) + "</i>");
            }
            return sb.ToString();
        }

        private static readonly string[] Swatches = { "#C8794A", "#9C5B3B", "#5E7C6B", "#3F6D8F", "#7A5C8E", "#B0475A" };

        private static string Field(string id, string label, string value, int max, string type = "text", string hint = "")
        {
            return "<label class=\"as-field\" for=\"as-" + id + "\"><span>" + label + "</span><input type=\"" + type + "\" id=\"as-" + id + "\" maxlength=\"" + max + "\" value=\"" + Attr(value) + "\" oninput=\"AdminSettingsJs.preview()\">" + (hint != string.Empty ? "<small class=\"as-hint\">" + hint + "</small>" : string.Empty) + "<em class=\"as-err\" id=\"as-e-" + id + "\"></em></label>";
        }

        private static string MainHtml(SiteData site, Session s)
        {
            Settings t = site.Settings;
            string accent = SafeColor(t.Accent) ? t.Accent : "#C8794A";
            StringBuilder sb = new StringBuilder("<div class=\"as-ptop\"><div><h1>Settings</h1><p>Changes show on the public site as soon as you save.</p></div></div>");
            sb.Append("<form class=\"as-set\" onsubmit=\"return AdminSettingsJs.save()\" novalidate><div>");
            sb.Append("<div class=\"as-panel\"><h2>Site</h2><div class=\"as-form2\">" + Field("name", "Name", t.Name, 40) + Field("tagline", "Tagline", t.Tagline, 80) + "</div></div>");
            sb.Append("<div class=\"as-panel\"><h2>Look</h2><div class=\"as-form2\"><div class=\"as-field\"><span>Accent colour</span><div class=\"as-sw\">");
            foreach (string c in Swatches)
            {
                sb.Append("<button type=\"button\" style=\"background:" + c + "\" aria-label=\"Use " + c + "\" onclick=\"AdminSettingsJs.swatch('" + c + "')\"></button>");
            }
            sb.Append("<input type=\"color\" id=\"as-accent\" value=\"" + accent + "\" oninput=\"AdminSettingsJs.preview()\" aria-label=\"Custom colour\"></div><em class=\"as-err\" id=\"as-e-accent\"></em></div>");
            sb.Append("<div class=\"as-field\"><span>Theme</span><div class=\"as-themes\"><label><input type=\"radio\" name=\"as-theme\" id=\"as-theme-light\" onchange=\"AdminSettingsJs.preview()\"" + (t.Theme != "dark" ? " checked" : string.Empty) + "> Light</label><label><input type=\"radio\" name=\"as-theme\" id=\"as-theme-dark\" onchange=\"AdminSettingsJs.preview()\"" + (t.Theme == "dark" ? " checked" : string.Empty) + "> Dark</label></div><small class=\"as-hint\">Visitors can still switch with the moon button.</small></div></div></div>");
            sb.Append("<div class=\"as-panel\"><h2>Contact</h2><div class=\"as-form2\"><div class=\"as-row2\">" + Field("email", "Email", t.Email, 80, "email") + Field("location", "Studio location", t.Location, 60) + "</div></div></div>");
            sb.Append("<div class=\"as-panel\"><h2>Social links</h2><p>Full web addresses starting with https://. Untick a network to hide its icon and keep the address; an empty box is hidden too. They show in the footer, on the contact page and in Follow blocks." + (s.Sbx != string.Empty ? " In the demo the icons do not leave the site." : string.Empty) + "</p><div class=\"as-srows\">");
            foreach ((string k, string name) in Networks)
            {
                SocialLink? l = t.Social.FirstOrDefault(x => x.Key == k);
                bool on = l == null || l.Show;
                sb.Append("<div class=\"as-sr" + (on ? string.Empty : " as-off") + "\"><input type=\"checkbox\" class=\"as-sv\" id=\"as-v-" + k + "\" aria-label=\"Show " + name + "\" title=\"Show on the site\"" + (on ? " checked" : string.Empty) + " onchange=\"AdminSettingsJs.preview()\"><label for=\"as-s-" + k + "\">" + Icon(k) + name + "</label><input type=\"url\" class=\"as-su\" id=\"as-s-" + k + "\" data-k=\"" + k + "\" maxlength=\"200\" placeholder=\"https://\" value=\"" + Attr(l != null ? l.Url : string.Empty) + "\" oninput=\"AdminSettingsJs.preview()\"></div>");
            }
            sb.Append("</div><em class=\"as-err\" id=\"as-e-social\"></em></div>");
            sb.Append("<div class=\"as-save\"><button type=\"submit\" class=\"as-btn\">Save settings</button><a class=\"as-btn as-btn-ghost\" href=\"Home\" target=\"_blank\" rel=\"noopener\">View site</a></div></div>");
            sb.Append("<div class=\"as-prev\"><div class=\"as-panel\"><h2>Preview</h2><div class=\"as-mock\" id=\"as-mock\"><div class=\"as-mock-h\"><b id=\"as-mock-name\"></b><i></i></div><img src=\"images/site/hero-thumb.webp\" alt=\"\"><div class=\"as-mock-b\"><p id=\"as-mock-tag\"></p><span id=\"as-mock-btn\">Get in touch</span></div><div class=\"as-mock-soc\">" + MockIcons() + "</div></div></div>");
            sb.Append("<div class=\"as-panel\"><h2>Admin password</h2><p>" + (s.Sbx != string.Empty ? "For when you run the site for real: " : string.Empty) + "type a new password, create its hash and paste it into <b>appConfig/application.cfg</b> as <b>app.admin.passhash</b>.</p><div class=\"as-form2\"><label class=\"as-field\" for=\"as-secret\"><span>New password</span><input type=\"password\" id=\"as-secret\" maxlength=\"100\" autocomplete=\"new-password\"><em class=\"as-err\" id=\"as-e-secret\"></em></label><div><button type=\"button\" class=\"as-btn as-btn-ghost as-btn-sm\" onclick=\"AdminSettingsJs.hash()\">Create hash</button></div><div class=\"as-hash\" id=\"as-hash\"></div></div></div></div></form>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            string name = Clip(GetDataValue("name"), 80);
            string tagline = Clip(GetDataValue("tagline"), 160);
            string accent = Clip(GetDataValue("accent"), 7);
            string theme = GetDataValue("theme") == "dark" ? "dark" : "light";
            string email = Clip(GetDataValue("email"), 100);
            List<SocialLink> social = new List<SocialLink>();
            string eSocial = string.Empty;
            try
            {
                foreach (SocialLink l in JsonSerializer.Deserialize<List<SocialLink>>(GetDataValue("social"), ReadOpts) ?? new List<SocialLink>())
                {
                    string url = (l.Url ?? string.Empty).Trim();
                    string key = Networks.Any(n => n.Key == l.Key) ? l.Key : string.Empty;
                    if (key == string.Empty || url == string.Empty || social.Any(x => x.Key == key))
                    {
                        continue;
                    }
                    if (!SafeUrl(url))
                    {
                        eSocial = Networks.First(n => n.Key == key).Name + ": use a full address that starts with https://";
                        break;
                    }
                    social.Add(new SocialLink { Key = key, Url = url, Show = l.Show });
                }
            }
            catch (JsonException)
            {
                eSocial = "Could not read the social links.";
            }
            string loc = Clip(GetDataValue("location"), 100);
            string eName = name.Length < 2 || name.Length > 40 ? "Use 2 to 40 characters." : string.Empty;
            string eTag = tagline.Length > 80 ? "Keep it under 80 characters." : string.Empty;
            string eAccent = !SafeColor(accent) ? "Choose a colour." : string.Empty;
            string eEmail = email != string.Empty && !IsEmail(email) ? "Enter a valid email address or leave it empty." : string.Empty;
            string eLoc = loc.Length > 60 ? "Keep it under 60 characters." : string.Empty;
            response.SetElementContents("as-e-name", eName);
            response.SetElementContents("as-e-tagline", eTag);
            response.SetElementContents("as-e-accent", eAccent);
            response.SetElementContents("as-e-email", eEmail);
            response.SetElementContents("as-e-social", eSocial);
            response.SetElementContents("as-e-location", eLoc);
            if (eName + eTag + eAccent + eEmail + eSocial + eLoc != string.Empty)
            {
                response.ExecuteScript("AdminSettingsJs.toast('Please check the highlighted fields');");
                return response;
            }
            site.Settings = new Settings { Name = name, Tagline = tagline, Accent = accent.ToUpperInvariant(), Theme = theme, Email = email, Location = loc, Social = social };
            await SaveSite(site, s.Sbx, true);
            response.ExecuteScript("document.getElementById('as-site').style.setProperty('--as-accent','" + accent + "');AdminSettingsJs.toast('Settings saved');");
            return response;
        }

        public async Task<ApiResponse> Hash()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            string secret = GetDataValue("secret");
            if (secret.Length < 10 || secret.Length > 100)
            {
                response.SetElementContents("as-e-secret", "Use 10 to 100 characters.");
                response.SetElementContents("as-hash", string.Empty);
                return response;
            }
            string hex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
            response.SetElementContents("as-e-secret", string.Empty);
            response.SetElementContents("as-hash", "app.admin.passhash | " + hex);
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
