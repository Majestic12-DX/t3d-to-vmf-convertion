using UTModels;
using System.Diagnostics;
using System.Globalization;

// Rewritten T3DReader (PendingActor is the reason why)

/* // WARNING //
    I have noticed that some maps have slight corruptions in their .t3d exports
    CTF-Niven .t3d export has different CSG result - few brushes misplaced, one missing???
    This is mirrored in both .vmf and UnrealEd .t3d importing result
    I hope the root can be found, and perhaps managed
    Otherwise we'll have to find another way of extracting map information
*/ // WARNING //

public class T3DReader
{
    private enum ReadingState
    {
        Searching,
        Actor,
        Brush,
        PolyList,
        Polygon
    }

    // This exists because UnrealScript allows derived classes from brushes and movers (GradualMover, AttachMover, etc, movers)
    // This can be problematic on custom maps where some actor has classname set to something stupid instead of "Something something mover/brush"
    // So instead of identifying class by classname we identify by properties
    // This fixed CTF-Niven and CityIntro
    private sealed class PendingActor
    {
        public required string Class;
        public required string Name;

        public bool HasBrushBlock;

        public ActorPropertyCategory Categories;
        
        public List<(int LineNumber, string Key, string Line)> Properties = new();
        public List<UTPolygon> Polygons = new();
    }

    private static readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    private static readonly char[] _separationChars = { ' ', ')', ',' };
    private static readonly char[] _propertySeparationChars = { ' ', '=' };

    private int _lineCount = 0;

    private PendingActor? _pendingActor;
    private UTPolygon? _currentPolygon;
    private Stack<ReadingState> _stack = new();
    private List<BaseActor> _actors = new();

    // Classification signals
    private static readonly Dictionary<string, ActorPropertyCategory> _propertyCategories = new()
    {
        // Brush
        { "CsgOper", ActorPropertyCategory.Brush },
        { "MainScale", ActorPropertyCategory.Brush },
        { "PostScale", ActorPropertyCategory.Brush },
        { "PrePivot", ActorPropertyCategory.Brush },
        { "PolyFlags", ActorPropertyCategory.Brush },

        // Mover
        { "BasePos", ActorPropertyCategory.Mover },
        { "BaseRot", ActorPropertyCategory.Mover },
        { "KeyPos", ActorPropertyCategory.Mover },
        { "KeyRot", ActorPropertyCategory.Mover },
        { "KeyNum", ActorPropertyCategory.Mover },
        { "NumKeys", ActorPropertyCategory.Mover },
        { "MoverTime", ActorPropertyCategory.Mover },
        { "MoverEncroachType", ActorPropertyCategory.Mover },
        { "MoverGlideType", ActorPropertyCategory.Mover },
        { "BumpType", ActorPropertyCategory.Mover },
        { "StayOpenTime", ActorPropertyCategory.Mover },
        { "WorldRaytraceKey", ActorPropertyCategory.Mover },
        { "BrushRaytraceKey", ActorPropertyCategory.Mover },

        // Light
        { "LightBrightness", ActorPropertyCategory.Light },
        { "LightHue", ActorPropertyCategory.Light },
        { "LightSaturation", ActorPropertyCategory.Light },
        { "LightRadius", ActorPropertyCategory.Light },
        { "LightType", ActorPropertyCategory.Light },
        { "LightEffect", ActorPropertyCategory.Light },
        { "LightPeriod", ActorPropertyCategory.Light },
        { "LightPhase", ActorPropertyCategory.Light },
        { "LightCone", ActorPropertyCategory.Light },
    };

    private void ThrowFormatException(string message) => throw new FormatException($"Error at line {_lineCount}: {message}");

    // .t3d String Value example: Class=Mover
    // .t3d Quoted String Value example: Tag="Reddoor"
    private string ExtractStringValueFromLine(string line, string key)
    {
        int keyIndex = line.LastIndexOf(key + "=", StringComparison.OrdinalIgnoreCase);
        if (keyIndex == -1) return "";

        string valuePart = line.Substring(keyIndex + key.Length + 1);

        // Sometimes values are stored inside quotes. They are not supposed to be a part of the value

        // Value is in quotes
        if (valuePart[0] == '"')
        {
            int secondQuote = valuePart.IndexOf('"', 1);
            if (secondQuote == -1) ThrowFormatException($"Failed reading value {key}. Seemed to be in quotes but no second quote found.");

            return valuePart.Substring(1, secondQuote - 1);
        }
        else
        {
            int endChar = valuePart.IndexOfAny(_separationChars);
            if (endChar == -1) return valuePart;

            return valuePart.Substring(0, endChar);
        }
    }

