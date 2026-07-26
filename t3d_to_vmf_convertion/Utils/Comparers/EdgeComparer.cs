internal class EdgeComparer : IEqualityComparer<Edge>
{
    // This may have problems from VectorComparer, since this is basically 2 vertices comparison
    // This treats edges as unordered

    public bool Equals(Edge a, Edge b) => a.NearlyEquals(b);
    public int GetHashCode(Edge edge) => edge.GetHashCode();
}
