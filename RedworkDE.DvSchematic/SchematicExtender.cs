using System;
using System.Collections.Generic;
using DV.CabControls;
using DV.CabControls.Spec;
using HarmonyLib;
using UnityEngine;

namespace RedworkDE.DvSchematic
{
	public class SchematicExtender : MonoBehaviour
	{
		private static string[] _targetPageTextures =
		{
			"Map-SteelMill-A",
			"Map-Farm-A",
			"Map-FoodFactory-A",
			"Map-GoodsFactory-A",
			"Map-CitySW-A",
			"Map-Harbor-A",
			"Map-MachineFactory-A",
			"Map-CoalMine-A",
			"Map-IronOreMineEast-A",
			"Map-IronOreMineWest-A",
			"Map-ForestCentral-A",
			"Map-ForestSouth-A",
			"Map-Sawmill-A",
			"Map-OilWellNorth-A",
			"Map-OilWellCentral-A",
			"Map-MilitaryBase-A",
		};

		private PageBook _book;
		private int _page;
		private PointMapping<Vector2> _w2m;
		private PointMapping<Vector2> _m2w;
		private GameObject _marker;
		private Transform _yardLinks;

		void Awake()
		{
			_book = GetComponent<PageBook>();
			var map = Resources.Load<GameObject>("Map").GetComponent<WorldMap>();
			_marker = Instantiate(map.playerIndicator.gameObject, transform);
			_marker.transform.localPosition = Vector3.zero;
			MakePageButtons();

			_yardLinks = new GameObject("YardLinks").transform;
			_yardLinks.parent = transform;
			_yardLinks.localPosition = Vector3.zero;
			_yardLinks.localRotation = Quaternion.identity;
		}

		void MakePageButtons()
		{
			const float offsetLeft = 0.227f;
			const float width = 0.034f;
			const float height = 0.088f;
			const int count = 16;

			var left = offsetLeft;

			var switchButtons = new GameObject("Buttons").transform;
			switchButtons.parent = transform;
			switchButtons.localPosition = Vector3.zero;
			switchButtons.localRotation = Quaternion.identity;

			for (int i = 0; i < count; i++)
			{
				MakeButton(switchButtons, left, 0, width, height, Array.FindIndex(_book.pageTextures, t => t.name == _targetPageTextures[i]));
				left += width;
			}
		}

		private void MakeButton(Transform parent, float left, float bottom, float width, float height, int page)
		{
			var container = new GameObject("link_to_" + page);
			container.transform.parent = parent;
			container.transform.localPosition = Project(new Vector2(left + width / 2, bottom + height / 2));
			container.transform.localRotation = Quaternion.identity;
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			var mr = go.GetComponent<MeshRenderer>();
			mr.material.SetOverrideTag("RenderType", "TransparentCutout");
			mr.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
			mr.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
			mr.material.SetInt("_ZWrite", 1);
			mr.material.EnableKeyword("_ALPHATEST_ON");
			mr.material.DisableKeyword("_ALPHABLEND_ON");
			mr.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
			mr.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
			mr.material.color = Color.clear;
			go.transform.parent = container.transform;
			go.transform.localPosition = Vector3.zero;
			go.transform.localRotation = Quaternion.identity;
			var size = ProjectSize(new Vector2(width, height));
			go.transform.localScale = new Vector3(size.y, 0.001f, size.x);
			var spec = go.AddComponent<Button>();
			spec.useJoints = false;
			var button = go.GetComponent<ButtonBase>();
			button.add_Used(() =>
			{
				_book.FlipTo(page);
			});
			var rb = go.GetComponent<Rigidbody>();
			if (rb) rb.mass = 0;
		}

		void CreateLinks(List<(int tl, int tr, int br, int bl, string target)> rects, Vector2[] points)
		{
			Log.Debug($"rects: {rects.Count}; points: {points.Length}");
			foreach (var (tl, tr, br, bl, target) in rects)
			{
				Log.Debug($"{tl} {tr} {br} {bl} / {target}");

				if (string.IsNullOrWhiteSpace(target)) continue;

				MakeButton(_yardLinks, points[bl].x, points[bl].y, points[tr].x - points[bl].x, points[tr].y - points[bl].y, Array.FindIndex(_book.pageTextures, t => t.name.EndsWith(target)));
			}
		}

		void Update()
		{
			if (_page != _book.currentPage)
			{
				_page = _book.currentPage;
				var name = _book.pageTextures[_page].name.StripStart("Map-");
				_w2m = StationMapping.Get(name);
				_m2w = StationMapping.Get(name, false);
				var rects = StationMapping.GetRects(name);

				if (_w2m is null || _m2w is null || rects is null) Log.Debug($"No mapping for texture {name} exists");

				foreach (Transform link in _yardLinks) Destroy(link.gameObject);
				if (rects is {}) CreateLinks(rects, _w2m.Values);
			}

			if (_w2m is {})
			{
				var currentPos = PlayerManager.PlayerTransform.position - WorldMover.currentMove;
				var lookAt = currentPos + PlayerManager.PlayerTransform.rotation * Vector3.forward;

				var hasMappedPos = _w2m.Get(new Vector2(currentPos.x, currentPos.z), out var mapPos);
				var hasMappedLookAt = _w2m.Get(new Vector2(lookAt.x, lookAt.z), out var mapLookAt);
				if (hasMappedPos && hasMappedLookAt)
				{
					_marker.SetActive(true);
					
					_marker.transform.localPosition = Project(mapPos);
					_marker.transform.localRotation = Quaternion.LookRotation(new Vector3(mapPos.y - mapLookAt.y, 0, mapPos.x - mapLookAt.x));
				}
				else
				{
					_marker.SetActive(false);
				}
			}
			else
			{
				_marker.SetActive(false);
			}
		}

		const float SCALE = 0.6826656f;
		const float A3_WIDTH = 0.420f;
		const float A3_HEIGHT = 0.297f;
		private Vector3 Project(Vector2 mapPos)
		{
			return new Vector3(mapPos.y * SCALE * A3_HEIGHT - 0.5f * SCALE * A3_HEIGHT, 0, 0.5f * SCALE * A3_WIDTH - mapPos.x * SCALE * A3_WIDTH);
		}
		private Vector2 ProjectSize(Vector2 mapPos)
		{
			return new Vector2(mapPos.x * SCALE * A3_WIDTH, mapPos.y * SCALE * A3_HEIGHT);
		}

		[HarmonyPatch(typeof(PageBook), nameof(PageBook.Start)), HarmonyPostfix]
		private static void Init(PageBook __instance)
		{
			if (__instance.name == "MapSchematic")
				__instance.gameObject.AddComponent<SchematicExtender>();
		}
	}
}
