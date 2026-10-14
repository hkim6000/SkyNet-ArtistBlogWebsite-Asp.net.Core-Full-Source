using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArtistBlog.Models;
using SkyNet;

namespace ArtistBlog.codes
{
    public class AdminWorks : WebPage
    {
        public override async Task OnInitialized()
        {
            SiteData site = await LoadSite();
            HtmlDoc.SetTitle("Studio admin | Works");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText.Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--aw-accent:" + site.Settings.Accent : string.Empty);
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
            response.SetElementContents("aw-cnt-AdminComments", pc > 0 ? pc.ToString(CultureInfo.InvariantCulture) : string.Empty);
            response.SetElementContents("aw-cnt-AdminMessages", um > 0 ? um.ToString(CultureInfo.InvariantCulture) : string.Empty);
            int left = s.Sbx != string.Empty ? Math.Max(1, (int)Math.Ceiling((Made(s.Sbx).AddMinutes(60) - DateTime.UtcNow).TotalMinutes)) : 0;
            response.SetElementContents("aw-demo", s.Sbx != string.Empty ? "<span class=\"aw-dl\">Demo copy &middot; resets in </span><span class=\"aw-ds\">Demo &middot; </span>" + left + " min" : string.Empty);
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminWorksJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            response.SetElementContents("aw-main", MainHtml(site, s));
            Chrome(response, site, s);
            response.ExecuteScript("AdminWorksJs.loaded();");
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
            response.ExecuteScript("AdminWorksJs.out();");
            return response;
        }

        private const long MaxFile = 8L * 1024 * 1024;
        private const long MaxBox = 60L * 1024 * 1024;
        private const int MaxUploads = 20;

        private static string ItemHtml(Work w, SiteData site)
        {
            Series? sr = site.Series.FirstOrDefault(x => x.Key == w.Series);
            return "<div class=\"aw-item\" data-id=\"" + Attr(w.Id) + "\"><img src=\"" + Attr(w.Thumb) + "\" alt=\"\" loading=\"lazy\"><button type=\"button\" class=\"aw-grip\" aria-label=\"Drag to reorder\" onpointerdown=\"AdminWorksJs.grab(event,this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"currentColor\" aria-hidden=\"true\"><circle cx=\"9\" cy=\"6\" r=\"1.6\"/><circle cx=\"15\" cy=\"6\" r=\"1.6\"/><circle cx=\"9\" cy=\"12\" r=\"1.6\"/><circle cx=\"15\" cy=\"12\" r=\"1.6\"/><circle cx=\"9\" cy=\"18\" r=\"1.6\"/><circle cx=\"15\" cy=\"18\" r=\"1.6\"/></svg></button><button type=\"button\" class=\"aw-star" + (w.Featured ? " aw-on" : string.Empty) + "\" aria-pressed=\"" + (w.Featured ? "true" : "false") + "\" aria-label=\"Featured\" title=\"" + (w.Featured ? "Featured on the home page" : "Feature on the home page") + "\" onclick=\"AdminWorksJs.star('" + Attr(w.Id) + "')\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M12 3.5l2.6 5.4 5.9.8-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.8z\"/></svg></button><div><b>" + HtmlEncode(w.Title) + "</b><small>" + HtmlEncode(sr != null ? sr.Title : "No series") + " &middot; " + w.Year + "</small><div class=\"aw-iacts\"><button type=\"button\" class=\"aw-mv\" aria-label=\"Move earlier\" onclick=\"AdminWorksJs.move('" + Attr(w.Id) + "',-1)\"><svg viewBox=\"0 0 24 24\" width=\"28\" height=\"28\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M15 5l-7 7 7 7\"/></svg></button><button type=\"button\" onclick=\"AdminWorksJs.edit('" + Attr(w.Id) + "')\">Edit</button><button type=\"button\" class=\"aw-mv\" aria-label=\"Move later\" onclick=\"AdminWorksJs.move('" + Attr(w.Id) + "',1)\"><svg viewBox=\"0 0 24 24\" width=\"28\" height=\"28\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M9 5l7 7-7 7\"/></svg></button></div></div></div>";
        }

