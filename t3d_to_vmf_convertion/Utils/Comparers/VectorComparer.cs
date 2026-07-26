public class VectorComparer : IEqualityComparer<Vector>
{
    // This is not transitive, an example with 0.01 epsilon below
    // Scenario: We are adding vertices to brush polygons and check for nearly-equals
    // We add Vertex A (0, 0, 0) to the HashSet - gets added
    // Then we add Vertex B (0, 0, 0.008) to the HashSet - within epsilon, Vertex A value used instead
    // But then we get Vertex C (0, 0, 0.016) to the HashSet - now it's (0, 0, 0.01), even though it nearly equals Vertex B which nearly equals Vertex A
    // Then we get our brush or brush split non-manifold.
    // Brush initially being non manifold isn't very bad since it's going to turn into bunch of func_detail
    // But if we get a non-manifold split during concave decomposition - it's bad. Because that process needs to be robust
    // This type of stuff is rare anyway (chance is a little higher on concave decomposition), but IF we stumble upon it - we NEED to fix it
    // Symptom: 2 or more vertices from .obj dump are VERY similar

    // Update: Damn this is stupid and contradicting. Causes issues obviously!
    // This will be gone in the major refactor

    public bool Equals(Vector a, Vector b) => a.NearlyEquals(b);
    public int GetHashCode(Vector vec) => vec.GetHashCode();
}
