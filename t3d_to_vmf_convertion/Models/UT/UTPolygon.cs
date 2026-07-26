using System.Runtime.CompilerServices;

namespace UTModels
{
    // CCW Order is assumed
    public class UTPolygon
    {
        // Gotta make these factorable
        void LogUTPolygon(UTPolygon polygon, LogLevel logLevel, string funcName, string infoString) =>
            Logger.Write(logLevel, $"{polygon}.{funcName}: {infoString}");

        // Debug Identity
        public string Name { get; set; }

        public UTPolygon(string name) => Name = name;

        public string Texture { get; set; } = "nodraw";
        public Vector Origin { get; set; } = new Vector();

        private Vector _normal = Vector.Zero;
        public Vector Normal {
            get
            {
                if (_normal.NearlyEquals(Vector.Zero)) CalculateNormal();
                return _normal;
            }
            set => _normal = value;
        }
        public Vector TextureU { get; set; } = new Vector();
        public Vector TextureV { get; set; } = new Vector();
        public int PanU { get; set; } = 0;
        public int PanV { get; set; } = 0;
        public int Flags { get; set; } = 0;
        private List<Vector> _vertices { get; set; } = new();
        public IReadOnlyList<Vector> Vertices => _vertices;

        // Meta data
        
        // This should be DI. No proper reason for poly to exist without an owner
        private BrushActor? _brush = null;
        public BrushActor? Brush
        {
            get => _brush;
            set
            {
                if (_brush is null) { _brush = value; return; }
                LogUTPolygon(this, LogLevel.WARNING, $"Brush Property:", $"Tried replacing polygon owner {_brush} with {value}");
            }
        }

        public void CopySettings(UTPolygon otherPolygon)
        {
            Name = otherPolygon.Name;
            Texture = otherPolygon.Texture;
            Origin = otherPolygon.Origin;
            TextureU = otherPolygon.TextureU;
            TextureV = otherPolygon.TextureV;
            PanU = otherPolygon.PanU;
            PanV = otherPolygon.PanV;
            Flags = otherPolygon.Flags;
        }

        public void CopyVertices(UTPolygon otherPolygon) => _vertices = otherPolygon.Vertices.ToList();

        public void Copy(UTPolygon otherPolygon)
        {
            CopySettings(otherPolygon);
            CopyVertices(otherPolygon);
        }

        public UTPolygon GetCopy(bool copyVertices = true)
        {
            UTPolygon copyPolygon = new(Name);

            copyPolygon.CopySettings(this);
            if (copyVertices)
                copyPolygon.CopyVertices(this);

            return copyPolygon;
        }

        private bool IsValidVertexToAdd(Vector vertexToCheck)
        {
            // Check if nearly-equal to some other vertex in polygon
            foreach (var vertex in Vertices)
                if (vertexToCheck.NearlyEquals(vertex))
                {
                    LogUTPolygon(this, LogLevel.WARNING, "IsValidVertexToAdd", $"Bad Vertex {vertexToCheck}, nearly equal to polygon vertex {vertex}");
                    return false;
                }

            // Planarity guard disabled. .t3d allows non-planar polys

            // var verticesCount = Vertices.Count;
            // if (verticesCount > 2 && !IsPointOnPlane(vertexToCheck))
            // {
            //     LogUTPolygon(this, LogLevel.WARNING, "IsValidVertexToAdd", $"Bad Vertex {vertexToCheck}, outside of polygon plane");
            //     return false;
            // }

            return true;
        }

        public bool PushVertex(Vector vertexToPush)
        {
            if (!IsValidVertexToAdd(vertexToPush)) return false;

            _normal = Vector.Zero;

            // Replace last vertex in case of collinearity
            // To make less trouble when creating planes for .vmf (currently)
            // In UT this seems to be common
            var verticesCount = _vertices.Count;
            if (verticesCount >= 2)
            {
                var lastVertex = _vertices[verticesCount - 1];
                var preLastVertex = _vertices[verticesCount - 2];
                if (lastVertex.IsOnLine(preLastVertex, vertexToPush, true))
                {
                    _vertices[verticesCount - 1] = vertexToPush;

                    return true;
                }
            }

            _vertices.Add(vertexToPush);
            return true;
        }

