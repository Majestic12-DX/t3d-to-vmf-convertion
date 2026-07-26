// This is a HUGE file that needs to be SPLIT once this is REWRITTEN TO BE MORE PROPER!!!

namespace UTModels
{
    public enum CSGOperation
    {
        Add, Subtract
    }

    public enum UTSheerAxis
    {
        None,
        XY,
        XZ,
        YX,
        YZ,
        ZX,
        ZY
    }

    public enum UTBrushType
    {
        Undetermined,
        Invalid,
        NonManifold,
        Convex,
        Concave
    }

    public class BrushActor : BaseActor
    {
        public CSGOperation Operation { get; set; } = CSGOperation.Add;

        private UTBrushType _type = UTBrushType.Undetermined;
        public UTBrushType Type
        {
            get
            {
                if (_type == UTBrushType.Undetermined) _type = CalculateBrushType();
                return _type;
            }
            private set => _type = value;
        }

        public UTSheerAxis MainSheerAxis { get; set; } = UTSheerAxis.ZX;
        public double MainSheerRate { get; set; } = 0;
        public Vector MainScale { get; set; } = new Vector(1, 1, 1);

        public UTSheerAxis PostSheerAxis { get; set; } = UTSheerAxis.ZX;
        public double PostSheerRate { get; set; } = 0;
        public Vector PostScale { get; set; } = new Vector(1, 1, 1);

        public Vector PrePivot { get; set; } = new Vector();

        public List<UTPolygon> Polygons { get; set; } = new();
        public string Model { get; set; } = string.Empty;

        // Debug Info
        public Plane? ClipperPlane { get; set; }

        // Meta Data
        public bool Transformed = false;

        private double _appliedScale = 1.0;
        public double AppliedScale => _appliedScale;

        private Vector _centerPoint = Vector.Zero;
        public Vector CenterPoint { 
            get
            {
                if (_centerPoint.NearlyEquals(Vector.Zero)) CalculateCenterPoint();
                return _centerPoint;
            }
            private set => _centerPoint = value; 
        }

        protected virtual BrushActor CreateEmpty() => new BrushActor();

        protected override bool CopyDetails(BaseActor actorToCopy)
        {
            if (actorToCopy is not BrushActor brushToCopy) return false;

            Operation = brushToCopy.Operation;

            MainSheerAxis = brushToCopy.MainSheerAxis;
            MainSheerRate = brushToCopy.MainSheerRate;
            MainScale = brushToCopy.MainScale;

            PostSheerAxis = brushToCopy.PostSheerAxis;
            PostSheerRate = brushToCopy.PostSheerRate;
            PostScale = brushToCopy.PostScale;

            PrePivot = brushToCopy.PrePivot;

            Model = brushToCopy.Model;

            ClipperPlane = brushToCopy.ClipperPlane;

            Transformed = brushToCopy.Transformed;

            _appliedScale = brushToCopy._appliedScale;

            return true;
        }

        public bool CopyPolygons(BrushActor brushToCopy)
        {
            if (brushToCopy is null) return false;

            var polygonsToCopy = brushToCopy.Polygons;
            if (polygonsToCopy.Count == 0) return false;

            Polygons.Clear();
            foreach (var polygonToCopy in polygonsToCopy)
            {
                var copyPolygon = new UTPolygon(polygonToCopy.Name);
                copyPolygon.Copy(polygonToCopy);

                AddPolygon(copyPolygon);
            }

            Type = brushToCopy.Type;

            return true;
        }

        // This is bad
        public int GetPolygonId(UTPolygon polygonToFind)
        {
            if (polygonToFind.Brush != this) return -1;

            var polygonCount = Polygons.Count;
            for (int i = 0; polygonCount > i; i++)
            {
                var polygon = Polygons[i];
                if (polygon == polygonToFind) return i;
            }

            return -1;
        }

        public bool AddPolygon(UTPolygon polygon)
        {
            if (polygon.Brush is not null)
            {
                // add logging
                return false;
            }

            Polygons.Add(polygon);
            polygon.Brush = this;

            CenterPoint = Vector.Zero; // Invalidate Center Point

            return true;
        }

        #region Calculating Brush Type
        // TODO: This is horrendous
        public List<Vector> GetAllUniqueVertices()
        {
            var vertices = new HashSet<Vector>(new VectorComparer());
            foreach (var polygon in Polygons)
                foreach (var vertex in polygon.Vertices)
                    vertices.Add(vertex);

            return vertices.ToList();
        }

        // Each face plane is polygon vertex #1 + its own normal.
        public List<Plane>? GetPlanes()
        {
            List<Plane> planes = new();
            for (int i = 0; Polygons.Count > i; i++)
            {
                var polygon = Polygons[i];
                var normal = polygon.Normal;
                if (normal.NearlyEquals(Vector.Zero))
                {
                    Logger.Write(LogLevel.ERROR, $"BrushActor.GetPlanes: Normal couldn't be calculated for {Name}'s polygon #{i}");
                    return null;
                }
                planes.Add(new Plane(polygon.PlaneOrigin, normal));
            }
            return planes;
        }

        // This solves T junctions being counted as non manifolds
        // Returns list with main edge inside if it does not consist of sub-edges
        private List<Edge> GetEdgeDecomposed(Edge mainEdge)
        {
            List<Edge> edgeParts = new();

            var pointsOnLine = new HashSet<Vector>(new VectorComparer())
            {
                mainEdge.StartPoint,
                mainEdge.EndPoint
            };

            var vertices = GetAllUniqueVertices();
            foreach (var vertex in vertices)
                if (vertex.IsOnLine(mainEdge.StartPoint, mainEdge.EndPoint))
                    pointsOnLine.Add(vertex);

            if (pointsOnLine.Count == 2)
            {
                edgeParts.Add(mainEdge);
                return edgeParts;
            }

            // Order them for correct sub-edge construction
            var sortedPointsOnLine = pointsOnLine
                .OrderBy(v => v.Distance(mainEdge.StartPoint))
                .ToList();

            // Construct sub-edges
            for (int i = 0; sortedPointsOnLine.Count - 1 > i; i++)
            {
                Vector vec1 = sortedPointsOnLine[i];
                Vector vec2 = sortedPointsOnLine[i + 1];

                Edge newEdge = new(vec1, vec2);
                edgeParts.Add(newEdge);
            }

            return edgeParts;
        }

