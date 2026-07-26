using SourceModels;
using UTModels;

public static class UnrealActorToVMF
{
    private static VMFBase? LogConversionFail(BaseActor actor)
    {
        Logger.Write(LogLevel.WARNING, $"UnrealActorToVMF.ConvertToVMF: No VMF counterpart for {actor.Name} unreal actor");
        return null;
    }

    #region Converting Various Unreal Actors to VMF
    #region Header Related
    private static VMFWorld UnrealLevelInfoToVMFWorld(LevelInfoActor levelInfo)
    {
        VMFWorld vmfWorld = new VMFWorld();
        vmfWorld.ChapterTitle = levelInfo.Title;
        vmfWorld.Message = levelInfo.LevelEnterText;

        return vmfWorld;
    }
    #endregion
    #region Brush Related
    private static VMFSolid UnrealBrushToVMFSolid(BrushActor brush)
    {
        Logger.Write(LogLevel.DEBUG, $"{brush.Name} type is {brush.Operation} {brush.Type}. Unique vertices {brush.GetAllUniqueVertices().Count}. If this seems wrong - go check .T3D and report");

        if (!brush.Transformed)
            brush.TransformToSource();

        VMFSolid vmfSolid = new VMFSolid();
        vmfSolid.UnrealBrushName = brush.Name;
        vmfSolid.UnrealBrushOperation = brush.Operation;

        HashSet<Vector> usedNormals = new(new VectorComparer());
        foreach (UTPolygon polygon in brush.Polygons)
        {
            // This is not ideal, nor even acceptable long run

            if (polygon.Vertices.Count < 3)
            {
                Logger.Write(LogLevel.ERROR, $"Bad polygon in {brush.Name}. Less than 3 vertices. Skipping");
                continue;
            }

            Vector vertex1 = polygon.Vertices[0];
            Vector vertex2 = polygon.Vertices[1];

            bool planeFound = false;
            for (int i = 2; polygon.Vertices.Count > i; i++)
            {
                Vector vertex3 = polygon.Vertices[i];
                if (!Geometry.IsNonCollinear(vertex1, vertex2, vertex3)) continue;

                var normal = Vector.GetNormal(vertex1, vertex2, vertex3);
                if (usedNormals.Contains(normal)) continue;
                usedNormals.Add(normal);

                VMFSide vmfSide = new VMFSide();
                vmfSide.Plane = (vertex1, vertex2, vertex3);
                vmfSide.Material = brush.Operation == CSGOperation.Add ? "dev/dev_measuregeneric01" : "dev/dev_measuregeneric01b"; // polygon.Texture

                vmfSolid.Sides.Add(vmfSide);

                planeFound = true;
                break;
            }

            if (!planeFound)
                Logger.Write(LogLevel.ERROR, $"Could not find 3 non-collinear vertices for polygon in {brush.Name}");
        }

        return vmfSolid;
    }
    #endregion
    #region Entity Related
    private static VMFPlayerStart UnrealPlayerStartToVMFPlayerStart(PlayerStartActor playerStart) => new VMFPlayerStart();
    private static VMFLight UnrealLightToVMFLight(LightActor light)
    {
        VMFLight vmfLight = new VMFLight();
        vmfLight.Color = light.HSVColor.ToRGB();
        vmfLight.Distance = light.Radius;

        return vmfLight;
    }
    private static void SetBasicEntityData(VMFBasePlaceableEntity entity, BaseActor actor)
    {
        // Sometimes this shouldn't be set. And it can wait
        // entity.TargetName = actor.Name;

        entity.Origin = Vector.GetSourcePositionFromUnrealPosition(actor.Position);
        
        if (actor.HasRotation() && entity.Solids.Count == 0)
            entity.Angles = Angle.GetEulerAngleFromUnrealAngle(actor.Angle);
    }
    #endregion
#endregion
    public static VMFBase? ConvertToVMF(BaseActor actor)
    {
        VMFBase? vmfObject = actor switch
        {
            // Header type of stuff
            LevelInfoActor levelInfo => UnrealLevelInfoToVMFWorld(levelInfo),

            // Brush objects
            BrushActor brush => UnrealBrushToVMFSolid(brush),
            // MoverActor mover => UnrealMoverToVMFTrain(mover),

            // Entity objects
            LightActor light => UnrealLightToVMFLight(light),
            PlayerStartActor playerStart => UnrealPlayerStartToVMFPlayerStart(playerStart),
            _ => LogConversionFail(actor)
        };

        if (vmfObject is VMFBasePlaceableEntity entity)
            SetBasicEntityData(entity, actor);

        return vmfObject;
    }
}
