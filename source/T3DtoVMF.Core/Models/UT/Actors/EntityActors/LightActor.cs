namespace UTModels
{
    class LightActor : EntityActor
    {
        public UnrealHSVColor HSVColor { get; set; } = new();
        public double Radius { get; set; } = 64f;
    }
}