    // .t3d Index Example: KeyPos(1)=(X=120.000000)
    private int ExtractIndexFromLine(string line, string key)
    {
        int keyStartIndex = line.LastIndexOf(key + '(', StringComparison.OrdinalIgnoreCase);
        if (keyStartIndex == -1) return -1;

        int indexStart = keyStartIndex + key.Length + 1;

        int indexEnd = line.IndexOf(')', indexStart);
        if (indexEnd == -1) return -1;

        var index = line.Substring(indexStart, indexEnd - indexStart);
        if (int.TryParse(index, out int finalIndex))
            return finalIndex;

        return -1;
    }

    // .t3d Vector Example: Location=(X=416.000000,Y=-432.000000,Z=-144.000000)
    private Vector ExtractVectorFromLine(string line, double defaultCoordinate = 0f)
    {
        string xValue = ExtractStringValueFromLine(line, "X");
        string yValue = ExtractStringValueFromLine(line, "Y");
        string zValue = ExtractStringValueFromLine(line, "Z");

        double x;
        if (!double.TryParse(xValue, _culture, out x))
            x = defaultCoordinate;

        double y;
        if (!double.TryParse(yValue, _culture, out y))
            y = defaultCoordinate;

        double z;
        if (!double.TryParse(zValue, _culture, out z))
            z = defaultCoordinate;

        return new Vector(x, y, z);
    }

    // .t3d Angle Example: Rotation=(Pitch=-3600,Yaw=12496,Roll=16384)
    private Angle ExtractAngleFromLine(string line)
    {
        string pitchValue = ExtractStringValueFromLine(line, "Pitch");
        string yawValue = ExtractStringValueFromLine(line, "Yaw");
        string rollValue = ExtractStringValueFromLine(line, "Roll");

        double.TryParse(pitchValue, _culture, out double pitch);
        double.TryParse(yawValue, _culture, out double yaw);
        double.TryParse(rollValue, _culture, out double roll);

        return new Angle(pitch, yaw, roll);
    }

    // .t3d Polygon Vector Example: Vertex   -00026.000000,+00036.000000,+00008.000000
    private Vector ExtractVectorFromPolygonLine(string line)
    {
        line = line.Trim();

        // Separate from the name of vector
        int firstSpace = line.IndexOf(' ');
        if (firstSpace == -1) ThrowFormatException($"Failed reading polygon vector. Couldn't separate from the name, no space found");

        string valuesPart = line.Substring(firstSpace).Trim();

        string[] valueComponents = valuesPart.Split(',');
        double x = 0, y = 0, z = 0;
        if (valueComponents.Length >= 1) double.TryParse(valueComponents[0].Trim(), _culture, out x);
        if (valueComponents.Length >= 2) double.TryParse(valueComponents[1].Trim(), _culture, out y);
        if (valueComponents.Length >= 3) double.TryParse(valueComponents[2].Trim(), _culture, out z);

        return new Vector(x, y, z);
    }

    public List<BaseActor> ReadT3DActors(string filePath)
    {
        Logger.Write(LogLevel.INFO, $"Reading {Path.GetFileName(filePath)}");

        var sw = Stopwatch.StartNew();
        using var reader = new StreamReader(filePath);

        _actors.Clear();

        _stack.Clear();
        _stack.Push(ReadingState.Searching);
        _lineCount = 0;
        _pendingActor = null;
        _currentPolygon = null;

        string? currentLine;
        while ((currentLine = reader.ReadLine()) != null)
        {
            _lineCount++;
            currentLine = currentLine.Trim();

            if (string.IsNullOrWhiteSpace(currentLine)) continue;

            string[] lineParts = currentLine.Split(' ', 3);
            if (lineParts.Length >= 2)
            {
                string actionType = lineParts[0];
                string actionArgument = lineParts[1];

                if (actionType.Equals("Begin") && TryHandleBegin(actionArgument, currentLine)) continue;
                if (actionType.Equals("End") && TryHandleEnd(actionArgument)) continue;
            }

            if (_stack.Peek() == ReadingState.Searching || _pendingActor is null) continue;

            int separatorIndex = currentLine.IndexOfAny(_propertySeparationChars);
            if (separatorIndex == -1)
            {
                Logger.Write(LogLevel.DEBUG, $"Skipping line {_lineCount}: {currentLine}");
                continue;
            }

            // Indexed properties come as "Key(index)" and we don't need the brackets part
            string key = currentLine.Substring(0, separatorIndex);
            int parenIndex = key.IndexOf('(');
            if (parenIndex >= 0) key = key.Substring(0, parenIndex);

            if (_stack.Peek() == ReadingState.Polygon)
            {
                ApplyPolygonProperty(key, currentLine);
                continue;
            }

            // Updating bit flags for actor categories
            if (_propertyCategories.TryGetValue(key, out var category))
                _pendingActor.Categories |= category;

            _pendingActor.Properties.Add((_lineCount, key, currentLine));
        }

        sw.Stop();
        Logger.Write(LogLevel.INFO, $"Read .t3d in presumably {sw.ElapsedMilliseconds} ms");

        return _actors;
    }

