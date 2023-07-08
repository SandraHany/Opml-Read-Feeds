using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Http;
namespace WebApplication1.Pages
{
    public class RssModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly List<RssModelClass> _rssList = new List<RssModelClass>();
        private readonly ILogger<RssModel> _logger;
        private bool _isLoaded = false;

        public List<RssModelClass> RssList { get; set; } = new();
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 5;
        public int TotalItemCount { get; set; }

        public RssModel(IHttpClientFactory httpClientFactory, ILogger<RssModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            this._logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(int? page)
        {
            PageNumber = Convert.ToInt32(Request.Query["page"]);
            _logger.LogInformation($"PageNumber: {PageNumber}, PageSize: {PageSize}");

            if (!_isLoaded)
            {
                using (var client = _httpClientFactory.CreateClient())
                {
                    var response = await client.GetStringAsync("https://blue.feedland.org/opml?screenname=dave");

                    var doc = new XmlDocument();
                    doc.LoadXml(response);
                    var manager = new XmlNamespaceManager(doc.NameTable);
                    manager.AddNamespace("opml", "http://www.opml.org/spec2");

                    var outlineNodes = doc.SelectNodes("//outline[@xmlUrl]", manager).Cast<XmlNode>();
                    TotalItemCount = outlineNodes.Count();

                    foreach (var node in outlineNodes)
                    {
                        var modelObject = new RssModelClass();
                        modelObject.Text = node.Attributes["text"].Value;
                        modelObject.XmlUrl = node.Attributes["xmlUrl"].Value;
                        modelObject.HtmlUrl = node.Attributes["htmlUrl"]?.Value ?? "";
                        modelObject.Items = new List<RssItem>();
                        _rssList.Add(modelObject);
                    }
                }

                _isLoaded = true;
            }

            // Filter the feeds based on the current page number and page size
            var filteredRssList = _rssList
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            foreach (var modelObject in filteredRssList)
            {
                if (!modelObject.Items.Any())
                {
                    using (var client = _httpClientFactory.CreateClient())
                    {
                        var feedResponse = await client.GetStringAsync(modelObject.XmlUrl);
                        var feedDoc = new XmlDocument();
                        feedDoc.LoadXml(feedResponse);

                        foreach (XmlNode itemNode in feedDoc.SelectNodes("/rss/channel/item"))
                        {
                            var item = new RssItem();
                            item.Link = itemNode["link"]?.InnerText;
                            item.Description = itemNode["description"]?.InnerText;
                            item.PublishDate = DateTime.TryParse(itemNode["pubDate"]?.InnerText, out var publishDate) ? publishDate : DateTime.MinValue;
                            item.Guid = itemNode["guid"]?.InnerText ?? "";
                            modelObject.Items.Add(item);
                        }
                    }
                }
            }

            RssList = filteredRssList;

            return Page();
        }

        public class RssModelClass
        {
            public string Text { get; set; }
            public string XmlUrl { get; set; }
            public string HtmlUrl { get; set; }
            public List<RssItem> Items { get; set; }
        }

        public class RssItem
        {
            public string Link { get; set; }
            public string Description { get; set; }
            public DateTime PublishDate { get; set; }
            public string Guid { get; set; }
        }
    }
}
