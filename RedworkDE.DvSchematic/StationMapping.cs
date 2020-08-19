using UnityEngine;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

namespace RedworkDE.DvSchematic
{
	public class StationMapping
	{
		private static string _assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
#if IS_PUBLISH
		private static string _sourcePath = "";
#else
		private static string _sourcePath = GetSourcePath();
		private static string GetSourcePath([CallerFilePath] string path = null) => Path.GetDirectoryName(path);
#endif

		private static ConcurrentDictionary<string, (PointMapping<Vector2>, PointMapping<Vector2>, List<(int tl, int tr, int br, int bl, string target)>)> _cache = new ConcurrentDictionary<string, (PointMapping<Vector2>, PointMapping<Vector2>, List<(int tl, int tr, int br, int bl, string target)>)>();
#if !IS_PUBLISH
		private static FileSystemWatcher _fsw;
#endif

		public static PointMapping<Vector2> Get(string name, bool worldToMap = true)
		{
			var item = _cache.GetOrAdd(name, Get);
			if (worldToMap) return item.Item1;
			return item.Item2;
		}

		public static List<(int tl, int tr, int br, int bl, string target)> GetRects(string name)
		{
			var item = _cache.GetOrAdd(name, Get);
			return item.Item3;
		}

		private static (PointMapping<Vector2>, PointMapping<Vector2>, List<(int,int,int,int,string)>) Get(string name)
		{
			var info = Path.Combine(_sourcePath, "map.json");
			if (!File.Exists(info)) info = Path.Combine(_assemblyPath, "map.json");
			var infoObj = JArray.Parse(File.ReadAllText(info));
			var data = infoObj.FirstOrDefault(t => t["name"].Value<string>() == name);
			if (data is null) return default;
			var points = data["points"].ToObject<List<PointInfo>>();

			if (points.Count < 3) return default;

			var map = new List<Vector2>();
			var world = new List<Vector2>();

			foreach (var pointInfo in points)
			{
				map.Add(new Vector2(pointInfo.map[0], pointInfo.map[1]));
				world.Add(new Vector2(pointInfo.world[1], pointInfo.world[0]));
			}

			var rects = new List<(int, int, int, int, string)>();
			foreach (var rect in data["rects"])
			{
				rects.Add((rect["points"][0].Value<int>(), rect["points"][1].Value<int>(), rect["points"][2].Value<int>(), rect["points"][3].Value<int>(), rect["linkTarget"].Value<string>()));
			}

#if !IS_PUBLISH
			if (_fsw is null)
			{
				_fsw = new FileSystemWatcher(_sourcePath, "info.json");
				_fsw.EnableRaisingEvents = true;
				_fsw.Changed += (sender, e) => _cache.Clear();
			}
#endif

			return (new PointMapping<Vector2>(world.ToArray(), map.ToArray(), Interpolate), new PointMapping<Vector2>(map.ToArray(), world.ToArray(), Interpolate), rects);
		}

		private static Vector2 Interpolate(Vector2 p0, float t0, Vector2 p1, float t1, Vector2 p2, float t2)
		{
			return p0 * t0 + p1 * t1 + p2 * t2;
		}

		private class PointInfo
		{
			public float[] map { get; set; }
			public float[] world { get; set; }
		}
	}
}