        public void ClearVertices()
        {
            _vertices.Clear();
            _normal = Vector.Zero;
        }

        public void ReverseVertexOrder()
        {
            _vertices.Reverse();
            _normal = Vector.Zero; // Invalidate Normal
        }

        public void ScaleVertices(double factor)
        {
            for (int i = 0; _vertices.Count > i; i++)
                _vertices[i] *= factor;
            _normal = Vector.Zero; // Invalidate Normal
        }

        private void CalculateNormal()
        {
            var verticesCount = Vertices.Count;
            if (verticesCount < 3) return;

            // Newell Method
            // This method also works for concave polygons, important for triangulation

            double normalX = 0, normalY = 0, normalZ = 0;
            for (int i = 0; verticesCount > i; i++)
            {
                var vertex1 = Vertices[i];
                var vertex2 = Vertices[(i + 1) % verticesCount];

                normalX += (vertex1.Y - vertex2.Y) * (vertex1.Z + vertex2.Z);
                normalY += (vertex1.Z - vertex2.Z) * (vertex1.X + vertex2.X);
                normalZ += (vertex1.X - vertex2.X) * (vertex1.Y + vertex2.Y);
            }

            var newellVec = new Vector(normalX, normalY, normalZ);
            double newellLen = newellVec.Length();
            _normal = newellLen > 1e-7
                ? new Vector(newellVec.X / newellLen, newellVec.Y / newellLen, newellVec.Z / newellLen)
                : Vector.Zero;
        }

        // This has to be more robust maybe, but this works for now
        public bool IsInvalid()
        {
            // Check if polygon has a normal
            if (Normal.NearlyEquals(Vector.Zero)) return true;

            return false;
        }

        public int GetBrushPolygonId()
        {
            if (Brush is null) return -1;
            return Brush.GetPolygonId(this);
        }

        #region Plane Stuff
        public Vector PlaneOrigin => _vertices[0];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double SignedDistance(Vector point) => Vector.Dot(Normal, point - PlaneOrigin);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPointBehindPlane(Vector point, double? eps = null) => SignedDistance(point) < (eps.HasValue ? -eps : -Vector.PlaneEpsilon); // SignedDistance(point) < -Vector.CoordinateEpsilon;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPointInFrontOfPlane(Vector point, double? eps = null) => SignedDistance(point) > (eps.HasValue ? eps : Vector.PlaneEpsilon); // SignedDistance(point) > Vector.CoordinateEpsilon;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPointOnPlane(Vector point, double? eps = null)
        {
            eps = eps.HasValue ? eps : Vector.PlaneEpsilon;

            var signedDistance = SignedDistance(point);

            return signedDistance >= -eps && signedDistance <= eps;
            //return signedDistance >= -Vector.CoordinateEpsilon && signedDistance <= Vector.CoordinateEpsilon;
        }
        #endregion
        #region Geometry Cleanup and Construction
        private bool GetPolygonCoordinates(Vector point, out double U, out double V)
        {
            // TODO: Cache
            var origin = Vertices[0];
            var polyU = (origin - Vertices[1]).GetNormalized();
            var polyV = Vector.Cross(Normal, polyU);

            U = Vector.Dot(point - origin, polyU);
            V = Vector.Dot(point - origin, polyV);
            return true;

        }

        private Vector GetWorldCoordinates(double U, double V)
        {
            // TODO: Cache
            var origin = Vertices[0];
            var polyU = (origin - Vertices[1]).GetNormalized();
            var polyV = Vector.Cross(Normal, polyU);

            return origin + polyU * U + polyV * V;
        }

