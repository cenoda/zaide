namespace Zaide.Features.Townhall.Presentation;

/// <summary>
/// Pure scroll-follow rules for the Townhall chat surface.
/// </summary>
internal static class TownhallChatScrollPolicy
{
    public const double NearBottomThresholdPixels = 48.0;

    public static bool IsNearBottom(
        double offsetY,
        double maxOffsetY,
        double threshold = NearBottomThresholdPixels) =>
        maxOffsetY <= 0 || offsetY >= maxOffsetY - threshold;

    public static bool ShouldAutoFollowOnAppend(bool wasNearBottomBeforeAppend) =>
        wasNearBottomBeforeAppend;
}
