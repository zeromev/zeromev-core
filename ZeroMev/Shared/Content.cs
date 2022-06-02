using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Json;

namespace ZeroMev.Shared
{
    public static class Content
    {
        public const string DocumentationSite = "https://zeromev.github.io/";
        public const string DefaultTag = "section";

        public static async Task<string> GetWithinTag(HttpClient http, string page)
        {
            return await GetWithinTag(http, page, DefaultTag);
        }

        public static async Task<string> GetWithinTag(HttpClient http, string page, string tag)
        {
            return await GetWithinTag(http, DocumentationSite, page, tag);
        }

        public static async Task<string> GetWithinTag(HttpClient http, string site, string page, string tag)
        {
            string content = await http.GetStringAsync(site + page);

            string open = $"<{tag}>";
            string close = $"</{tag}>";
            int from = content.IndexOf(open);
            int to = content.IndexOf(close);

            if (from == -1 || to == -1 || from >= to)
                return content;
            return content.Substring(from, to - from);
        }
    }
}