        // To be optimized, or may be redundant after cap polygon creation is improved
        // This uses tiny epsilon on purpose. Iterative
        public int CleanupCollinearPoints()
        {
            int totalRemoved = 0;
            int initialCount = Vertices.Count;

            while (Vertices.Count >= 4)
            {
                int bestIdx = -1;
                double bestDistSq = double.MaxValue;
                Vector bestPrev = Vector.Zero, bestMid = Vector.Zero, bestNext = Vector.Zero;

                int verticesCount = Vertices.Count;
                for (int i = 0; verticesCount > i; i++)
                {
                    var vertex1 = Vertices[i];
                    int midIndex = (i + 1) % verticesCount;
                    var vertex2 = Vertices[midIndex];
                    var vertex3 = Vertices[(i + 2) % verticesCount];

                    if (!vertex2.IsOnLine(vertex1, vertex3)) continue;

                    var lineVec = vertex3 - vertex1;
                    var lineLenSq = lineVec.LengthSquared();
                    if (lineLenSq <= Vector.CoordinateEpsilonSquared) continue;
                    var pointVec = vertex2 - vertex1;
                    var fraction = Vector.Dot(pointVec, lineVec) / lineLenSq;
                    var closest = vertex1 + lineVec * fraction;
                    var distSq = vertex2.DistanceSquared(closest);

                    if (distSq > Vector.CollinearExactEpsilonSquared) continue;

                    if (distSq < bestDistSq)
                    {
                        bestDistSq = distSq;
                        bestIdx = midIndex;
                        bestPrev = vertex1; bestMid = vertex2; bestNext = vertex3;
                    }
                }

                if (bestIdx < 0) break;

                LogUTPolygon(this, LogLevel.DEBUG, "CleanupCollinearPoints",
                    $"Removing mid vertex {bestMid} (perp dist {Math.Sqrt(bestDistSq):F6}) from triple ({bestPrev} -> {bestMid} -> {bestNext})");

                _vertices.RemoveAt(bestIdx);
                totalRemoved++;
            }

            if (totalRemoved > 0)
                LogUTPolygon(this, LogLevel.DEBUG, "CleanupCollinearPoints",
                    $"Removed {totalRemoved} of {initialCount} vertices (iterative, smallest-dist-first)");

            return totalRemoved;
        }

        // Edges themselves are unordered
        // TODO: This is related to creating cap polygons after split. Should be improved later!
        public void GenerateVertexListFromEdges(List<Edge> edges)
        {
            var startEdge = edges[0];

            var startVertex = startEdge.StartPoint;
            var currentVertex = startEdge.EndPoint;

            List<Vector> vertices = new(edges.Capacity) { startVertex, currentVertex };
            for (int i = 1; edges.Count > i; i++)
            {
                var edge = edges[i];
                var edgeStart = edge.StartPoint;
                var edgeEnd = edge.EndPoint;

                var nextVertex = edgeStart.NearlyEquals(currentVertex) ? edgeEnd : edgeStart;
                if (nextVertex.NearlyEquals(startVertex)) break;

                vertices.Add(nextVertex);
                currentVertex = nextVertex;
            }

            _vertices = vertices;
        }

        // This is likely to make polygon look inward instead of outward the brush
        // Should find a way to fix this
        // But on the other hand: We actually need convex brushes to look inside themselves to be accepted by .vmf
        // So we also need to detect if the brush polygon looks inside the brush volume
        // May also need the check for concaves for repair pass
        // Since Unreal does not care about manifoldness rules
        public bool Transform()
        {
            if (Brush is null || Brush.Transformed) return false;

            var verticesCount = _vertices.Count;
            for (int vertexId = 0; verticesCount > vertexId; vertexId++)
            {
                Vector vertex = _vertices[vertexId];

                vertex -= Brush.PrePivot;

                vertex *= Brush.MainScale;

                vertex = vertex.GetRotatedFromUnrealAngle(Brush.Angle);

                vertex *= Brush.PostScale;

                vertex = Vector.GetSourcePositionFromUnrealPosition(vertex);

                // Brush position itself should already be transformed
                vertex += Brush.Position;

                _vertices[vertexId] = vertex;
            }

            _normal = Vector.Zero;

            return true;
        }

