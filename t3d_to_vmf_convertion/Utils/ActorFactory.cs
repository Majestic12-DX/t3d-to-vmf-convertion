using UTModels;

[Flags]
public enum ActorPropertyCategory
{
    None = 0,
    Brush = 1 << 0,
    Mover = 1 << 1,
    Light = 1 << 2,
}

public static class ActorFactory
{
    public static BaseActor Create(string className, ActorPropertyCategory categories, bool hasBrushGeometry)
    {
        // These should be stably classified
        switch (className)
        {
            case "PlayerStart": return new PlayerStartActor();
            case "LevelInfo": return new LevelInfoActor();
        }

        if (hasBrushGeometry || categories.HasFlag(ActorPropertyCategory.Brush) || className == "Brush")
        {
            bool isMover = categories.HasFlag(ActorPropertyCategory.Mover) || className == "Mover";
            return isMover ? new MoverActor() : new BrushActor();
        }

        if (categories.HasFlag(ActorPropertyCategory.Light) || className == "Light")
            return new LightActor();

        return new BaseActor();
    }
}
