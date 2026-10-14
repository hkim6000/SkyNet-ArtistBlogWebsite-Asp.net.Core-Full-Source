using System.Text.Json.Serialization;

namespace ArtistBlog.Models
{
    public class SiteData
    {
        public Settings Settings { get; set; } = new Settings();
        public List<Series> Series { get; set; } = new List<Series>();
        public List<Work> Works { get; set; } = new List<Work>();
        public List<BlogPost> Posts { get; set; } = new List<BlogPost>();
        public List<PostComment> Comments { get; set; } = new List<PostComment>();
        public List<Message> Messages { get; set; } = new List<Message>();
        public Pages Pages { get; set; } = new Pages();
    }

    public class Settings
    {
        public string Name { get; set; } = string.Empty;
        public string Tagline { get; set; } = string.Empty;
        public string Accent { get; set; } = "#C8794A";
        public string Theme { get; set; } = "light";
        public string Email { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public List<SocialLink> Social { get; set; } = new List<SocialLink>();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Instagram { get; set; }
    }

    public class SocialLink
    {
        public string Key { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool Show { get; set; } = true;
    }

    public class Series
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    public class Work
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Series { get; set; } = string.Empty;
        public int Year { get; set; }
        public string Full { get; set; } = string.Empty;
        public string Thumb { get; set; } = string.Empty;
        public int W { get; set; }
        public int H { get; set; }
        public string Caption { get; set; } = string.Empty;
        public bool Featured { get; set; }
    }

    public class BlogPost
    {
        public string Slug { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
        public string Cover { get; set; } = string.Empty;
        public string Excerpt { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool Published { get; set; }
        public int Likes { get; set; }
    }

    public class PostComment
    {
        public string Id { get; set; } = string.Empty;
        public string Post { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
    }

    public class Message
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public bool Done { get; set; }
    }

    public class Pages
    {
        public List<Block> Home { get; set; } = new List<Block>();
        public List<Block> About { get; set; } = new List<Block>();
    }

    public class Block
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Image { get; set; } = string.Empty;
        public bool Flip { get; set; }
        public string Source { get; set; } = "featured";
    }

    public class Session
    {
        public string Tok { get; set; } = string.Empty;
        public string Sbx { get; set; } = string.Empty;
        public DateTime Exp { get; set; }
    }
}
