using HtmlAgilityPack;

static class Utilities {
	public static HtmlDocument LoadHTML(string html) {
		var document = new HtmlDocument();
		document.LoadHtml(html);
		return document;
	}

	public static string Spaces(int count) =>
		new string(' ', count);

	public static void Erase(int count) =>
		Console.Write($"\r{Spaces(count)}\r");
}