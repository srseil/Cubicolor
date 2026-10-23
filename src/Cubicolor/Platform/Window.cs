using System;

namespace Cubicolor.Platform;

// The game's window as the game sees it: its size in points (the UI's space; the renderer draws at
// Width × PixelDensity pixels), focus, fullscreen, and whether the player asked to close it.
public interface IWindow : IDisposable
{
    int Width { get; }
    int Height { get; }
    float PixelDensity { get; }
    bool IsFocused { get; }
    bool IsFullscreen { get; }
    bool CloseRequested { get; }
    void SetFullscreen(bool fullscreen);
}
