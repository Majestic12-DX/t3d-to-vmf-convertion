using UTModels;
using System.Diagnostics;
using SourceModels;

// Should really get a BaseWriter...

public class VMFWriter
{
    private static readonly string _VMF_FOLDER = "t3d_to_vmf_vmfs";
    private static readonly int MAX_BRUSHES = 8192;

    private HashSet<Vector> _uniqueVertices = new();
    private Dictionary<string, string> _currentChunkData = new();
    private Stack<string> _chunks = new();
    private StreamWriter? _writer = null;

    private int _uniqueVertsCount = 0;
    private int _brushesCount = 0;

    private int _badBrushSidesCount = 0;
    private int _badBrushesCount = 0;

    #region Chunk Manipulation
    private void WriteChunkLine(string line)
    {
        if (_writer == null) throw new IOException("VMF File Writer is null");

        string chunkDepthTabulations = new string('\t', _chunks.Count);
        _writer.WriteLine(chunkDepthTabulations + line);
    }
    public void PushChunk(string header)
    {
        WriteChunkLine(header);
        WriteChunkLine("{");

        _chunks.Push(header);
    }
    public void FlushChunkData()
    {
        foreach (var kvPair in _currentChunkData)
        {
            WriteChunkLine($"\"{kvPair.Key}\" \"{kvPair.Value}\"");
        }

        _currentChunkData.Clear();
    }
    public void PopChunk()
    {
        if (GetCurrentChunk().Equals("solid"))
        {
            _brushesCount++;
            if (_brushesCount == MAX_BRUSHES)
                Logger.Write(LogLevel.WARNING, $"Wrote maximum amount of brushes! ({MAX_BRUSHES})");
        }

        _chunks.Pop();
        WriteChunkLine("}");
    }
    public void FlushChunkDataAndPop()
    {
        FlushChunkData();
        PopChunk();
    }
    public void AddChunkData(string key, string value) => _currentChunkData[key] = value;
    public string GetCurrentChunk() => _chunks.Peek();
    #endregion
    #region Writing VMFs

    public bool WriteVMFFromUnrealActors(List<BaseActor> actors, string fileName)
    {
        if (actors is null) return false;

        _uniqueVertices.Clear();
        _currentChunkData.Clear();
        _chunks.Clear();

        Directory.CreateDirectory(_VMF_FOLDER);

        fileName = $"{Path.GetFileName(fileName)} {DateTime.Now:yyyy-MM-dd HH-mm-ss}.vmf";
        string filePath = Path.Combine(_VMF_FOLDER, fileName);

        Logger.Write(LogLevel.INFO, $"Writing {Path.GetFileName(filePath)} from Unreal Actors");

        var sw = Stopwatch.StartNew();
        _writer = new StreamWriter(filePath, true);
        _writer.AutoFlush = true;

        // All that decomposition shouldn't be done here, but it's temporary before input repair + csg pass

        List<VMFBase> vmfObjects = new();
        foreach (var actor in actors)
        {
            VMFBase? currentVMFObject;
            if (actor is BrushActor brush)
            {
                // Some of these are area-portals. Need to identify them using polygon bit flags
                // As it also seems is that each brush polygon can have its own flags, independently
                // It can lead to dumb scenarios where brush needs to be treated as a non-manifold, too?..
                if (brush.Type == UTBrushType.NonManifold)
                {
                    if (brush.GetPolygonsAsBrushes(out var polygonBrushes))
                    {
                        foreach (var polygonBrush in polygonBrushes)
                        {
                            currentVMFObject = UnrealActorToVMF.ConvertToVMF(polygonBrush);
                            if (currentVMFObject is null) continue;

                            vmfObjects.Add(currentVMFObject);
                        }
                    }

                    // OBJWriter.WriteOBJFromUnrealBrush(brush, brush.Name);

                    continue;
                }

                if (brush.Type == UTBrushType.Concave)
                {
                    if (brush.GetDecomposedConcave(out var brushSplits))
                    {
                        foreach (var brushSplit in brushSplits)
                        {
                            currentVMFObject = UnrealActorToVMF.ConvertToVMF(brushSplit);
                            if (currentVMFObject is null) continue;

                            vmfObjects.Add(currentVMFObject);
                        }
                    }

                    continue;
                }
                else if (brush.Type != UTBrushType.Convex) { OBJWriter.WriteOBJFromUnrealBrush(brush, brush.Name); continue; }
                else OBJWriter.WriteOBJFromUnrealBrush(brush, brush.Name);
            }

            currentVMFObject = UnrealActorToVMF.ConvertToVMF(actor);
            if (currentVMFObject is null) continue;

            vmfObjects.Add(currentVMFObject);
        }

        // TODO: This sucks ass, should make a method for this
        VMFMap vmf = new();
        foreach (var vmfObject in vmfObjects)
        {
            if (vmfObject is VMFSolid vmfSolid)
                vmf.World.Solids.Add(vmfSolid);
            else if (vmfObject is VMFBaseEntity vmfEntity)
                vmf.Entities.Add(vmfEntity);
        }

        vmf.WriteToStream(this);

        _writer.Dispose();
        _writer = null;

        sw.Stop();
        Logger.Write(LogLevel.INFO, $"Constructed .vmf in presumably {sw.ElapsedMilliseconds} ms");
        Logger.Write(LogLevel.INFO, $"Counted {vmf.GetBrushCount()} brushes, {vmf.GetBrushSidesCount()} brush faces and {_uniqueVertsCount} unique !geometric! vertices");

        if (_badBrushesCount > 0 || _badBrushSidesCount > 0)
            Logger.Write(LogLevel.WARNING, $"Counted {_badBrushesCount} bad brushes and {_badBrushSidesCount} bad brush faces");

        return true;
    }
    #endregion
}