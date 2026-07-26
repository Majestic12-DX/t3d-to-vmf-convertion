using UTModels;

// This is directed edge that is treated as undirected in comparisons (maybe should be changed?) looking at this now and it could have been better
public readonly struct Edge : IEquatable<Edge>
{
    public Vector StartPoint { get; init; }
    public Vector EndPoint { get; init; }

    // Debug Info
    public (UTPolygon Polygon, int PolygonId) CapEdgeOrigin { get; init; }

    public Edge(Vector edgeStart, Vector edgeEnd)
    {
        StartPoint = edgeStart;
        EndPoint = edgeEnd;
    }

    public bool Equals(Edge otherEdge) =>
        StartPoint.Equals(otherEdge.StartPoint) && EndPoint.Equals(otherEdge.EndPoint);
    public bool NearlyEquals(Edge otherEdge) =>
        (StartPoint.NearlyEquals(otherEdge.StartPoint) && EndPoint.NearlyEquals(otherEdge.EndPoint))
        || (StartPoint.NearlyEquals(otherEdge.EndPoint) && EndPoint.NearlyEquals(otherEdge.StartPoint));

    public bool IsAdjacent(Edge otherEdge) =>
        StartPoint.NearlyEquals(otherEdge.StartPoint) || StartPoint.NearlyEquals(otherEdge.EndPoint) 
        || EndPoint.NearlyEquals(otherEdge.StartPoint) || EndPoint.NearlyEquals(otherEdge.EndPoint);

    public bool IsValid() => !StartPoint.NearlyEquals(EndPoint);

    public override int GetHashCode()
    {
        int edgeStartHash = StartPoint.GetHashCode();
        int edgeEndHash = EndPoint.GetHashCode();
        return HashCode.Combine(Math.Min(edgeStartHash, edgeEndHash), Math.Max(edgeStartHash, edgeEndHash));
    }

    public override string ToString() => $"{StartPoint} - {EndPoint}";
}