using Godot;

namespace ChallengePoints;

internal static class ChallengeArt
{
    private static Texture2D? _malleableIcon;
    private static Texture2D? _ambergrisIcon;
    private static Texture2D? _contractFrame;

    internal static Texture2D? ContractFrame => _contractFrame ??= Load("contract_panel.png");
    internal static Texture2D? MalleableIcon => _malleableIcon ??= Load("malleable_icon.png");
    internal static Texture2D? AmbergrisIcon => _ambergrisIcon ??= Load("ambergris_potion.png");

    internal static Texture2D? Load(string file)
    {
        try
        {
            using Stream? stream = typeof(MainFile).Assembly.GetManifestResourceStream(
                $"ChallengePoints.ChallengePointsAssets.{file}");
            if (stream is null) return null;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var image = new Image();
            if (image.LoadPngFromBuffer(buffer.ToArray()) != Error.Ok) return null;
            return ImageTexture.CreateFromImage(image);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[ChallengePoints] art {file}: {ex.Message}");
            return null;
        }
    }
}
