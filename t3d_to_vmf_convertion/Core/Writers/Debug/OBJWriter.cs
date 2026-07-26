using System.Text;
using UTModels;

// Should really get a BaseWriter...

public static class OBJWriter
{
    private static readonly string _OBJ_FOLDER = "t3d_to_vmf_objs";

    private static StreamWriter? _writer = null;

    private static void WriteLine(string line = "")
    {
        if (_writer == null) throw new IOException("File Writer is null");
        _writer.WriteLine(line);
    }

    private static void WriteComment(string line = "") => WriteLine($"# {line}");

    public static bool WriteOBJFromUnrealBrush(BrushActor brush, string fileName)
    {
        // Temp hack
        if (!brush.Transformed)
        {
            WriteOBJFromUnrealBrush(brush.GetTransformed(), fileName + "_Transformed");
            return false;
        }

        Directory.CreateDirectory(_OBJ_FOLDER);

        fileName = $"{Path.GetFileName(fileName)} {brush.Type}.obj";
        string filePath = Path.Combine(_OBJ_FOLDER, fileName);

        try { 
            _writer = new StreamWriter(filePath); 
        }
        catch (Exception ex) 
        { 
            Logger.Write(LogLevel.WARNING, $"OBJ Write aborted ({Path.GetFileName(filePath)}): {ex.Message}"); _writer = null; 
            return false; 
        }

        _writer.AutoFlush = false;

        #region Brush Data Comments
        WriteComment("Brush Data included");

        WriteComment($"CSG Operation: {brush.Operation}");

        WriteLine();
        WriteComment($"Pivot Point Offset: {brush.PrePivot}");

        WriteLine();
        WriteComment($"Main Scale: {brush.MainScale}");

        WriteLine();
        WriteComment("Sheering not yet implemented nor encountered");
        WriteComment($"Main Sheer Axis: {brush.MainSheerAxis}");
        WriteComment($"Main Sheer Rate: {brush.MainSheerRate}");

        WriteLine();
        WriteComment($"Angle: {brush.Angle}");

        WriteLine();
        WriteComment($"Post Scale: {brush.PostScale}");

        WriteLine();
        WriteComment("Sheering not yet implemented nor encountered");
        WriteComment($"Post Sheer Axis: {brush.PostSheerAxis}");
        WriteComment($"Post Sheer Rate: {brush.PostSheerRate}");

        WriteLine();
        WriteComment($"World Origin: {brush.Position}");

        WriteLine();
        WriteComment($"Model Name: {brush.Model}");

        WriteLine();
        WriteComment("Splitter Debug");
        WriteComment($"(Local Space) Clip Plane: {brush.ClipperPlane}");
        WriteLine();
        #endregion

        // Write Vertex Region
        int vertexCount = 1;
        Dictionary<Vector, int> vertexIds = new(new VectorComparer());
        foreach (var vertex in brush.GetAllUniqueVertices())
        {
            vertexIds.TryAdd(vertex, vertexCount++);
            WriteLine($"v {vertex.ToOBJString()}");
        }

        WriteLine();

        // Write Normal Region
        int normalCount = 1;
        Dictionary<Vector, int> normalIds = new(new VectorComparer());
        foreach (var polygon in brush.Polygons)
        {
            var normal = polygon.Normal;
            normalIds.TryAdd(normal, normalCount++);

            WriteLine($"vn {normal.ToOBJString()}");
        }

        WriteLine();

        // Assembly faces out of vertices and normals
        foreach (var polygon in brush.Polygons)
        {
            var faceLine = new StringBuilder("f");

            var normal = polygon.Normal;
            var normalId = normalIds[normal];
            foreach (var vertex in polygon.Vertices)
                faceLine.Append(" ")
                        .Append(vertexIds[vertex])
                        .Append("//")
                        .Append(normalId);

            WriteLine($"# {polygon.Name}");
            WriteLine(faceLine.ToString());
        }

        _writer.Dispose();
        _writer = null;

        return true;
    }
}