        private List<Edge> GetDecomposedEdges(UTPolygon polygon)
        {
            HashSet<Edge> edges = new(new EdgeComparer());

            int vertexCount = polygon.Vertices.Count;
            for (int i = 0; vertexCount > i; i++)
            {
                Vector vec1 = polygon.Vertices[i];
                Vector vec2 = polygon.Vertices[(i + 1) % vertexCount];

                if (vec1.NearlyEquals(vec2)) continue;

                var newEdge = new Edge(vec1, vec2);
                var decomposedEdge = GetEdgeDecomposed(newEdge);
                foreach (var edge in decomposedEdge)
                    edges.Add(edge);
            }

            return edges.ToList();
        }

        // Returns edges which are used only once. Which means non-watertight geometry
        // oddCountIsBoundary is a temp crutch for brushes like CityIntro Brush220/331
        // They have internal and penetrating faces that overlap on each other
        private List<(Edge Edge, int PolygonId)> GetNonManifoldEdges(bool oddCountIsBoundary = false)
        {
            Dictionary<Edge, (int UseCount, int PolygonId)> edgeUseCounts = new(new EdgeComparer());
            for (int i = 0; Polygons.Count > i; i++)
            {
                var polygon = Polygons[i];
                foreach (var edge in GetDecomposedEdges(polygon))
                    if (edgeUseCounts.TryGetValue(edge, out var edgeData))
                    {
                        edgeData.UseCount++;
                        edgeUseCounts[edge] = edgeData;
                    }
                    else
                        edgeUseCounts[edge] = (UseCount: 1, PolygonId: i);
            }

            List<(Edge Edge, int PolygonId)> nonManifoldEdges = new();
            foreach (var edgeProfile in edgeUseCounts)
            {
                var edge = edgeProfile.Key;
                var edgeData = edgeProfile.Value;

                if (edgeData.UseCount < 2 || (oddCountIsBoundary && edgeData.UseCount % 2 == 1))
                    nonManifoldEdges.Add((edge, edgeData.PolygonId));
            }

            return nonManifoldEdges;
        }

        // This exists because we handle different types of brushes differently
        // Source only allows convex brushes
        // While unreal allows plain ass polygons, convexes and concaves
        private void LogCalculateBrushType(LogLevel level, string message)
        {
            Logger.Write(level, $"BrushActor.CalculateBrushType({Name}): {message}");
        }
        private UTBrushType CalculateBrushType()
        {
            // If brush has any poly with less than 3 vertices it's a pure degenerate
            for (int i = 0; Polygons.Count > i; i++)
                if (Polygons[i].Vertices.Count < 3)
                {
                    LogCalculateBrushType(LogLevel.DEBUG, $"Polygon #{i} has less than 3 vertices. This brush is invalid");
                    return UTBrushType.Invalid; 
                }

            // Not enough polygons for a closed volume
            if (Polygons.Count < 4)
            {
                LogCalculateBrushType(LogLevel.DEBUG, "Less than 4 polygons. This brush is non manifold");
                return UTBrushType.NonManifold;
            }

            // Firstly we have to check if its a non manifold
            // A non manifold geometry has at least 1 edge that is used only once
            // If there's any point on the edge, it means it consists of sub-edges
            // An edge with points on it is decomposed into those subedges
            // This is for t junctions to not be counted as non manifolds
            var nonManifoldEdges = GetNonManifoldEdges();
            foreach (var nonManifoldEdgeData in nonManifoldEdges)
            {
                var nonManifoldEdge = nonManifoldEdgeData.Edge;
                var polygonId = nonManifoldEdgeData.PolygonId;

                LogCalculateBrushType(LogLevel.DEBUG, $"Edge {nonManifoldEdge} from polygon {polygonId} is used only once. This brush is non manifold");
            }

            if (nonManifoldEdges.Count > 0)
            {
                return UTBrushType.NonManifold;
            }

            // Check if this is a convex. If not - it's a concave shape
            // Convex can be described as having all vertices behind or on each brush poly
            foreach (var vertex in GetAllUniqueVertices())
                for (int i = 0; Polygons.Count > i; i++)
                {
                    var polygon = Polygons[i];

                    // Convex classification uses its own tolerance, not PlaneEpsilon.
                    // This is temporary.. as most of this code
                    var signedDistance = polygon.SignedDistance(vertex);
                    if (signedDistance > Vector.ConvexEpsilon)
                    {
                        LogCalculateBrushType(LogLevel.DEBUG, $"Vertex {vertex} is in front of polygon {i} by {signedDistance}. This brush is concave");
                        return UTBrushType.Concave;
                    }
                }

            LogCalculateBrushType(LogLevel.DEBUG, $"All vertices behind each face, this is a convex");
            return UTBrushType.Convex;
        }
        #endregion
        #region Decomposing Concave into Convexes
        // 3D Sutherland Hogdman clipping to decompose concaves into convexes, recursively driven by BSP Plane selection
        // This whole thing honestly has to be put into its own folder. First thing that will be done during major refactor
        private enum RelativeVertexPosition
        {
            BehindPlane,
            InFrontOfPlane,
            OnPlane
        }

        private readonly struct VertexPlanePosition
        {
            // PlaneEpsilon (0.1) is a lot more unstable than CoordinateEpsilon (0.01)
            // CoordinateEpsilon is the current standard, however, PlaneEpsilon prevents thin slivers
            // So it's being experimented with to make it work better, on par with CoordinateEpsilon
            // Update: EXACT math would prevent thin slivers.
            private static readonly double _eps = Vector.SplitPolygonByPlaneEpsilon;
            public RelativeVertexPosition RelativePosition { get; init; }
            public double SignedDistance { get; init; }
            public VertexPlanePosition(double signedDistance)
            {
                SignedDistance = signedDistance;

                if (signedDistance > _eps)
                    RelativePosition = RelativeVertexPosition.InFrontOfPlane;
                else if (signedDistance < -_eps)
                    RelativePosition = RelativeVertexPosition.BehindPlane;
                else
                    RelativePosition = RelativeVertexPosition.OnPlane;
            }
        }

        private enum VertexType
        {
            Vertex,
            Intersection,
            EdgePoint
        }