        public bool LooksInsideBrushVolume()
        {
            if (Brush is null)
            {
                LogUTPolygon(this, LogLevel.WARNING, "LooksInsideBrushVolume", "Polygon does not belong to any brush");
                return false;
            }

            // TODO: Raycasting? Can't really trust .t3d input since Unreal compiles any sort of shit as far as I know
            if (Brush.Type == UTBrushType.Concave)
            {
                LogUTPolygon(this, LogLevel.ERROR, "LooksInsideBrushVolume", "Brush is concave, can't determine");
                return false;
            }

            if (Brush.Type != UTBrushType.Convex)
            {
                LogUTPolygon(this, LogLevel.ERROR, "LooksInsideBrushVolume", "Brush is non-manifold, can't determine");
                return false;
            }

            // Convex Check (Tiny epsilon for tiny slivers)
            if (IsPointInFrontOfPlane(Brush.CenterPoint, 1e-7)) return true;

            return false;
        }

        #region Eberly Hole Bridging
        // This is a simplified version of Eberly Hole Bridging
        // Instead of finding a visible vertex on the outer loop
        // We just bridge to the first ray intersection point
        // Eberly finds a visible vertex to reduce messing more than needed with topology and escape FP errors
        // But I don't care about that, unless it'll cause problems :)

        // This is called in TryBridgeHole
        // TODO: Should get metadata for vertices in polygon
        private void BridgeHole(UTPolygon holeToBridge, int bridgeInnerPointIndex, Vector rayIntersectionPoint, Edge bridgeOuterEdge)
        {
            var bridgeEdgeStart = bridgeOuterEdge.StartPoint;
            var bridgeEdgeEnd = bridgeOuterEdge.EndPoint;

            var bridgeInnerPoint = holeToBridge.Vertices[bridgeInnerPointIndex];

            var intersectionPointIsEdgeStart = rayIntersectionPoint.NearlyEquals(bridgeEdgeStart);
            var intersectionPointIsEdgeEnd = rayIntersectionPoint.NearlyEquals(bridgeEdgeEnd);

            // Snap to escape inaccuracy
            rayIntersectionPoint = intersectionPointIsEdgeStart ? bridgeEdgeStart 
                                    : intersectionPointIsEdgeEnd ? bridgeEdgeEnd 
                                    : rayIntersectionPoint;

            // Find the bridge edge by matching BOTH endpoints as a pair
            // Looking up single vertex alone is unreliable once the polygon has accumulated duplicates
            int edgeStartIdx = -1;
            for (int i = 0; i < _vertices.Count; i++)
            {
                var nextI = (i + 1) % _vertices.Count;
                if (_vertices[i].NearlyEquals(bridgeOuterEdge.StartPoint)
                    && _vertices[nextI].NearlyEquals(bridgeOuterEdge.EndPoint))
                {
                    edgeStartIdx = i;
                    break;
                }
            }
            if (edgeStartIdx < 0) return;

            var insertIndex = (intersectionPointIsEdgeStart || !intersectionPointIsEdgeEnd)
                                ? edgeStartIdx + 1            // insert after StartPoint
                                : edgeStartIdx + 2;           // insert after EndPoint

            if (!intersectionPointIsEdgeStart && !intersectionPointIsEdgeEnd)
            {
                _vertices.Insert(insertIndex, rayIntersectionPoint);
                insertIndex++;
            }

            var holeVerticesCount = holeToBridge.Vertices.Count;
            for (int i = bridgeInnerPointIndex; i <= holeVerticesCount + bridgeInnerPointIndex; i++)
            {
                var vertex = holeToBridge.Vertices[i % holeVerticesCount];
                
                _vertices.Insert(insertIndex, vertex);
                insertIndex++;
            }

            _vertices.Insert(insertIndex, rayIntersectionPoint);
        }

