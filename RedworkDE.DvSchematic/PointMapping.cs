using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace RedworkDE.DvSchematic
{
	public class PointMapping<T>
	{
		public delegate T Interpolator(T p0, float t0, T p1, float t1, T p2, float t2);

		private (int P0, int P1, int P2)[] Triangles;
		[NotNull] public readonly Vector2[] Points;
		[NotNull] public readonly T[] Values;
		[NotNull] public Interpolator Interpolate;

		public PointMapping([NotNull] Vector2[] points, [NotNull] T[] values, [NotNull] Interpolator interpolate)
		{
			if (points is null) throw new ArgumentNullException(nameof(points));
			if (values is null) throw new ArgumentNullException(nameof(values));
			if (interpolate is null) throw new ArgumentNullException(nameof(interpolate));
			if (points.Length != values.Length) throw new ArgumentException("number of points must be the same as the number of values");
			if (points.Length < 3) throw new ArgumentException("not enough points");
			Points = points;
			Values = values;
			Interpolate = interpolate;
		}

		public void Triangulate()
		{
			var points = Points;

			[NotNull]
			Triangle[] FindBorder()
			{
				var minX = float.PositiveInfinity;
				var maxX = float.NegativeInfinity;
				var minY = float.PositiveInfinity;
				var maxY = float.NegativeInfinity;

				for (int i = 0; i < points.Length; i++)
				{
					var p = points[i];
					if (p.x < minX) minX = p.x;
					if (p.x > maxX) maxX = p.x;
					if (p.y < minY) minY = p.y;
					if (p.y > maxY) maxY = p.y;
				}

				const float margin = 1000;
				minX -= margin;
				maxX += margin;
				minY -= margin;
				maxY += margin;

				var corners = points.Length;
				points = new Vector2[points.Length + 4];
				Array.Copy(Points, points, Points.Length);
				points[corners + 0] = new Vector2(minX, maxY);
				points[corners + 1] = new Vector2(maxX, maxY);
				points[corners + 2] = new Vector2(maxX, minY);
				points[corners + 3] = new Vector2(minX, minY);

				return new[] {new Triangle(points, corners + 0, corners + 1, corners + 2), new Triangle(points, corners + 0, corners + 2, corners + 3)};
			}

			bool IsPointInsideCircumcircle(Triangle tri, Vector2 point)
			{
				return (tri.Center - point).sqrMagnitude < tri.RadiusSquare;
			}

			[NotNull]
			HashSet<Triangle> FindBadTriangles(Vector2 point, [NotNull] HashSet<Triangle> triangles)
			{
				var badTriangles = triangles.Where(o => IsPointInsideCircumcircle(o, point));
				return new HashSet<Triangle>(badTriangles);
			}

			[NotNull]
			List<Edge> FindHoleBoundaries([NotNull] HashSet<Triangle> badTriangles)
			{
				var edges = new List<Edge>();
				foreach (var triangle in badTriangles)
				{
					edges.Add(new Edge(triangle.P0, triangle.P1));
					edges.Add(new Edge(triangle.P1, triangle.P2));
					edges.Add(new Edge(triangle.P2, triangle.P0));
				}

				return edges.GroupBy(o => o).Where(o => o.Count() == 1).Select(o => o.First()).ToList();
			}

			var triangulation = new HashSet<Triangle>(FindBorder());

			for (var point = 0; point < points.Length; point++)
			{
				var badTriangles = FindBadTriangles(points[point], triangulation);
				var polygon = FindHoleBoundaries(badTriangles);

				triangulation.RemoveWhere(o => badTriangles.Contains(o));

				foreach (var edge in polygon.Where(possibleEdge => possibleEdge.P0 != point && possibleEdge.P1 != point))
				{
					var triangle = new Triangle(points, point, edge.P0, edge.P1);
					triangulation.Add(triangle);
				}
			}

			triangulation.RemoveWhere(o => o.P0 >= Points.Length || o.P1 >= Points.Length || o.P2 >= Points.Length);

			Triangles = triangulation.Select(tri => (tri.P0, tri.P1, tri.P2)).ToArray();
		}

		public bool Get(Vector2 p, out T val)
		{
			if (Triangles is null) Triangulate();

			for (int i = 0; i < Triangles.Length; i++)
			{
				var tri = Triangles[i];
				var p0 = Points[tri.P0];
				var p1 = Points[tri.P1];
				var p2 = Points[tri.P2];

				var invdet = 1 / ((p1.y - p2.y) * (p0.x - p2.x) + (p2.x - p1.x) * (p0.y - p2.y));
				var t0 = ((p1.y - p2.y) * (p.x - p2.x) + (p2.x - p1.x) * (p.y - p2.y)) * invdet;
				if (t0 < 0 || t0 > 1) continue;
				var t1 = ((p2.y - p0.y) * (p.x - p2.x) + (p0.x - p2.x) * (p.y - p2.y)) * invdet;
				if (t1 < 0 || t1 > 1) continue;
				var t2 = 1 - t0 - t1;
				if (t2 < 0 || t2 > 1) continue;
				val = Interpolate(Values[tri.P0], t0, Values[tri.P1], t1, Values[tri.P2], t2);
				return true;
			}

			val = default;
			return false;
		}

		private struct Triangle
		{
			public Triangle(Vector2[] points, int p0, int p1, int p2)
			{
				if (IsCounterClockwise(points[p0], points[p1], points[p2]))
				{
					P0 = p0;
					P1 = p1;
					P2 = p2;
				}
				else
				{
					P0 = p0;
					P1 = p2;
					P2 = p1;
				}

				// https://codefound.wordpress.com/2013/02/21/how-to-compute-a-circumcircle/
				// https://en.wikipedia.org/wiki/Circumscribed_circle
				var pp0 = points[P0];
				var pp1 = points[P1];
				var pp2 = points[P2];
				var dA = pp0.x * pp0.x + pp0.y * pp0.y;
				var dB = pp1.x * pp1.x + pp1.y * pp1.y;
				var dC = pp2.x * pp2.x + pp2.y * pp2.y;

				var aux1 = (dA * (pp2.y - pp1.y) + dB * (pp0.y - pp2.y) + dC * (pp1.y - pp0.y));
				var aux2 = -(dA * (pp2.x - pp1.x) + dB * (pp0.x - pp2.x) + dC * (pp1.x - pp0.x));
				var div = (2 * (pp0.x * (pp2.y - pp1.y) + pp1.x * (pp0.y - pp2.y) + pp2.x * (pp1.y - pp0.y)));

				if (div == 0)
				{
					throw new DivideByZeroException();
				}

				var center = new Vector2(aux1 / div, aux2 / div);
				Center = center;
				RadiusSquare = (center.x - pp0.x) * (center.x - pp0.x) + (center.y - pp0.y) * (center.y - pp0.y);
			}

			private static bool IsCounterClockwise(Vector2 p0, Vector2 p1, Vector2 p2)
			{
				return ((p1.x - p0.x) * (p2.y - p0.y) -
				        (p2.x - p0.x) * (p1.y - p0.y)) > 0;
			}

			public int P0;
			public int P1;
			public int P2;
			public Vector2 Center;
			public float RadiusSquare;

		}

		private struct Edge
		{
			public Edge(int p0, int p1)
			{
				if (p0 <= p1)
				{
					P0 = p0;
					P1 = p1;
				}
				else
				{
					P0 = p1;
					P1 = p0;
				}
			}

			public int P0;
			public int P1;
		}
	}
}