    private bool TryHandleBegin(string argument, string line)
    {
        if (argument.Equals("Actor"))
        {
            if (_stack.Peek() != ReadingState.Searching) ThrowFormatException("\"Begin Actor\" found, but we weren't searching for actor");

            string actorClass = ExtractStringValueFromLine(line, "Class");
            if (actorClass == "") ThrowFormatException("Actor Class not found");

            string actorName = ExtractStringValueFromLine(line, "Name");
            Logger.Write(LogLevel.DEBUG, $"Started reading {actorClass} Actor {actorName}");

            _pendingActor = new PendingActor { Class = actorClass, Name = actorName };

            _stack.Push(ReadingState.Actor);
            return true;
        }

        if (argument.Equals("Brush"))
        {
            if (_stack.Peek() != ReadingState.Actor) ThrowFormatException("\"Begin Brush\" found, but we weren't reading actor");
            Logger.Write(LogLevel.DEBUG, "Started reading brush");

            _pendingActor!.HasBrushBlock = true;
            _stack.Push(ReadingState.Brush);
            return true;
        }

        if (argument.Equals("PolyList"))
        {
            if (_stack.Peek() != ReadingState.Brush) ThrowFormatException("\"Begin PolyList\" found, but we weren't reading brush");
            Logger.Write(LogLevel.DEBUG, "Started reading polygon list");

            _stack.Push(ReadingState.PolyList);
            return true;
        }

        if (argument.Equals("Polygon"))
        {
            if (_stack.Peek() != ReadingState.PolyList) ThrowFormatException("\"Begin Polygon\" found, but we weren't reading polygon list");
            Logger.Write(LogLevel.DEBUG, "Started reading polygon");

            var pending = _pendingActor!;

            // Stable debug identity for better tracing
            UTPolygon newPolygon = new($"{pending.Name}_P{pending.Polygons.Count}");
            newPolygon.Texture = ExtractStringValueFromLine(line, "Texture");

            SetValue<int>(line, "Flags", value => newPolygon.Flags = value);

            pending.Polygons.Add(newPolygon);
            _currentPolygon = newPolygon;

            _stack.Push(ReadingState.Polygon);
            return true;
        }

        return false;
    }

    private bool TryHandleEnd(string argument)
    {
        if (argument.Equals("Actor"))
        {
            if (_stack.Peek() != ReadingState.Actor) ThrowFormatException("\"End Actor\" found, but we weren't reading actor");

            FinalizePendingActor();

            _pendingActor = null;
            _stack.Pop();
            return true;
        }

        if (argument.Equals("Brush"))
        {
            if (_stack.Peek() != ReadingState.Brush) ThrowFormatException("\"End Brush\" found, but we weren't reading brush");
            Logger.Write(LogLevel.DEBUG, "Finished reading brush");

            _stack.Pop();
            return true;
        }

        if (argument.Equals("PolyList"))
        {
            if (_stack.Peek() != ReadingState.PolyList) ThrowFormatException("\"End PolyList\" found, but we weren't reading polygon list");
            Logger.Write(LogLevel.DEBUG, "Finished reading polygon list");

            _stack.Pop();
            return true;
        }

        if (argument.Equals("Polygon"))
        {
            if (_stack.Peek() != ReadingState.Polygon) ThrowFormatException("\"End Polygon\" found, but we weren't reading polygon");
            Logger.Write(LogLevel.DEBUG, "Finished reading polygon");

            _currentPolygon = null;
            _stack.Pop();
            return true;
        }

        return false;
    }

    private void FinalizePendingActor()
    {
        var pending = _pendingActor!;

        BaseActor newActor = ActorFactory.Create(pending.Class, pending.Categories, pending.HasBrushBlock);
        newActor.Class = pending.Class;
        newActor.Name = pending.Name;
        newActor.ReadFromFile = true;

        if (newActor is BrushActor brushActor)
            foreach (var polygon in pending.Polygons)
                brushActor.AddPolygon(polygon);

        int savedLineCount = _lineCount;
        foreach (var (lineNumber, key, propertyLine) in pending.Properties)
        {
            _lineCount = lineNumber;
            ApplyActorProperty(newActor, key, propertyLine);
        }
        _lineCount = savedLineCount;

        _actors.Add(newActor);

        Logger.Write(LogLevel.DEBUG, $"Finished reading {pending.Class} Actor {pending.Name} as {newActor.GetType().Name}");
    }

