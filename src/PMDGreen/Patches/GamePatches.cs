namespace PMDGreen.Patches;

internal static class GamePatches
{
    public static void Install()
    {
        StagedSprites.Install();
        EntitySlots.Install();
        FileSystem.Install();
        GameFrame.Install();
        RecompInput.Install();
        Tilemaps.Install();
        GroundMap.Install();
        GroundCamera.Install();

        MotionReport.Install();
        DungeonMotion.Install();
        GroundMotion.Install();
        WorldSprites.Install();

        WideSprites.Install();
        TitleSea.Install();
        MarginDraw.Install();
        MarginSprites.Install();
        DungeonMargins.Install();
        DungeonDarkness.Install();
        GroundMargins.Install();
        PictureMapMargins.Install();
        ScreenEdgeLine.Install();
        MenuPictures.Install();
        OpeningPicture.Install();
        MusicVolume.Install();
    }
}
