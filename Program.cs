using System.IO.Compression;
using System.Text.RegularExpressions;
using Fizzler.Systems.HtmlAgilityPack;
using Markdig;
using static Utilities;

if (!args.Any()) {
	Console.Error.WriteLine("Provide a link to the glowfic as an argument");
	return;
}

Regex glowfic_regex = new(@"glowfic.com/posts/(\d+)");
Regex paragraph_regex = new(@"<p>(.+?)</p>");
Regex underline_regex = new(@"<span style=""text-decoration: underline;"">(.+?)</span>");
Regex non_bullet_regex_1 = new(@"^(\s*)- ");
Regex non_bullet_regex_2 = new(@"(\n\s*)- ");
Regex query_string_regex = new(@"\?.+$");
Regex tag_regex = new(@"<(\/?)(([-a-z]+).*?)>");

var match = glowfic_regex.Match(args[0]);
if (!match.Success) {
	Console.Error.WriteLine("Invalid glowfic URL");
	return;
}

var glowfic_id = match.Groups[1].Value;

Console.WriteLine($"GlowFic ID: {glowfic_id}");

Console.Write("Downloading posts...");

var client = new HttpClient();
var glowfic = LoadHTML(await (await client.GetAsync($"https://glowfic.com/posts/{glowfic_id}?view=flat")).Content.ReadAsStringAsync());

Erase(20);

var title = glowfic.Title().Remove(" | Glowfic Constellation").Trim();
DateTime.TryParse(glowfic.QuerySelector(".details + .details")!.InnerText?.Split(": ").Last().Trim(), out var last_updated);
var author = glowfic.QuerySelector(".post-author")!.InnerText.Trim();

Console.WriteLine($"Glowfic: {title}");
Console.WriteLine($"Author: {author}");
Console.WriteLine($"Last updated: {last_updated:ddd, dd MMM yyyy, HH':'mm}");

var posts = glowfic.QuerySelectorAll(".post-container").Select(post => {
	var character   = post.QuerySelector(".post-character")?.InnerText;
	var screen_name = post.QuerySelector(".post-screenname")?.InnerText;
	var icon = post.QuerySelector(".post-icon > .icon");
	var content = post.QuerySelector(".post-content")?.InnerHtml;

	return new {
		Character = $"{character ?? null}{(screen_name is not null? $" - {screen_name}" : null)}".NullIfNothing(),
		Icon = icon is not null? new { Id = icon.Get("src"), Title = icon.Get("title") } : null,
		Text = paragraph_regex.Matches(content!).Select(match => match.Groups[1].Value
				.Replace(underline_regex, "<u>$1</u>")
				.Replace(non_bullet_regex_1, "$1\\-").Replace(non_bullet_regex_2, "$1\\-")
			).Join("\n\n")
	};
}).ToArray();

Console.WriteLine($"\nPosts: {posts.Length}");

var icons = posts.Where(p => p.Icon is not null).Select(post => post.Icon!.Id).Distinct().ToArray();

var images = icons
	.Select(async icon => {
		try {
			var image_data = await (await client.GetAsync(icon)).Content.ReadAsByteArrayAsync();
			return new {
				Source = icon,
				Filename = $"{Guid.NewGuid()}{query_string_regex.Replace(Path.GetExtension(icon), "")}",
				Data = image_data
			};
		} catch (Exception ex) {
			Console.Error.WriteLine($"\r[!] Error downloading image from \"{icon}\":\n\t[ex.GetType().Name]: {ex.Message}");
			return null;
		}
	})
	.Chunk(10)
	.SelectMany((tasks, index) => {
		Console.Write($"\rDownloading icons {(index * 10) + 1:D3}–{Math.Min((index + 1) * 10 + 1, icons.Length):D3} / {icons.Length:D3}...");
		Task.WaitAll(tasks);
		return tasks.Select(t => t.Result);
	})
	.Where(_ => _ is not null)
	.ToDictionary(o => o.Source, o => new { o.Filename, o.Data });

Erase(34);
Console.WriteLine($"Images: {icons.Length}\n");

Console.Write("Converting to markdown...");
var markdown = posts.Select(post => 
	(post.Icon is not null? $"<img title=\"{post.Icon.Title}\" src=\"assets/{(images.ContainsKey(post.Icon.Id)? images[post.Icon.Id].Filename : "not_found.png")}\" width=\"50\"/> ": null) + 
	(post.Character is not null? $"__[{post.Character}]__" : null) + 
	"\n\n" + post.Text).Join("\n\n-----\n\n");

