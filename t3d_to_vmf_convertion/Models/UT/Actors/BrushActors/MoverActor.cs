public enum MoverEncroachType
{
    ME_StopWhenEncroach,
    ME_ReturnWhenEncroach,
    ME_CrushWhenEncroach,
    ME_IgnoreWhenEncroach
}

namespace UTModels
{
    public class MoverActor : BrushActor
    {
        public MoverEncroachType EncroachType { get; set; } = MoverEncroachType.ME_StopWhenEncroach;
        public int MoveTime { get; set; } = 0;
        public bool DamageTriggered { get; set; } = false;
        public Dictionary<int, Vector> KeyPos { get; set; } = new();
        public Dictionary<int, Angle> KeyRot { get; set; } = new();

        protected override bool CopyDetails(BaseActor actorToCopy)
        {
            if (actorToCopy is not MoverActor moverToCopy || !base.CopyDetails(actorToCopy)) return false;

            EncroachType = moverToCopy.EncroachType;

            MoveTime = moverToCopy.MoveTime;

            DamageTriggered = moverToCopy.DamageTriggered;

            KeyPos = moverToCopy.KeyPos.ToDictionary();
            KeyRot = moverToCopy.KeyRot.ToDictionary();

            return true;
        }

        protected override MoverActor CreateEmpty() => new MoverActor();
    }
}
