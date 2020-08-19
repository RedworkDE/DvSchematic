#if !IS_PUBLISH

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace RedworkDE.DvSchematic
{
	public static class MapIntegration
	{
		[NotNull] private static string _sourcePath = GetSourcePath();
		[NotNull] private static string _mapData = Path.GetFullPath(Path.Combine(GetSourcePath(), "..", "MapPages"));
		[NotNull] private static string GetSourcePath([CallerFilePath] string path = null) => Path.GetDirectoryName(path);


		private static Stream GetFile([NotNull] string name)
		{
			var path = Path.Combine(_mapData, name);
			if (File.Exists(path)) return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			return null;
		}

		[CanBeNull]
		private static string GetContentType([NotNull] string ext)
		{
			return ext.ToLowerInvariant() switch
			{
				".html" => "text/html",
				".css" => "text/css",
				".js" => "application/javascript",
				".png" => "image/png",
				_ => null
			};
		}

		[AutoLoad]
		public static void Init()
		{
			var page = WebServer.RegisterPage("schematic", "schematic");

			page.Register("/info.json", SendInfo);
			page.Register("/save", SaveInfo);
			page.Register(new Regex("^/lines/(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase), FindLines);
			page.Register(new Regex("^/map/([^/]*)/([\\d.]+)/([\\d.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase), MapPoint);
			page.Register(new Regex("^/images/", RegexOptions.Compiled | RegexOptions.IgnoreCase), (context, uri) => context?.AllowCors()?.SetResponseStreamAsync(GetFile(uri.AbsolutePath.Substring(8)), GetContentType(Path.GetExtension(context.Request.Url.AbsolutePath))));
		}

		private static void MapPoint([CanBeNull] HttpListenerContext ctx, [NotNull] Match match)
		{
			if (ctx.AllowCors() is null) return;

			var map = StationMapping.Get(match.Groups[1].Value);

			float.TryParse(match.Groups[2].Value, out var x);
			float.TryParse(match.Groups[3].Value, out var y);

			if (!map.Get(new Vector2(x, y), out var val)) ctx.SetResponseTextAsync("[]");
			else ctx.SetResponseTextAsync(JToken.FromObject(new[] {val.x, val.y}).ToString(Formatting.None));
		}

		private static void FindLines([CanBeNull] HttpListenerContext ctx, [NotNull] Match match)
		{
			if (ctx.AllowCors() is null) return;

			var map = StationMapping.Get(match.Groups[1].Value);
			
			var minX = map.Points.Min(p => p.x);
			var minY = map.Points.Min(p => p.y);
			var maxX = map.Points.Max(p => p.x);
			var maxY = map.Points.Max(p => p.y);

			var lines = new List<float[][]>();
			for (var x = Mathf.Round(minX / 10) * 10; x < maxX; x += 10)
			{
				var line = new List<float[]>();
				for (var y = Mathf.Round(minY); y < maxY; y++)
				{
					if (map.Get(new Vector2(x, y), out var mapped))
						line.Add(new[] {mapped.x, mapped.y});
				}

				if (line.Count > 1)
					lines.Add(line.ToArray());
			}

			for (var y = Mathf.Round(minY / 10) * 10; y < maxY; y += 10)
			{
				var line = new List<float[]>();
				for (var x = Mathf.Round(minX); x < maxX; x++)
				{
					if (map.Get(new Vector2(x, y), out var mapped))
						line.Add(new[] { mapped.x, mapped.y });
				}

				if (line.Count > 1)
					lines.Add(line.ToArray());
			}

			ctx.SetResponseTextAsync(JToken.FromObject(lines).ToString(Formatting.None));
		}

		private static async Task SaveInfo(HttpListenerContext obj)
		{
			if (obj.AllowCors() is null) return;

			var info = Path.Combine(_sourcePath, "map.json");
			var infoObj = JArray.Parse(File.ReadAllText(info));
			var patch = JObject.Parse(await obj.GetRequestTextAsync());
			foreach (var (key, token) in patch)
			{
				var data = infoObj.First(t => t["name"].Value<string>() == key);
				data["points"] = token["points"];
				data["rects"] = token["rects"];
			}

			File.WriteAllText(info, infoObj.ToString(Formatting.None));
			obj.SetResponseTextAsync("");
		}

		private static async Task SendInfo([NotNull] HttpListenerContext arg)
		{
			if (arg.AllowCors() is null) return;

			var info = Path.Combine(_sourcePath, "map.json");
			if (File.Exists(info))
			{
				await arg.SetResponseStreamAsync(File.OpenRead(info));
				return;
			}

			var json = JToken.FromObject(Directory.EnumerateFiles(_mapData, "Map-*.png").Select(file => new {name = Path.GetFileNameWithoutExtension(file).Substring(4), url = $"http://localhost:6886/schematic/images/{Path.GetFileName(file)}", points = new object[0], rects = new object[0] })).ToString(Formatting.None);
			File.WriteAllText(info, json);
			await arg.SetResponseTextAsync(json);
		}

		[CanBeNull]
		private static HttpListenerContext AllowCors([CanBeNull] this HttpListenerContext ctx)
		{
			if (ctx is null) return null;

			var origin = ctx.Request.Headers.Get("Origin");
			if (origin is {}) ctx.Response.AddHeader("Access-Control-Allow-Origin", origin);
			var method = ctx.Request.Headers.Get("Access-Control-Request-Method");
			if (method is {}) ctx.Response.AddHeader("Access-Control-Allow-Methods", method);
			var header = ctx.Request.Headers.Get("Access-Control-Request-Headers");
			if (header is { }) ctx.Response.AddHeader("Access-Control-Allow-Headers", header);
			ctx.Response.AppendHeader("Vary", "Origin, Access-Control-Request-Method, Access-Control-Request-Headers");

			if (ctx.Request.HttpMethod == "OPTIONS")
			{
				ctx.Response.OutputStream.Dispose();
				return null;
			}

			return ctx;
		}
	}
}

#endif