Erase(25);
Console.Write("Converting to html...");
var html = Markdown.ToHtml(markdown).Replace("<br>", "<br/>");

string FixInterleavedHtmlTags(string html) { // @NOTE: THIS IS A HACK!
	var tags = tag_regex.Matches(html);
	var tag_stack = new Stack<Match>();
	var fixes = new Stack<(int index, int count, string replacement)>();
	foreach (Match tag in tags) {
		if (tag.Groups[2].Value.EndsWith("/")) continue;
		var closing = !string.IsNullOrEmpty(tag.Groups[1].Value);
		var name = tag.Groups[3].Value;

		if (closing) {
			if (!tag_stack.Any(t => t.Groups[3].Value == name))
				fixes.Push((tag.Index, tag.Length, ""));
			else {
				var between = tag_stack.TakeWhile(m => m.Groups[3].Value != name).Count();
				if (between > 0) {
					var replacement = tag.Value;
					var temp = new Stack<Match>();
					for (int i = 0; i < between; i++) {
						var prev = tag_stack.Pop();
						temp.Push(prev);
						var prev_name = prev.Groups[3].Value;
						var prev_full = prev.Groups[2].Value;
						replacement = $"</{prev_full}>{replacement}<{prev_name}>";
					}
					fixes.Push((tag.Index, tag.Length, replacement));
					tag_stack.Pop();
					while (temp.TryPop(out var temp_tag))
						tag_stack.Push(temp_tag);
				} else tag_stack.Pop();
			}
		} else
			tag_stack.Push(tag);
	}
	while (tag_stack.TryPop(out var tag))
		fixes.Push((html.Length, 0, $"</{tag.Groups[2].Value}>"));
	while (fixes.TryPop(out var fix)) {
		var (index, count, replacement) = fix;
		html = html.Substring(0, index) + replacement + html.Substring(index + count);
	}
	return html;
}

html = 
"<?xml version='1.0' encoding='utf-8'?>\n" + 
"<!DOCTYPE html>\n" + 
"<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\" epub:prefix=\"z3998: http://www.daisy.org/z3998/2012/vocab/structure/#\" lang=\"en\" xml:lang=\"en\">\n" + 
"<head><style>body { font-size: .8rem; }</style></head>\n" + 
"<body>\n"+ FixInterleavedHtmlTags(html) + "\n</body>\n" + 
"</html>";

Erase(25);

using var archive_file = File.OpenWrite($"./{title}.epub");
using var archive = new ZipArchive(archive_file, ZipArchiveMode.Create);

Console.Write("Generating ePUB: Compressing images...");
foreach (var image in images.Values)
	archive.WriteEntry($"assets/{image.Filename}", image.Data, CompressionLevel.SmallestSize);
Erase(40);
Console.Write("Generating ePUB: Compressing posts...");
archive.WriteEntry($"glowfic_{glowfic_id}.html", html, CompressionLevel.SmallestSize);

Erase(40);
Console.Write("Generating ePUB: Writing metadata...");
archive.WriteEntry("mimetype", "application/epub+zip");
archive.WriteEntry("META-INF/container.xml", 
	"<?xml version=\"1.0\"?>\n" + 
	"<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\">\n" + 
		"\t<rootfiles>\n" + 
			"\t\t<rootfile full-path=\"content.opf\" media-type=\"application/oebps-package+xml\"/>\n" +
		"\t</rootfiles>\n" + 
	"</container>\n");
archive.WriteEntry("content.opf",
	"<?xml version='1.0' encoding='utf-8'?>\n" + 
	"<package xmlns='http://www.idpf.org/2007/opf' unique-identifier='uuid_id' version='2.0'>\n" +
		"\t<metadata xmlns:dc='http://purl.org/dc/elements/1.1/'>\n" +
			$"\t\t<dc:title>{title}</dc:title>\n" + 
			$"\t\t<dc:creator>{author}</dc:creator>\n" + 
			$"\t\t<dc:date>{last_updated}</dc:date>\n" +
			"\t\t<dc:language>en</dc:language>\n" +
		"\t</metadata>\n" +
		"\t<manifest>\n" +
			$"\t\t<item href='glowfic_{glowfic_id}.html' id='content' media-type='application/xhtml+xml'/>\n" + 
		"\t</manifest>\n" +
		"\t<spine>\n" +
			"\t\t<itemref idref='content'/>\n" +
		"\t</spine>\n" +
	"</package>\n");

Erase(40);
Console.WriteLine("\nePUB generated");