        private void LogConcavePartSplitByPlane(LogLevel level, string message) =>
            Logger.Write(level, $"BrushActor.GetConcavePartSplitByPlane({Name}): {message}");

        // Post processing is highly unoptimized and needs to be optimized afterwards
        // Builds an in-plane tangent for a unit normal: pick the world axis least
        // aligned with normal, project out its normal component, normalize. Used by the
        // cap-walker to compute angular positions of cap edges in the cap plane
        private static Vector ChooseTangentForPlane(Vector normal)
        {
            double absX = Math.Abs(normal.X);
            double absY = Math.Abs(normal.Y);
            double absZ = Math.Abs(normal.Z);

            Vector axis = (absX <= absY && absX <= absZ)
                ? new Vector(1, 0, 0)
                : (absY <= absZ ? new Vector(0, 1, 0) : new Vector(0, 0, 1));

            // Gram-Schmidt: subtract the component along normal, then normalize
            double align = Vector.Dot(axis, normal);

            Vector tangent = new Vector(axis.X - normal.X * align, axis.Y - normal.Y * align, axis.Z - normal.Z * align);
            return tangent.GetNormalized();
        }

        // Picks the left-most (CCW around referenceNormal) directed edge to be walked by cap polygon construction walker
        // This may cause problems if we have an hour glass out of 2 CW loops (supposed to be holes, merged into one degenerate because of CCW prioritization no matter what)
        // Hasn't happened for now
        // This returns -1 if there's no edge to walk
        private static int SelectNextEdgeByLeftTurn(List<Edge> unusedEdges, Vector currentVertex, Vector prevVertex, Vector referenceNormal)
        {
            // First pass: cheap candidate scan. If exactly one candidate, return it.
            int firstIdx = -1;
            int candidateCount = 0;
            for (int i = 0; i < unusedEdges.Count; i++)
            {
                if (!unusedEdges[i].StartPoint.NearlyEquals(currentVertex)) continue;
                if (firstIdx == -1) firstIdx = i;
                if (++candidateCount > 1) break;
            }
            if (candidateCount <= 1) return firstIdx;

            // Build a planar tangent frame in the cap plane and pick the leftmost directed edge
            Vector tangent = ChooseTangentForPlane(referenceNormal);
            Vector bitangent = Vector.Cross(referenceNormal, tangent);

            Vector inDir = (currentVertex - prevVertex).GetNormalized();
            double inAngle = Math.Atan2(Vector.Dot(inDir, bitangent), Vector.Dot(inDir, tangent));

            int bestIdx = -1;
            double bestKey = double.MaxValue;
            const double tau = 2 * Math.PI;

            for (int i = 0; i < unusedEdges.Count; i++)
            {
                var edge = unusedEdges[i];
                if (!edge.StartPoint.NearlyEquals(currentVertex)) continue;

                Vector outDir = (edge.EndPoint - currentVertex).GetNormalized();
                double outAngle = Math.Atan2(Vector.Dot(outDir, bitangent), Vector.Dot(outDir, tangent));

                // Delta = CCW rotation from incoming to outgoing, in [0, 2pi)
                double delta = (outAngle - inAngle + tau) % tau;
                // Key = (pi - delta) mod 2pi. Smallest key wins:
                // Delta near pi (sharp left, just shy of U-turn) → key near 0 (BEST)
                // Delta near 2pi or near 0 (gentle right or near-straight) → key near pi (worst)
                // All left turns (delta in (0, pi)) get key in [0, pi)
                // All right turns (delta in (pi, 2pi)) get key in (pi, 2pi)
                // So lefts are always preferred.
                double key = (Math.PI - delta + tau) % tau;

                if (key < bestKey)
                {
                    bestKey = key;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }

        // This is bad but it works
        // Again, will be a lot better after the refactoring
        // This still is a great journey for me to learn from
        private bool GetConcavePartSplitByPlane(Plane clippingPlane, Dictionary<Vector, VertexPlanePosition> assortedVertices, bool isFront, out BrushActor splitPartBrush)
        {
            splitPartBrush = CreateEmpty();

            // Non manifold shouldn't be allowed here, really...
            // Will remove non manifold allowance once this is properly implemented
            if (Type != UTBrushType.Concave && Type != UTBrushType.NonManifold)
            {
                LogConcavePartSplitByPlane(LogLevel.WARNING, $"Tried calling, but brush is type is {Type} instead of Concave or Non Manifold");
                return false;
            }

            string polygonType = isFront ? "FS" : "BS";
            string splitName = $"{Name}_{polygonType}";

            HashSet<Vector> intersectionVertices = new(new VectorComparer());

            List<UTPolygon> splitPartPolygons = new();

            HashSet<Edge> intersectionEdges = new(new EdgeComparer());

            // This fixes the inconsistency within current VectorComparer (it's stupid and very old)
            // Vectors may be nearly equal but if they produce different hash due to grid snapping they are treated as separate
            // This removes grid snapping because grid snapping is unnecessary
            // This fixes Brush17 from DM-Deck17][ custom map
            List<Vector> canonicalVertices = new();
            Vector Canonicalize(Vector v)
            {
                foreach (var canonical in canonicalVertices)
                    if (canonical.NearlyEquals(v))
                        return canonical;

                canonicalVertices.Add(v);
                return v;
            }

            // For triple plane intersection point calculation
            // Raw edges only (no decomposition) - finds no partner and falls back to interpolation
            // needs to be improved
            Dictionary<Edge, (int First, int Second)> edgeOwnerPairs = new(new EdgeComparer());
            for (int ownerId = 0; Polygons.Count > ownerId; ownerId++)
            {
                var ownerVerts = Polygons[ownerId].Vertices;
                for (int vi = 0; ownerVerts.Count > vi; vi++)
                {
                    var rawEdge = new Edge(ownerVerts[vi], ownerVerts[(vi + 1) % ownerVerts.Count]);
                    if (!rawEdge.IsValid()) continue;
                    if (edgeOwnerPairs.TryGetValue(rawEdge, out var pair))
                        edgeOwnerPairs[rawEdge] = (pair.First, ownerId);
                    else
                        edgeOwnerPairs[rawEdge] = (ownerId, -1);
                }
            }

            #region Helper Functions
            bool IsVertexInsidePlane(RelativeVertexPosition vertexPos)
                    => vertexPos == RelativeVertexPosition.OnPlane || vertexPos == (isFront ? RelativeVertexPosition.InFrontOfPlane : RelativeVertexPosition.BehindPlane);

            Vector GetIntersectionPoint(Vector edgeStart, Vector edgeEnd, double distanceToStart, double distanceToEnd)
            {
                double denominator = distanceToStart - distanceToEnd;

                if (Math.Abs(denominator) < Vector.ShortEdgeEpsilon) return edgeStart;
                double intersectionFraction = distanceToStart / denominator;

                Vector intersectionPoint = edgeStart + intersectionFraction * (edgeEnd - edgeStart);

                Logger.Write(LogLevel.DEBUG, $"Calculated intersection point {intersectionPoint} for {edgeStart} - {edgeEnd} edge" +
                    $"(distance to start {distanceToStart}, distance to end {distanceToEnd}). Brush {splitName}, Plane {clippingPlane}");
                return intersectionPoint;
            };


            // This is a crutchified version of something that should become the norm
            // Plane intersections dictate the vertex position rather than simple plane intersection, used before, carries more error the deeper you split
            // Uses planes of 2 polys that used the edge and the splitting plane itself
            // Fixes Brush17 DM-Deck17][ custom map
            Vector GetCrossingPoint(int polyId, Vector edgeStart, Vector edgeEnd, double distanceToStart, double distanceToEnd)
            {
                Vector interpolated = GetIntersectionPoint(edgeStart, edgeEnd, distanceToStart, distanceToEnd);

                // Temporary A/B gate for corpus testing - remove after validation
                if (Environment.GetEnvironmentVariable("T3D_NO_TRIPLE") is not null) return interpolated;

                if (!edgeOwnerPairs.TryGetValue(new Edge(edgeStart, edgeEnd), out var owners)) return interpolated;
                int otherId = owners.First == polyId ? owners.Second : (owners.Second == polyId ? owners.First : -1);
                if (otherId < 0) return interpolated;

                var p = Polygons[polyId];
                var q = Polygons[otherId];

                Vector np = p.Normal, nq = q.Normal, nc = clippingPlane.Normal;
                double dp = Vector.Dot(np, p.PlaneOrigin);
                double dq = Vector.Dot(nq, q.PlaneOrigin);
                double dc = clippingPlane.Distance();

                Vector qxc = Vector.Cross(nq, nc);
                double det = Vector.Dot(np, qxc);

                if (Math.Abs(det) < 1e-6) return interpolated;

                Vector meet = (dp * qxc + dq * Vector.Cross(nc, np) + dc * Vector.Cross(np, nq)) / det;

                // Warped-face safety + blast-radius limiter: a non-planar source face makes
                // its plane a lie, and large corrections reroute healthy decomposition trees
                // (corpus-measured at bound 1.0: Orbital +2 NM, Deck17 +1). The kernel's job
                // is only to collapse noise-scale ghost twins (0.026-0.03 measured), so accept
                // the algebraic corner only when it agrees with the edge to noise scale.
                if ((meet - interpolated).Length() > 0.05) return interpolated;

                return meet;
            };

            void AddVertex(List<Vector> polygonVertices, Vector vertex, int polyId, int vertexId, VertexType vertexType = VertexType.Vertex)
            {
                foreach (var polygonVertex in polygonVertices)
                    if (polygonVertex.NearlyEquals(vertex))
                    {
                        LogConcavePartSplitByPlane(LogLevel.WARNING, $"Tried adding copy vertex {vertexId} from polygon {polyId}");
                        return;
                    }

                polygonVertices.Add(vertex);

                string logMessage = vertexType switch
                {
                    VertexType.Vertex => $"Added polygon {polyId} vertex {vertexId} to split part brush",
                    VertexType.Intersection => $"Added polygon {polyId} intersection point {vertex} to split part brush",
                    VertexType.EdgePoint => $"Marked polygon {polyId} vertex {vertexId} as potential cap edge vertex",
                    _ => $"Unknown vertex type polygon {polyId} vertex {vertexId}"
                };

                LogConcavePartSplitByPlane(LogLevel.DEBUG, logMessage);
            };

            void AddVertexToPolygon(UTPolygon polygon, Vector vertex, int polyId, int vertexId, VertexType vertexType = VertexType.Vertex)
            {
                vertex = Canonicalize(vertex);

                if (!polygon.PushVertex(vertex)) return;

                string logMessage = vertexType switch
                {
                    VertexType.Vertex => $"Added polygon {polyId} vertex {vertexId} to split part brush",
                    VertexType.Intersection => $"Added polygon {polyId} intersection point {vertex} to split part brush",
                    VertexType.EdgePoint => $"Marked polygon {polyId} vertex {vertexId} as potential cap edge vertex",
                    _ => $"Unknown vertex type polygon {polyId} vertex {vertexId}"
                };

                LogConcavePartSplitByPlane(LogLevel.DEBUG, logMessage);
            };

            bool CoplanarPolygonHasCorrectDirection(UTPolygon polygon)
            {
                var sameDirectionAsPlane = Vector.Dot(polygon.Normal, clippingPlane.Normal) > 0;
                return sameDirectionAsPlane && !isFront || !sameDirectionAsPlane && isFront;
            }
            #endregion

            splitPartBrush.Copy(this);
            splitPartBrush.Name = splitName;

            splitPartBrush.Polygons = splitPartPolygons;
            splitPartBrush.ClipperPlane = isFront ? -clippingPlane : clippingPlane;

            #region Assigning Vertices
            for (int polyId = 0; Polygons.Count > polyId; polyId++)
            {
                UTPolygon polygon = Polygons[polyId];

                UTPolygon clippedPolygon = new(polygon.Name);
                clippedPolygon.CopySettings(polygon);
                clippedPolygon.Normal = polygon.Normal;

                List<Vector> polygonIntersectionVertices = new();

                #region Sutherland Hodgman 3D brush clipping
                var vertices = polygon.Vertices;
                if (vertices.All(v => assortedVertices[v].RelativePosition == RelativeVertexPosition.OnPlane))
                {
                    var sameDirectionAsPlane = Vector.Dot(polygon.Normal, clippingPlane.Normal) > 0;
                    var shouldAdd = sameDirectionAsPlane && !isFront || !sameDirectionAsPlane && isFront;

                    if (shouldAdd)
                    {
                        foreach (var v in polygon.Vertices)
                            clippedPolygon.PushVertex(Canonicalize(v));
                        splitPartBrush.AddPolygon(clippedPolygon);
                    }

                    continue;
                }

                var verticesCount = vertices.Count;
                for (int vertexId = 0; verticesCount > vertexId; vertexId++)
                {
                    Vector edgeStart = vertices[vertexId];
                    Vector edgeEnd = vertices[(vertexId + 1) % verticesCount];

                    var edgeStartData = assortedVertices[edgeStart];
                    var edgeEndData = assortedVertices[edgeEnd];

                    var edgeStartPos = edgeStartData.RelativePosition;
                    var edgeEndPos = edgeEndData.RelativePosition;

                    bool isStartInside = IsVertexInsidePlane(edgeStartPos);
                    bool isEndInside = IsVertexInsidePlane(edgeEndPos);

                    if (!isStartInside && !isEndInside) continue;

                    if (isStartInside && isEndInside)
                    {
                        AddVertexToPolygon(clippedPolygon, edgeEnd, polyId, vertexId);
                        continue;
                    }

                    double distanceToStart = edgeStartData.SignedDistance;
                    double distanceToEnd = edgeEndData.SignedDistance;

                    if (isStartInside && !isEndInside)
                    {
                        Vector intersectionPoint = edgeStartPos == RelativeVertexPosition.OnPlane
                            ? edgeStart
                            : GetCrossingPoint(polyId, edgeStart, edgeEnd, distanceToStart, distanceToEnd);

                        intersectionPoint = Canonicalize(intersectionPoint);

                        AddVertexToPolygon(clippedPolygon, intersectionPoint, polyId, vertexId, VertexType.Intersection);
                        AddVertex(polygonIntersectionVertices, intersectionPoint, polyId, vertexId, VertexType.EdgePoint);

                        intersectionVertices.Add(intersectionPoint);
                    }
                    else if (!isStartInside && isEndInside)
                    {
                        Vector intersectionPoint = edgeEndPos == RelativeVertexPosition.OnPlane
                            ? edgeEnd
                            : GetCrossingPoint(polyId, edgeEnd, edgeStart, distanceToEnd, distanceToStart);

                        intersectionPoint = Canonicalize(intersectionPoint);

                        AddVertexToPolygon(clippedPolygon, intersectionPoint, polyId, vertexId, VertexType.Intersection);
                        AddVertex(polygonIntersectionVertices, intersectionPoint, polyId, vertexId, VertexType.EdgePoint);

                        intersectionVertices.Add(intersectionPoint);

                        //if (!intersectionPoint.NearlyEquals(edgeEnd))
                            AddVertexToPolygon(clippedPolygon, edgeEnd, polyId, vertexId);
                    }
                }
                #endregion
                #region Adding Valid Polygons

                // If all vertices became coplanar after splitting a polygon, it's a weird degenerate - ignore
                // Helped Brush433 CTF-Kosov
                bool remnantAllInBand = clippedPolygon.Vertices.Count > 2;
                if (remnantAllInBand)
                    foreach (var remnantVertex in clippedPolygon.Vertices)
                        if (Math.Abs(clippingPlane.SignedDistance(remnantVertex)) > Vector.SplitPolygonByPlaneEpsilon)
                        {
                            remnantAllInBand = false;
                            break;
                        }

                if (remnantAllInBand)
                    LogConcavePartSplitByPlane(LogLevel.DEBUG, $"Skipping band-sliver remnant of polygon {polyId} ('{polygon.Name}', {clippedPolygon.Vertices.Count}v) - no real area on this side");
                else if (clippedPolygon.Vertices.Count > 2)
                {
                    splitPartBrush.AddPolygon(clippedPolygon);

                    LogConcavePartSplitByPlane(LogLevel.DEBUG, $"Filled up split {polygonType} polygon {splitPartPolygons.Count - 1} from polygon {polyId}");

                }
                else
                    LogConcavePartSplitByPlane(LogLevel.WARNING, $"Failed to fill up split {polygonType} polygon for polygon {polyId}");
                #endregion
                #region Adding Intersection Cap Edges
                // Unreal Polygons are convex-only, therefore they can intersect the plane only 0 or 2 times
                // UPD: No they are not only convex. Encountered: Planar, Concave. Suspecting: Self-intersecting
                if (polygonIntersectionVertices.Count == 2)
                {
                    LogConcavePartSplitByPlane(LogLevel.DEBUG, $"Found {polygonIntersectionVertices.Count} intersecting vertices for polygon {polyId}");

                    var edgeStart = polygonIntersectionVertices[0];
                    var edgeEnd = polygonIntersectionVertices[1];
                    var edge = new Edge(edgeStart, edgeEnd) { CapEdgeOrigin = (polygon, polyId) };

                    intersectionEdges.Add(edge);
                }
                else if (polygonIntersectionVertices.Count > 2 || polygonIntersectionVertices.Count == 1)
                    LogConcavePartSplitByPlane(LogLevel.WARNING, $"Found {polygonIntersectionVertices.Count} intersecting vertices for polygon {polyId}?");
                #endregion
            }
            #endregion

            #region Building Cap Polygon(s) out of Edges used only once
            Vector capReferenceNormal = isFront ? new Vector(-clippingPlane.Normal.X, -clippingPlane.Normal.Y, -clippingPlane.Normal.Z) : clippingPlane.Normal;

            List<Edge> capEdges = new();
            foreach (var edgeData in splitPartBrush.GetNonManifoldEdges(oddCountIsBoundary: true))
            {
                var edge = edgeData.Edge;
                var edgeStart = edge.StartPoint;
                var edgeEnd = edge.EndPoint;

                // Make plane thickness match the splitting epsilon
                if (!clippingPlane.IsPointOnPlane(edgeStart, Vector.SplitPolygonByPlaneEpsilon)
                    || !clippingPlane.IsPointOnPlane(edgeEnd, Vector.SplitPolygonByPlaneEpsilon)) continue;
                var capEdge = new Edge(edgeEnd, edgeStart);

                capEdges.Add(capEdge);

                LogConcavePartSplitByPlane(LogLevel.INFO, $"Cap Edge found: {edge}");
            }

            // First pass: walk all closed loops and build them into polygons. Classify each as
            // an outer cap (fills brush material) or as a hole (subtraction from an outer cap).
            // CoplanarPolygonHasCorrectDirection returns true if we get the outer case. Opposite is a hole

            List<UTPolygon> outerCapPolygons = new();
            List<UTPolygon> holeCapPolygons = new();

            int capIndex = 0;
            var unusedEdges = capEdges.ToList();
            while (unusedEdges.Count > 0)
            {
                var startEdge = unusedEdges[0];
                unusedEdges.RemoveAt(0);

                Vector startVertex = startEdge.StartPoint;
                Vector currentVertex = startEdge.EndPoint;
                Vector prevVertex = startVertex;

                List<Edge> polygonEdges = new() { startEdge };

                bool isClosedLoop = false;
                while (!isClosedLoop)
                {
                    var nextEdgeIndex = SelectNextEdgeByLeftTurn(unusedEdges, currentVertex, prevVertex, capReferenceNormal);
                    if (nextEdgeIndex == -1) break;

                    var nextEdge = unusedEdges[nextEdgeIndex];
                    unusedEdges.RemoveAt(nextEdgeIndex);

                    polygonEdges.Add(nextEdge);

                    prevVertex = currentVertex;
                    currentVertex = nextEdge.EndPoint;
                    if (currentVertex.NearlyEquals(startVertex))
                        isClosedLoop = true;
                }

                if (!isClosedLoop)
                {
                    LogConcavePartSplitByPlane(LogLevel.ERROR, $"Couldn't find closed loop for {startEdge}");
                    continue;
                }

                if (polygonEdges.Count < 3)
                {
                    LogConcavePartSplitByPlane(LogLevel.ERROR, $"Closed loop for {startEdge} has less than 3 edges");
                    continue;
                }

                LogConcavePartSplitByPlane(LogLevel.INFO, $"Found valid closed loop for {startEdge}");

                UTPolygon polygon = new($"{splitName}_Cap{capIndex++}");
                polygon.GenerateVertexListFromEdges(polygonEdges);

                if (polygon.IsInvalid())
                {
                    LogConcavePartSplitByPlane(LogLevel.ERROR, $"Invalid closed loop polygon for {startEdge}");
                    continue;
                }

                polygon.CleanupCollinearPoints();

                if (CoplanarPolygonHasCorrectDirection(polygon))
                {
                    LogConcavePartSplitByPlane(LogLevel.INFO, $"Loop for {startEdge} classified as OUTER cap polygon");
                    outerCapPolygons.Add(polygon);
                }
                else
                {
                    LogConcavePartSplitByPlane(LogLevel.INFO, $"Loop for {startEdge} classified as HOLE cap polygon");
                    holeCapPolygons.Add(polygon);
                }
            }

            Dictionary<UTPolygon, List<UTPolygon>> holesByOuter = new();
            foreach (var outer in outerCapPolygons)
                holesByOuter[outer] = new();

            foreach (var hole in holeCapPolygons)
            {
                // In a closed manifold the holes are either fully outside or fully inside any outer
                // Same goes for outers inside holes
                var testVertex = hole.Vertices[0];

                UTPolygon? innermostOuter = null;
                foreach (var candidate in outerCapPolygons)
                {
                    if (!candidate.IsPointInside(testVertex)) continue;

                    // The innermost candidate must not contain any other outer that also contains the hole
                    // This is for nested holes
                    if (outerCapPolygons.Any(other =>
                            other != candidate
                            && other.IsPointInside(testVertex)
                            && candidate.IsPointInside(other.Vertices[0])))
                        continue;

                    innermostOuter = candidate;
                    break;
                }

                if (innermostOuter is null)
                {
                    // This is weird and is probably an outer, not a hole
                    hole.ReverseVertexOrder();
                    outerCapPolygons.Add(hole);
                    holesByOuter[hole] = new();
                    LogConcavePartSplitByPlane(LogLevel.WARNING, $"Hole cap polygon (Test Vertex {testVertex}) has no containing outer - promoting to OUTER island cap");
                    continue;
                }

                holesByOuter[innermostOuter].Add(hole);
                LogConcavePartSplitByPlane(LogLevel.INFO, $"Paired hole cap polygon (Test Vertex {testVertex}) into its innermost containing outer");
            }

            // Second pass: Bridge holes
            foreach (var outerPolygon in outerCapPolygons)
            {
                var contained = holesByOuter[outerPolygon];

                if (contained.Count > 0)
                {
                    if (!outerPolygon.TryBridgeHoles(contained))
                    {
                        LogConcavePartSplitByPlane(LogLevel.ERROR, $"Failed to bridge {contained.Count} hole(s) into outer cap polygon - cap will be left out of split brush");
                        continue;
                    }
                    LogConcavePartSplitByPlane(LogLevel.INFO, $"Bridged {contained.Count} hole(s) into outer cap polygon");
                }

                // Plane provenance: a committed outer cap lies in the cut plane and faces
                // cap-outward by definition, so it carries the EXACT cut-plane normal
                // instead of the Newell normal of its walked (noise-perturbed) ring.
                // Assigned at commit time - classification/bridging/triangulation above
                // must keep using the winding-derived normal.
                if (outerPolygon.IsConcave())
                {
                    if (outerPolygon.GetTriangulated(out var triangles))
                    {
                        LogConcavePartSplitByPlane(LogLevel.INFO, $"Triangulated cap polygon into {triangles.Count} piece(s)");
                        foreach (var triangle in triangles)
                        {
                            // Should be automatic in GetTriangulated
                            triangle.Normal = capReferenceNormal;
                            splitPartBrush.AddPolygon(triangle);
                        }
                    }
                    else
                        LogConcavePartSplitByPlane(LogLevel.ERROR, $"Failed to triangulate cap polygon - cap will be left out of split brush");
                }
                else
                {
                    outerPolygon.Normal = capReferenceNormal;
                    splitPartBrush.AddPolygon(outerPolygon);
                }
            }
            #endregion

            LogConcavePartSplitByPlane(LogLevel.DEBUG, $"Original Polygon Count {Polygons.Count}, Split Brush Polygon Count {splitPartPolygons.Count}");
            LogConcavePartSplitByPlane(LogLevel.DEBUG, $"Split {polygonType} Brush Type {splitPartBrush.Type}. Unique Verts {splitPartBrush.GetAllUniqueVertices().Count}");


            for (int i = 0; splitPartPolygons.Count > i; i++)
            {
                var polygon = splitPartPolygons[i];

                LogConcavePartSplitByPlane(LogLevel.DEBUG, $"Polygon {i} (Normal {polygon.Normal}, Split {polygonType}):");
                foreach (var vertex in polygon.Vertices)
                    LogConcavePartSplitByPlane(LogLevel.DEBUG, $"\t\t{vertex}");

                Logger.Write(LogLevel.DEBUG, "");
            }

            if (ReadFromFile || true)
            {
                OBJWriter.WriteOBJFromUnrealBrush(this, Name);
                OBJWriter.WriteOBJFromUnrealBrush(splitPartBrush, splitPartBrush.Name);
            }

            return true;
        }

        // TODO: Combines coplanar adjacent compatible polygons together
        // TODO: Returns the amount of polygons combined (2 for each join)
        // This can produce concave polygons, so should only be used on convex geometry to clean up faces
        //public int JoinPolygons()
        //{
        //    return 0;
        //}

        public bool ApplyCustomScale(double scale)
        {
            // Non-positive would collapse or mirror the geometry (and break the 1/scale restore).
            if (scale <= 0)
            {
                Logger.Write(LogLevel.WARNING, $"BrushActor.ApplyCustomScale({Name}): rejected non-positive scale {scale}");
                return false;
            }

            double factor = scale / _appliedScale;
            _appliedScale = scale;

            if (factor == 1.0) return true;

            foreach (var polygon in Polygons)
                polygon.ScaleVertices(factor);

            CenterPoint = Vector.Zero;

            return true;
        }

        private const double MinNativeDecompExtent = 20.0;
        private const double CanonicalDecompExtent = 500.0;

        // This exists to support small brush scaling.. a crutch that should be replaced with a better solution
        // Uses power of 2 scaling to not mess up mantissa
        private bool TryScaleUpForDecomposition()
        {
            var verts = GetAllUniqueVertices();
            if (verts.Count == 0) return false;

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            foreach (var v in verts)
            {
                if (v.X < minX) minX = v.X;
                if (v.Y < minY) minY = v.Y;
                if (v.Z < minZ) minZ = v.Z;
                if (v.X > maxX) maxX = v.X;
                if (v.Y > maxY) maxY = v.Y;
                if (v.Z > maxZ) maxZ = v.Z;
            }

            double longest = Math.Max(maxX - minX, Math.Max(maxY - minY, maxZ - minZ));
            if (longest < Vector.ShortEdgeEpsilon) return false; // Tiny degenerate
            if (longest >= MinNativeDecompExtent) return false; // Big enough

            int exponent = (int)Math.Floor(Math.Log2(CanonicalDecompExtent / longest));
            double factor = Math.Pow(2, exponent);

            ApplyCustomScale(_appliedScale * factor);
            LogGetDecomposedConcave(LogLevel.INFO, $"Small brush (extent {longest:F4}) scaled x{factor} for decomposition");
            return true;
        }

        private void LogGetDecomposedConcave(LogLevel level, string message) =>
            Logger.Write(level, $"BrushActor.ComputeConvexPieces({Name}): {message}");

        // I dont like it being in a huge try
        public bool GetDecomposedConcave(out List<BrushActor> brushSplits)
        {
            brushSplits = new();

            if (Type != UTBrushType.Concave && Type != UTBrushType.NonManifold)
            {
                LogGetDecomposedConcave(LogLevel.WARNING, $"Called with type {Type} instead of Concave");
                return false;
            }

            double s0 = _appliedScale;
            bool didScale = TryScaleUpForDecomposition();
            try
            {
                // First we have to find the plane that splits our shape
                // The one with least imbalance and least polygon splits is the best splitting plane
                var planes = GetPlanes();
                if (planes is null)
                {
                    LogGetDecomposedConcave(LogLevel.ERROR, $"GetPlanes failed - some polygon has no calculable normal. Will create thin brush per polygon in VMF as fallback behavior");
                    return false;
                }

                int bestSplittingScore = int.MaxValue;

                Dictionary<Vector, VertexPlanePosition> assortedVertices = new(new VectorComparer());
                Plane? clippingPlane = null;

                foreach (var plane in planes)
                {
                    Dictionary<Vector, VertexPlanePosition> currentAssortedVertices = new(new VectorComparer());

                    int polygonsBehind = 0, polygonsInFront = 0, polygonSplits = 0;
                    foreach (var polygon in Polygons)
                    {
                        bool isBehind = false, isInFront = false;
                        foreach (var vertex in polygon.Vertices)
                        {
                            var vertexPlanePosition = new VertexPlanePosition(plane.SignedDistance(vertex));
                            currentAssortedVertices.TryAdd(vertex, vertexPlanePosition);

                            var relativePosition = vertexPlanePosition.RelativePosition;

                            if (relativePosition == RelativeVertexPosition.BehindPlane)
                                isBehind = true;

                            if (relativePosition == RelativeVertexPosition.InFrontOfPlane)
                                isInFront = true;
                        }

                        if (isBehind && isInFront) polygonSplits++;
                        else if (isBehind) polygonsBehind++;
                        else if (isInFront) polygonsInFront++;
                    }

                    bool skipPlane = (polygonsBehind == 0 || polygonsInFront == 0) && polygonSplits == 0;
                    if (skipPlane) continue;

                    int planeScore = (polygonSplits * 20) + Math.Abs(polygonsBehind - polygonsInFront);
                    if (planeScore < bestSplittingScore)
                    {
                        bestSplittingScore = planeScore;

                        assortedVertices = currentAssortedVertices;
                        clippingPlane = plane;
                    }
                }

                if (clippingPlane is null)
                {
                    LogGetDecomposedConcave(LogLevel.WARNING, $"Couldn't find clipping plane. Will create thin brush per polygon in VMF as fallback behavior");
                    return false;
                }

                if (!GetConcavePartSplitByPlane(clippingPlane.Value, assortedVertices, false, out var backBrush))
                {
                    LogGetDecomposedConcave(LogLevel.ERROR, $"Couldn't split back brush. Back Brush type {backBrush.Type}. Will create thin brush per polygon in VMF as fallback behavior");
                    return false;
                }

                if (!GetConcavePartSplitByPlane(clippingPlane.Value, assortedVertices, true, out var frontBrush))
                {
                    LogGetDecomposedConcave(LogLevel.ERROR, $"Couldn't split front brush. Front Brush type {frontBrush.Type}. Will create thin brush per polygon in VMF as fallback behavior");
                    return false;
                }

                List<BrushActor> brushSubSplits = new();
                void SplitToConvexes(BrushActor brushSplit)
                {
                    if (brushSplit.Type == UTBrushType.Invalid)
                    {
                        LogGetDecomposedConcave(LogLevel.WARNING, $"Ignoring brush {brushSplit.Name}, type of {brushSplit.Type} with {brushSplit.GetAllUniqueVertices().Count} vertices");
                        return;
                    }

                    if (brushSplit.Type == UTBrushType.Convex)
                    {
                        brushSubSplits.Add(brushSplit);
                        LogGetDecomposedConcave(LogLevel.INFO, $"Formed a convex piece: {brushSplit.Name}");

                        return;
                    }

                    if (brushSplit.GetDecomposedConcave(out var subSplits))
                        foreach (var subSplit in subSplits)
                            SplitToConvexes(subSplit);

                    LogGetDecomposedConcave(LogLevel.WARNING, $"Brush {brushSplit.Name} is {brushSplit.Type}, not a convex");
                    return;
                }

                SplitToConvexes(backBrush);
                SplitToConvexes(frontBrush);

                if (ReadFromFile)
                    foreach (var brushSplit in brushSubSplits)
                    {
                        //brushSplit.JoinPolygons();
                        OBJWriter.WriteOBJFromUnrealBrush(brushSplit, brushSplit.Name);
                    }

                brushSplits = brushSubSplits;

                LogGetDecomposedConcave(LogLevel.INFO, $"Decomposed {Operation} {Type} to {brushSubSplits.Count} convexes");

                return true;
            }
            finally
            {
                // Restore size
                if (didScale)
                {
                    ApplyCustomScale(s0);
                    foreach (var piece in brushSplits)
                        piece.ApplyCustomScale(s0);
                }
            }
        }
        #endregion

        // Transforms UT brush vertices to Source world coordinates
        // https://github.com/FaultyRAM/Ut99PubSrc/blob/master/Engine/Inc/ABrush.h#L13
        // UTPolygon.Transform resembles "ToLocal" of ABrush from UT99 Public Source Code, due to order of the formula
        // Although, our transformation provides results same as how UT transforms brushes from .t3d
        // Maybe I do not understand something? Well, it works...
        // TODO: Sheering (Not encountered yet so didn't implement)

        // This is very likely to break topology (inverted vertex winding for example)
        // Need to fix the invertions later!
        public void TransformToSource()
        {
            if (Transformed) return;

            if (_appliedScale != 1.0)
                ApplyCustomScale(1.0);

            Position = Vector.GetSourcePositionFromUnrealPosition(Position);
            foreach (var polygon in Polygons)
                polygon.Transform();

            // We check here if it does NOT look inside the brush volume and then flip
            // Because Source brush planes winding is CW, not CCW
            if (Type == UTBrushType.Convex)
                foreach (var polygon in Polygons)
                    if (!polygon.LooksInsideBrushVolume())
                        polygon.ReverseVertexOrder();

            Transformed = true;
        }

        // Should all be convex, but heard rumors that UT accepts concave polygons
        // Could check for concavity and then triangulate if it's like that, then
        // Update: Yes it does accept almost ANY degeneracy!
        // TODO: Join coplanar polygons first to reduce brush count
        public bool GetPolygonsAsBrushes(out List<BrushActor> polygonBrushes)
        {
            polygonBrushes = new();
            if (Type == UTBrushType.Invalid) return false;

            // Should turn into a launch param later
            int thickness = 1;

            foreach (var polygon in Polygons)
            {
                var polygonBrush = CreateEmpty();
                polygonBrush.Copy(this);

                polygonBrush.Name += $"_Polygon_{polygon.GetBrushPolygonId()}";
                    
                var frontPolygon = polygon.GetCopy();

                polygonBrush.AddPolygon(frontPolygon);

                var backPolygon = frontPolygon.GetCopy(false);
                polygonBrush.AddPolygon(backPolygon);

                var frontNormal = frontPolygon.Normal;
                var frontVertices = frontPolygon.Vertices;

                // Form the back side, but don't reverse vertex order yet
                // Because we need to form side brushes, then we can reverse
                foreach (var vertex in frontVertices)
                {
                    var backVertex = vertex - thickness * frontNormal;
                    backPolygon.PushVertex(backVertex);
                }

                // Form a side for each edge
                var backVertices = backPolygon.Vertices;
                var verticesCount = frontVertices.Count;
                for (int i = 0; frontVertices.Count > i; i++)
                {
                    var vertex1 = backVertices[i];
                    var vertex2 = backVertices[(i + 1) % verticesCount];
                    var vertex3 = frontVertices[(i + 1) % verticesCount];
                    var vertex4 = frontVertices[i];

                    var sidePolygon = frontPolygon.GetCopy(false);
                    polygonBrush.AddPolygon(sidePolygon);

                    sidePolygon.PushVertex(vertex1);
                    sidePolygon.PushVertex(vertex2);
                    sidePolygon.PushVertex(vertex3);
                    sidePolygon.PushVertex(vertex4);
                }

                // Reverse the back side so it looks outward the brush volume
                backPolygon.ReverseVertexOrder();

                polygonBrushes.Add(polygonBrush);
                OBJWriter.WriteOBJFromUnrealBrush(polygonBrush, polygonBrush.Name);
            }

            return true;
        }

        public BrushActor GetTransformed()
        {
            var copyBrush = CreateEmpty();
            copyBrush.Copy(this);
            copyBrush.CopyPolygons(this);
            copyBrush.TransformToSource();

            return copyBrush;
        }

        public void CalculateCenterPoint()
        {
            var uniqueVertices = GetAllUniqueVertices();
            var uniqueVerticesCount = uniqueVertices.Count;
            if (uniqueVerticesCount == 0) return;

            var centerPoint = Vector.Zero;
            foreach (var vertex in uniqueVertices)
                centerPoint += vertex;

            CenterPoint = centerPoint / uniqueVerticesCount;
        }
    }
}