        private Vector GetHoleRightmostVertex(UTPolygon hole)
        {
            double bestBridgeU = double.MinValue, bestBridgeV = double.MinValue;
            var bestBridgeVertex = Vector.Zero;
            for (int i = 0; hole.Vertices.Count > i; i++)
            {
                var holeVertex = hole.Vertices[i];
                GetPolygonCoordinates(holeVertex, out var vertexU, out var vertexV);

                // The most rightest vertex, with height being a secondary priority
                if (vertexU > bestBridgeU || vertexU == bestBridgeU && vertexV > bestBridgeV)
                {
                    bestBridgeU = vertexU;
                    bestBridgeV = vertexV;

                    bestBridgeVertex = holeVertex;
                }
            }

            return bestBridgeVertex;
        }

        public bool TryBridgeHoles(IEnumerable<UTPolygon> holesToBridge)
        {
            List<UTPolygon> holes = holesToBridge.ToList();

            // Descending rightness
            holes.Sort((x, y) =>
            {
                var xBridgeVertex = GetHoleRightmostVertex(x);
                var yBridgeVertex = GetHoleRightmostVertex(y);

                GetPolygonCoordinates(xBridgeVertex, out var xU, out var xV);
                GetPolygonCoordinates(yBridgeVertex, out var yU, out var yV);

                // TODO: Height matters?

                return yU.CompareTo(xU);
            });

            var originalGeometry = _vertices.ToList();
            var atLeastOneHoleBridged = false;
            foreach (var hole in holes)
            {
                if (!TryBridgeHole(hole))
                {
                    // TODO: More things to revert
                    if (atLeastOneHoleBridged)
                    {
                        _vertices = originalGeometry;
                    }

                    return false;
                }

                atLeastOneHoleBridged = true;
            }

            return true;
        }

        public bool TryBridgeHole(UTPolygon holeToBridge)
        {
            // In case of manifold closed volume we can perform a singular vertex test to determine if the hole loop is fully inside or fully outside
            if (IsInvalid() || holeToBridge.IsInvalid() || !holeToBridge.Normal.NearlyEquals(-Normal) || !IsPointInside(holeToBridge.Vertices[0])) return false;
            
            double bestBridgeU = double.MinValue, bestBridgeV = double.MinValue;
            var bestBridgeVertex = Vector.Zero;
            var bestBridgeVertexIndex = -1;
            for (int i = 0; holeToBridge.Vertices.Count > i; i++)
            {
                var holeVertex = holeToBridge.Vertices[i];
                GetPolygonCoordinates(holeVertex, out var vertexU, out var vertexV);

                // The most rightest vertex, with height being a secondary priority
                if (vertexU > bestBridgeU || vertexU == bestBridgeU && vertexV > bestBridgeV)
                {
                    bestBridgeU = vertexU;
                    bestBridgeV = vertexV;
                    bestBridgeVertex = holeVertex;
                    bestBridgeVertexIndex = i;
                }
            }

            RaycastRightFromPoint(bestBridgeVertex, out var intersections, strictRayIntersection: true);
            if (intersections.Count == 0) return false;

            // Get closest (first) edge intersection
            var intersectionPoint = intersections[0].Point;
            var intersectionPointVector = GetWorldCoordinates(intersectionPoint.U, intersectionPoint.V);

            var intersectionEdge = intersections[0].Edge;

            // TODO: Metadata for vertices... to get their IDs quick via hashset
            BridgeHole(holeToBridge, bestBridgeVertexIndex, intersectionPointVector, intersectionEdge);
            return true;
        }
        #endregion