    private void ApplyPolygonProperty(string key, string line)
    {
        if (_currentPolygon is null) return;

        switch (key.ToLowerInvariant())
        {
            case "origin":
                _currentPolygon.Origin = ExtractVectorFromPolygonLine(line);
                break;

            // "Normal" intentionally not read — recomputed from vertices for more accuracy and stability. Might regret later
            // Update: No, I did not
            // Update 2: I'm not sure

            case "textureu":
                _currentPolygon.TextureU = ExtractVectorFromPolygonLine(line);
                break;

            case "texturev":
                _currentPolygon.TextureV = ExtractVectorFromPolygonLine(line);
                break;

            case "vertex":
                _currentPolygon.PushVertex(ExtractVectorFromPolygonLine(line));
                break;

            case "pan":
                SetValue<int>(line, "U", value => _currentPolygon.PanU = value);
                SetValue<int>(line, "V", value => _currentPolygon.PanV = value);
                break;

            default:
                Logger.Write(LogLevel.DEBUG, $"Unknown polygon property at line {_lineCount}: {line}");
                break;
        }
    }

    private void ApplyActorProperty(BaseActor actor, string key, string line)
    {
        switch (key.ToLowerInvariant())
        {
            // General Actor
            case "tag":
                actor.Tag = ExtractStringValueFromLine(line, "Tag");
                break;

            case "event":
                actor.Event = ExtractStringValueFromLine(line, "Event");
                break;

            case "location":
                if (actor is MoverActor) break; // Movers position is BasePos

                actor.Position = ExtractVectorFromLine(line);
                break;

            case "rotation":
                if (actor is MoverActor) break; // Movers rotation is BaseRot

                actor.Angle = ExtractAngleFromLine(line);
                break;

            // Brush Actor
            case "mainscale":
                if (actor is BrushActor mainScaleBrush && line.Contains("Scale="))
                    mainScaleBrush.MainScale = ExtractVectorFromLine(line, 1);
                break;

            case "postscale":
                if (actor is BrushActor postScaleBrush && line.Contains("Scale="))
                    postScaleBrush.PostScale = ExtractVectorFromLine(line, 1);
                break;

            case "csgoper":
                if (actor is BrushActor csgBrush)
                    csgBrush.Operation = line.Contains("CSG_Subtract") ? CSGOperation.Subtract : CSGOperation.Add;
                break;

            case "prepivot":
                if (actor is BrushActor prePivotBrush)
                    prePivotBrush.PrePivot = ExtractVectorFromLine(line);
                break;

            // Mover Actor
            case "basepos":
                if (actor is MoverActor basePosMover)
                {
                    basePosMover.Position = ExtractVectorFromLine(line);
                    basePosMover.KeyPos[0] = basePosMover.Position;
                }

                break;

            case "baserot":
                if (actor is MoverActor baseRotMover)
                {
                    baseRotMover.Angle = ExtractAngleFromLine(line);
                    baseRotMover.KeyRot[0] = baseRotMover.Angle;
                }

                break;

            case "keypos":
                if (actor is MoverActor keyPosMover)
                {
                    int keyPosIndex = ExtractIndexFromLine(line, "KeyPos");
                    if (keyPosIndex < 0 || keyPosIndex >= 8)
                    {
                        Logger.Write(LogLevel.ERROR, $"Read invalid mover KeyPos {keyPosIndex} at ${_lineCount}. Skipping");
                        break;
                    }

                    keyPosMover.KeyPos[keyPosIndex] = ExtractVectorFromLine(line);
                }
                break;

            // Light Data
            case "lighthue":
                if (actor is LightActor hueLight) SetValue<byte>(line, "LightHue", value => hueLight.HSVColor = hueLight.HSVColor.WithHue(value));
                break;

            case "lightsaturation":
                if (actor is LightActor saturationLight) SetValue<byte>(line, "LightSaturation", value => saturationLight.HSVColor = saturationLight.HSVColor.WithSaturation(value));
                break;

            case "lightbrightness":
                if (actor is LightActor brightnessLight) SetValue<byte>(line, "LightBrightness", value => brightnessLight.HSVColor = brightnessLight.HSVColor.WithBrightness(value));
                break;

            case "lightradius":
                if (actor is LightActor radiusLight) SetValue<double>(line, "LightRadius", value => radiusLight.Radius = value);
                break;

            // Level Info
            case "title":
                if (actor is LevelInfoActor titleInfo) titleInfo.Title = ExtractStringValueFromLine(line, "Title");
                break;

            case "levelentertext":
                if (actor is LevelInfoActor enterTextInfo) enterTextInfo.LevelEnterText = ExtractStringValueFromLine(line, "LevelEnterText");
                break;

            default:
                Logger.Write(LogLevel.DEBUG, $"Unknown property at line {_lineCount}: {line}");
                break;
        }
    }

    private void SetValue<T>(string line, string key, Action<T> setter) where T : struct, IParsable<T>
    {
        if (T.TryParse(ExtractStringValueFromLine(line, key), null, out T value))
            setter(value);
    }
}
