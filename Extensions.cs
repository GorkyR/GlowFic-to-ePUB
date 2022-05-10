using HtmlAgilityPack;
using Fizzler.Systems.HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.IO.Compression;

static class Extensions {
	public static HtmlNode? QuerySelector(this HtmlDocument document, string selector) => 
		document.DocumentNode.QuerySelector(selector);

	public static IEnumerable<HtmlNode> QuerySelectorAll(this HtmlDocument document, string selector) =>
		document.DocumentNode.QuerySelectorAll(selector);

	public static string Get(this HtmlNode node, string attribute) =>
		node.GetAttributeValue(attribute, null);

	public static string Title(this HtmlDocument document) =>
		document.QuerySelector("title")!.InnerText;

	public static string Replace(this string text, Regex regex, string replacement) =>
		regex.Replace(text, replacement);

	public static string Join(this IEnumerable<string> strings, string separator = "") =>
		string.Join(separator, strings);

	public static string Remove(this string text, string pattern) =>
		text.Replace(pattern, "");

	public static string? NullIfNothing(this string text) =>
		string.IsNullOrWhiteSpace(text)? null : text;

	public static void WriteEntry(this ZipArchive archive, string entry_name, string contents, CompressionLevel compression_level = CompressionLevel.Fastest) {
		var entry = archive.CreateEntry(entry_name, compression_level);
		using var stream = entry.Open();
		using var writer = new StreamWriter(stream);
		writer.WriteLine(contents);
	}

	public static void WriteEntry(this ZipArchive archive, string entry_name, byte[] contents, CompressionLevel compression_level = CompressionLevel.Fastest) {
		var entry = archive.CreateEntry(entry_name, compression_level);
		using var stream = entry.Open();
		stream.Write(contents);
	}
}