        #endregion
        #region Collision
        // Works only for convex polygons. Same-side technique
        private bool IsPointInsideSimple(Vector point)
        {
            if (IsInvalid() || !IsPointOnPlane(point)) return false;

            var verticesCount = Vertices.Count;
            for (int i = 0; verticesCount > i; i++)
            {
                var vertex1 = Vertices[i];
                var vertex2 = Vertices[(i + 1) % verticesCount];

                var edge = vertex2 - vertex1;
                var toPoint = point - vertex1;

                var crossProduct = Vector.Cross(edge, toPoint);
                var dot = Vector.Dot(crossProduct, Normal);

                if (dot < -Vector.CoordinateEpsilon) return false;
            }

            return true;
        }

        private void RaycastRightFromPoint(Vector point, out List<((double U, double V) Point, Edge Edge)> intersections, bool strictRayIntersection = false)
        {
            intersections = new();
            if (IsInvalid() || !IsPointOnPlane(point)) return;

            GetPolygonCoordinates(point, out var pointU, out var pointV);
            foreach (var edge in GetEdges())
            {
                var edgeStart = edge.StartPoint;
                var edgeEnd = edge.EndPoint;

                GetPolygonCoordinates(edgeStart, out var edgeStartU, out var edgeStartV);
                GetPolygonCoordinates(edgeEnd, out var edgeEndU, out var edgeEndV);

                // Is the point below or above both edge endpoints? If so - no intersection
                // Half-open principle
                if ((edgeStartV > pointV) == (edgeEndV > pointV))
                {
                    if (!strictRayIntersection) continue;

                    // Bridging mode: still count if one vertex is on ray and the other is below
                    var startOnRay = Math.Abs(edgeStartV - pointV) <= Vector.CoordinateEpsilon;
                    var endOnRay = Math.Abs(edgeEndV - pointV) <= Vector.CoordinateEpsilon;
                    if (startOnRay == endOnRay) continue;
                }

                // Is the edge behind the point (on the left from the point)? If so - no intersection
                var intersectionU = edgeStartU + ((pointV - edgeStartV) / (edgeEndV - edgeStartV) * (edgeEndU - edgeStartU));
                if (pointU >= intersectionU)
                    continue;

                intersections.Add(((intersectionU, pointV), edge));
            }

            intersections.Sort((x, y) => x.Point.U.CompareTo(y.Point.U));
        }

        // This works for concave polygons. Raycast method
        public bool IsPointInside(Vector point)
        {
            RaycastRightFromPoint(point, out var intersections);
            return intersections.Count % 2 == 1;
        }
        #endregion
        #region Concavity
        private bool IsConcaveVertex(Vector prevVertex, Vector curVertex, Vector nextVertex)
        {
            var edge1 = curVertex - prevVertex;
            var edge2 = nextVertex - curVertex;

            var cross = Vector.Cross(edge1, edge2);
            var dot = Vector.Dot(cross, Normal);

            // CCW Order assumed
            return dot < -Vector.CoordinateEpsilon;
        }