        private static string GridHtml(SiteData site)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Work w in site.Works)
            {
                sb.Append(ItemHtml(w, site));
            }
            return site.Works.Count == 0 ? "<p class=\"aw-none\">No photographs yet. Upload your first ones above.</p>" : sb.ToString();
        }

        private static string SeriesHtml(SiteData site)
        {
            StringBuilder sb = new StringBuilder("<div class=\"aw-ser\" id=\"aw-ser\">");
            foreach (Series x in site.Series)
            {
                int n = site.Works.Count(w => w.Series == x.Key);
                sb.Append("<div class=\"aw-srow\" data-key=\"" + Attr(x.Key) + "\"><input type=\"text\" maxlength=\"40\" value=\"" + Attr(x.Title) + "\" aria-label=\"Series title\"><input type=\"text\" maxlength=\"160\" value=\"" + Attr(x.Text) + "\" aria-label=\"Description\"><small>" + n + (n == 1 ? " photo" : " photos") + "</small></div>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string MainHtml(SiteData site, Session s)
        {
            StringBuilder sb = new StringBuilder("<div class=\"aw-ptop\"><div><h1>Works</h1><p>" + site.Works.Count + " photographs. Drag the handle to change the order on the site; &#9733; features a photo in Selected works.</p></div><div class=\"aw-acts\"><a class=\"aw-btn aw-btn-ghost aw-btn-sm\" href=\"Works\" target=\"_blank\" rel=\"noopener\">View works</a></div></div>");
            sb.Append("<div class=\"aw-drop\" id=\"aw-drop\"><b>Drop photos here</b><p>JPG, PNG or WebP. They are resized in your browser to 1600 pixels before upload." + (s.Sbx != string.Empty ? " Demo: up to " + MaxUploads + " uploads, kept for one hour." : string.Empty) + "</p><button type=\"button\" class=\"aw-btn aw-btn-sm\" onclick=\"AdminWorksJs.choose()\">Choose photos</button><input type=\"file\" id=\"aw-file\" accept=\"image/jpeg,image/png,image/webp\" multiple hidden onchange=\"AdminWorksJs.chosen(this)\"><ul class=\"aw-prog\" id=\"aw-prog\"></ul></div>");
            sb.Append("<div class=\"aw-wgrid\" id=\"aw-wgrid\">" + GridHtml(site) + "</div>");
            sb.Append("<div class=\"aw-panel\" style=\"margin-top:24px\"><div class=\"aw-ph\"><h2>Series</h2></div><p>The filters on the works page. A series can be removed once it has no photos; clear its title and save.</p>" + SeriesHtml(site) + "<div class=\"aw-acts\" style=\"margin-top:14px\"><button type=\"button\" class=\"aw-btn aw-btn-ghost aw-btn-sm\" onclick=\"AdminWorksJs.addSeries()\">Add series</button><button type=\"button\" class=\"aw-btn aw-btn-sm\" onclick=\"AdminWorksJs.saveSeries()\">Save series</button></div></div>");
            return sb.ToString();
        }

        private static (byte[]? data, string ext) Decode(string url)
        {
            Match m = Regex.Match(url ?? string.Empty, "^data:image/(webp|jpeg);base64,([A-Za-z0-9+/=]+)$");
            if (!m.Success || m.Groups[2].Length > MaxFile * 4 / 3 + 8)
            {
                return (null, string.Empty);
            }
            byte[] buf = new byte[m.Groups[2].Length * 3 / 4 + 3];
            if (!Convert.TryFromBase64String(m.Groups[2].Value, buf, out int n))
            {
                return (null, string.Empty);
            }
            byte[] data = buf.AsSpan(0, n).ToArray();
            bool webp = n > 12 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P';
            bool jpeg = n > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;
            if (m.Groups[1].Value == "webp" && webp)
            {
                return (data, "webp");
            }
            if (m.Groups[1].Value == "jpeg" && jpeg)
            {
                return (data, "jpg");
            }
            return (null, string.Empty);
        }

        private static string UploadDir(string sbx)
        {
            return sbx == string.Empty ? Path.Combine(Root, "images", "uploads") : Path.Combine(Root, "images", "uploads", "sandbox", sbx);
        }

        private static string UploadUrl(string sbx)
        {
            return sbx == string.Empty ? "images/uploads/" : "images/uploads/sandbox/" + sbx + "/";
        }

        public async Task<ApiResponse> Upload()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminWorksJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            (byte[]? full, string ext) = Decode(GetDataValue("full"));
            (byte[]? thumb, string text) = Decode(GetDataValue("thumb"));
            if (full == null || thumb == null)
            {
                response.ExecuteScript("AdminWorksJs.uploaded(false,'not a supported image or larger than 8 MB');");
                return response;
            }
            string dir = UploadDir(s.Sbx);
            Directory.CreateDirectory(dir);
            if (s.Sbx != string.Empty)
            {
                FileInfo[] have = new DirectoryInfo(dir).GetFiles();
                if (have.Length / 2 >= MaxUploads || have.Sum(f => f.Length) + full.Length + thumb.Length > MaxBox)
                {
                    response.ExecuteScript("AdminWorksJs.uploaded(false,'demo upload limit reached');");
                    return response;
                }
            }
            if (site.Works.Count >= 500)
            {
                response.ExecuteScript("AdminWorksJs.uploaded(false,'the library is full');");
                return response;
            }
            string id = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
            await File.WriteAllBytesAsync(Path.Combine(dir, id + "." + ext), full);
            await File.WriteAllBytesAsync(Path.Combine(dir, id + "-t." + text), thumb);
            string name = Path.GetFileNameWithoutExtension(Clip(GetDataValue("name"), 120));
            name = Regex.Replace(name, "[_\\-.]+", " ").Trim();
            name = name.Length < 2 ? "Untitled" : char.ToUpperInvariant(name[0]) + Clip(name, 80).Substring(1);
            int w = int.TryParse(GetDataValue("w"), out int wv) ? Math.Clamp(wv, 1, 4000) : 1600;
            int h = int.TryParse(GetDataValue("h"), out int hv) ? Math.Clamp(hv, 1, 4000) : 1067;
            site.Works.Insert(0, new Work { Id = "u" + id, Title = name, Series = site.Series.Count > 0 ? site.Series[0].Key : string.Empty, Year = DateTime.Now.Year, Full = UploadUrl(s.Sbx) + id + "." + ext, Thumb = UploadUrl(s.Sbx) + id + "-t." + text, W = w, H = h });
            await SaveSite(site, s.Sbx, true);
            response.SetElementContents("aw-wgrid", GridHtml(site));
            response.ExecuteScript("AdminWorksJs.uploaded(true,'added');");
            return response;
        }

        public async Task<ApiResponse> Order()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminWorksJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            List<string> ids = GetDataValue("ids").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (ids.Count != site.Works.Count || ids.Distinct().Count() != ids.Count || ids.Any(i => !site.Works.Any(w => w.Id == i)))
            {
                response.SetElementContents("aw-wgrid", GridHtml(site));
                response.ExecuteScript("AdminWorksJs.toast('The list changed. Please try again');");
                return response;
            }
            site.Works = ids.Select(i => site.Works.First(w => w.Id == i)).ToList();
            await SaveSite(site, s.Sbx, true);
            response.ExecuteScript("AdminWorksJs.toast('Order saved');");
            return response;
        }

        public async Task<ApiResponse> Edit()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminWorksJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            Work? w = site.Works.FirstOrDefault(x => x.Id == Clip(GetDataValue("id"), 80));
            if (w == null)
            {
                response.ExecuteScript("AdminWorksJs.toast('This photograph is gone');");
                return response;
            }
            StringBuilder sb = new StringBuilder("<h2>Edit photograph</h2><form class=\"aw-wedit\" onsubmit=\"return AdminWorksJs.save()\" novalidate><img src=\"" + Attr(w.Thumb) + "\" alt=\"\"><div class=\"aw-form2\"><input type=\"hidden\" id=\"aw-wid\" value=\"" + Attr(w.Id) + "\">");
            sb.Append("<label class=\"aw-field\" for=\"aw-wtitle\"><span>Title</span><input type=\"text\" id=\"aw-wtitle\" maxlength=\"80\" value=\"" + Attr(w.Title) + "\"><em class=\"aw-err\" id=\"aw-e-wtitle\"></em></label>");
            sb.Append("<div class=\"aw-row2\"><label class=\"aw-field\" for=\"aw-wseries\"><span>Series</span><select id=\"aw-wseries\"><option value=\"\">No series</option>");
            foreach (Series x in site.Series)
            {
                sb.Append("<option value=\"" + Attr(x.Key) + "\"" + (x.Key == w.Series ? " selected" : string.Empty) + ">" + HtmlEncode(x.Title) + "</option>");
            }
            sb.Append("</select></label><label class=\"aw-field\" for=\"aw-wyear\"><span>Year</span><input type=\"number\" id=\"aw-wyear\" min=\"1900\" max=\"2100\" value=\"" + w.Year + "\"><em class=\"aw-err\" id=\"aw-e-wyear\"></em></label></div>");
            sb.Append("<label class=\"aw-field\" for=\"aw-wcaption\"><span>Caption</span><textarea id=\"aw-wcaption\" rows=\"3\" maxlength=\"200\">" + HtmlEncode(w.Caption) + "</textarea><em class=\"aw-err\" id=\"aw-e-wcaption\"></em></label>");
            sb.Append("<label class=\"aw-wtog\" for=\"aw-wfeat\"><input type=\"checkbox\" id=\"aw-wfeat\"" + (w.Featured ? " checked" : string.Empty) + "> Featured in Selected works</label>");
            sb.Append("<div class=\"aw-wbtns\"><button type=\"button\" class=\"aw-btn aw-btn-danger aw-btn-sm\" onclick=\"AdminWorksJs.remove()\">Delete</button><button type=\"submit\" class=\"aw-btn aw-btn-sm\">Save</button></div></div></form>");
            response.SetElementContents("aw-mbody", sb.ToString());
            response.ExecuteScript("AdminWorksJs.openModal();");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminWorksJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            Work? w = site.Works.FirstOrDefault(x => x.Id == Clip(GetDataValue("id"), 80));
            if (w == null)
            {
                response.ExecuteScript("AdminWorksJs.closeModal();AdminWorksJs.toast('This photograph is gone');");
                return response;
            }
            string title = Clip(GetDataValue("title"), 120);
            string series = GetDataValue("series");
            string caption = Clip(GetDataValue("caption"), 400);
            bool yearOk = int.TryParse(GetDataValue("year"), out int year) && year >= 1900 && year <= DateTime.Now.Year + 1;
            string eTitle = title.Length < 2 || title.Length > 80 ? "Use 2 to 80 characters." : string.Empty;
            string eYear = !yearOk ? "Enter a year from 1900." : string.Empty;
            string eCap = caption.Length > 200 ? "Keep it under 200 characters." : string.Empty;
            response.SetElementContents("aw-e-wtitle", eTitle);
            response.SetElementContents("aw-e-wyear", eYear);
            response.SetElementContents("aw-e-wcaption", eCap);
            if (eTitle + eYear + eCap != string.Empty)
            {
                return response;
            }
            w.Title = title;
            w.Series = site.Series.Any(x => x.Key == series) ? series : string.Empty;
            w.Year = year;
            w.Caption = caption;
            w.Featured = GetDataValue("featured") == "1";
            await SaveSite(site, s.Sbx, true);
            response.SetElementContents("aw-wgrid", GridHtml(site));
            response.ExecuteScript("AdminWorksJs.closeModal();AdminWorksJs.toast('Saved');");
            return response;
        }

        public async Task<ApiResponse> Feature()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminWorksJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            Work? w = site.Works.FirstOrDefault(x => x.Id == Clip(GetDataValue("id"), 80));
            if (w == null)
            {
                response.ExecuteScript("AdminWorksJs.toast('This photograph is gone');");
                return response;
            }
            w.Featured = !w.Featured;
            await SaveSite(site, s.Sbx, true);
            response.SetElementContents("aw-wgrid", GridHtml(site));
            int n = site.Works.Count(x => x.Featured);
            response.ExecuteScript("AdminWorksJs.toast('" + (w.Featured ? "Featured" : "No longer featured") + " \\u00b7 " + n + " featured');");
            return response;
        }

        public async Task<ApiResponse> Delete()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminWorksJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            Work? w = site.Works.FirstOrDefault(x => x.Id == Clip(GetDataValue("id"), 80));
            if (w == null)
            {
                response.ExecuteScript("AdminWorksJs.closeModal();");
                return response;
            }
            List<string> uses = site.Posts.Where(p => p.Cover == w.Full || p.Body.Contains("\"" + w.Full + "\"")).Select(p => "the post \u201c" + p.Title + "\u201d").ToList();
            uses.AddRange(site.Pages.Home.Concat(site.Pages.About).Where(b => b.Image == w.Full).Select(b => "a block on the " + (site.Pages.Home.Contains(b) ? "home" : "about") + " page"));
            if (uses.Count > 0)
            {
                response.SetElementContents("aw-e-wcaption", HtmlEncode("Used by " + string.Join(", ", uses.Take(3)) + ". Change that first."));
                return response;
            }
            site.Works.Remove(w);
            await SaveSite(site, s.Sbx, true);
            string mine = UploadUrl(s.Sbx);
            foreach (string f in new[] { w.Full, w.Thumb })
            {
                if (f.StartsWith(mine, StringComparison.Ordinal) && !f.Substring(mine.Length).Contains('/'))
                {
                    string file = Path.Combine(UploadDir(s.Sbx), f.Substring(mine.Length));
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
            }
            response.SetElementContents("aw-wgrid", GridHtml(site));
            response.ExecuteScript("AdminWorksJs.closeModal();AdminWorksJs.toast('Deleted');");
            return response;
        }

        private sealed class SeriesRow
        {
            public string Key { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
        }

        public async Task<ApiResponse> SaveSeries()
        {
            ApiResponse response = new ApiResponse();
            Session? s = await Auth();
            if (s == null)
            {
                response.ExecuteScript("AdminWorksJs.expired();");
                return response;
            }
            SiteData site = await LoadSite(s.Sbx);
            List<SeriesRow> rows;
            try
            {
                rows = JsonSerializer.Deserialize<List<SeriesRow>>(GetDataValue("list"), ReadOpts) ?? new List<SeriesRow>();
            }
            catch (JsonException)
            {
                rows = new List<SeriesRow>();
            }
            List<Series> result = new List<Series>();
            foreach (SeriesRow r in rows.Take(12))
            {
                string title = Clip(r.Title, 40);
                string text = Clip(r.Text, 160);
                Series? old = site.Series.FirstOrDefault(x => x.Key == r.Key);
                if (title == string.Empty)
                {
                    if (old != null && site.Works.Any(w => w.Series == old.Key))
                    {
                        response.ExecuteScript("AdminWorksJs.toast('" + Attr(old.Title.Replace("'", " ")) + " still has photos');");
                        return response;
                    }
                    continue;
                }
                string key = old != null ? old.Key : Regex.Replace(title.ToLowerInvariant().Replace("&", "and"), "[^a-z0-9]+", "-").Trim('-');
                if (key == string.Empty || result.Any(x => x.Key == key))
                {
                    key = "series-" + (result.Count + 1);
                }
                result.Add(new Series { Key = key, Title = title, Text = text });
            }
            foreach (Series old in site.Series.Where(x => !rows.Any(r => r.Key == x.Key)))
            {
                result.Add(old);
            }
            site.Series = result;
            await SaveSite(site, s.Sbx, true);
            response.SetElementContents("aw-main", MainHtml(site, s));
            response.ExecuteScript("AdminWorksJs.loaded();AdminWorksJs.toast('Series saved');");
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
