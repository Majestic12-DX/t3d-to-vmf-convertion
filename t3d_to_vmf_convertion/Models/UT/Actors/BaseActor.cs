using System.Runtime.CompilerServices;

namespace UTModels
{
    public class BaseActor
    {
        public string Class { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Event { get; set; } = string.Empty;
        public Vector Position { get; set; } = new Vector();
        public Angle Angle { get; set; } = new Angle();
        public Dictionary<string, string> Properties { get; set; } = new();

        // Debug Info
        public bool ReadFromFile { get; set; } = false;

        protected virtual bool CopyDetails(BaseActor actorToCopy) => true;

        public bool Copy(BaseActor actorToCopy)
        {
            if (actorToCopy is null) return false;

            Class = actorToCopy.Class;
            Name = actorToCopy.Name;
            Tag = actorToCopy.Tag;
            Event = actorToCopy.Event;
            Position = actorToCopy.Position;
            Angle = actorToCopy.Angle;
            Properties = actorToCopy.Properties.ToDictionary();

            CopyDetails(actorToCopy);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasRotation() => !Angle.IsZero();
    }
}