        public bool IsConcave()
        {
            if (IsInvalid()) return false;

            var verticesCount = Vertices.Count;
            for (int i = 0; verticesCount > i; i++)
            {
                var vertex1 = Vertices[i];
                var vertex2 = Vertices[(i + 1) % verticesCount];
                var vertex3 = Vertices[(i + 2) % verticesCount];

                if (IsConcaveVertex(vertex1, vertex2, vertex3))
                    return true;
            }

            return false;
        }
        #endregion
        #region Triangulation
        private bool EarClip(out UTPolygon triangle)
        {
            triangle = new(Name);
            triangle.CopySettings(this);

            var verticesCount = Vertices.Count;
            if (verticesCount < 4) return false;

            for (int i = 0; verticesCount > i; i++)
            {
                var startVertex = Vertices[i];

                var middleVertexIndex = (i + 1) % verticesCount;
                var middleVertex = Vertices[middleVertexIndex];

                var endVertex = Vertices[(i + 2) % verticesCount];

                // The triangle tip must be strictly convex
                if (IsConcaveVertex(startVertex, middleVertex, endVertex)) continue;

                triangle.ClearVertices();
                triangle.PushVertex(startVertex);
                triangle.PushVertex(middleVertex);
                triangle.PushVertex(endVertex);

                // Normal couldn't be calculated
                if (triangle.IsInvalid()) continue;

                // If there are any other vertices inside - it's an invalid ear
                bool pointInside = false;
                foreach (var vertex in Vertices)
                {
                    if (vertex.NearlyEquals(startVertex) || vertex.NearlyEquals(middleVertex) || vertex.NearlyEquals(endVertex)) continue;
                    if (!triangle.IsPointInsideSimple(vertex)) continue;

                    LogUTPolygon(this, LogLevel.WARNING, "EarClip", $"Vertex {vertex} found inside Ear {startVertex} - {middleVertex} - {endVertex}. Skipping ear");

                    pointInside = true;
                    break;
                }

                if (pointInside) continue;

                // Cut off triangulated part of the polygon
                _vertices.RemoveAt(middleVertexIndex);
                LogUTPolygon(this, LogLevel.INFO, "EarClip", $"Found Valid Ear {startVertex} - {middleVertex} - {endVertex}. {Vertices.Count} Vertices left.");

                return true;
            }

            LogUTPolygon(this, LogLevel.ERROR, "EarClip", "Found no Valid Ear");
            return false;
        }

        public bool GetTriangulated(out List<UTPolygon> triangles)
        {
            triangles = new();
            if (IsInvalid()) return false;

            UTPolygon triangulationCopy = new(Name);
            triangulationCopy.Copy(this);

            while (triangulationCopy.EarClip(out var triangle))
                triangles.Add(triangle);

            var leftover = triangulationCopy.Vertices;
            bool hasArea = false;

            // Either a final triangle or collinear stall
            // I am disappointed in this, but this works for now
            // We have 2 epsilons for "identifying" non-collinearity. The one is PerpDistance and other is CollinearExact
            // Funny enough, PerpDistance was originally meant to deal with t-junctions in 3d space.
            // this is a mess
            if (leftover.Count > 2)
            {
                for (int i = 0; i < leftover.Count; i++)
                    if (Geometry.IsNonCollinear(leftover[i], leftover[(i + 1) % leftover.Count], leftover[(i + 2) % leftover.Count]))
                    {
                        hasArea = true;
                        break;
                    }
            }

            // Zero-area collinear leftover: holds no area - drop
            // Learned the hard way from CTF-Kosov Brush433
            if (!hasArea)
            {
                LogUTPolygon(this, LogLevel.WARNING, "GetTriangulated", $"Dropped zero-area collinear leftover of {leftover.Count} verts, kept {triangles.Count} triangle(s)");
                return true;
            }

            if (leftover.Count == 3)
            {
                triangles.Add(triangulationCopy);
                return true;
            }

            return false;
        }
        #endregion

        // TODO: Cache this on PushVertex calls
        public List<Edge> GetEdges()
        {
            var verticesCount = Vertices.Count;
            List<Edge> edges = new(verticesCount);
            for (int i = 0; verticesCount > i; i++)
            {
                var edgeStart = Vertices[i];
                var edgeEnd = Vertices[(i + 1) % verticesCount];

                edges.Add(new Edge(edgeStart, edgeEnd));
            }

            return edges;
        }

        public override string ToString() => $"{Brush?.Name} Polygon #{GetBrushPolygonId()} '{Name}' UTPolygon";

        //public override string ToString() => $"Polygon: Texture {Texture}, Vertices Count {Vertices.Count}, Normal {Normal}";
    }
}