global using System.Diagnostics.CodeAnalysis;
global using System.Numerics;
global using System.Runtime.InteropServices;

global using AtcSoft.VideoEngine.FFmpeg;
global using AtcSoft.VideoEngine.Helpers;
global using Flyleaf.FFmpeg;
global using Microsoft.Extensions.Logging;
global using Vortice;
global using Vortice.Direct3D;
global using Vortice.Direct3D11;
global using Vortice.DirectComposition;
global using Vortice.DXGI;
global using Vortice.Mathematics;

global using static Flyleaf.FFmpeg.Raw;

global using ID3D11Device = Vortice.Direct3D11.ID3D11Device;
global using ID3D11DeviceContext = Vortice.Direct3D11.ID3D11DeviceContext;
global using ID3D11Texture2D = Vortice.Direct3D11.ID3D11Texture2D;
global using ID3D11VideoContext = Vortice.Direct3D11.ID3D11VideoContext;
global using ID3D11VideoDevice = Vortice.Direct3D11.ID3D11VideoDevice;