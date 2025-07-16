using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;
using System.Threading.Tasks;
using System;
using System.Drawing;
using System.Threading;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;
using ExileCore.Shared;
using Newtonsoft.Json;

namespace Follower;

public enum PathStatus
{
    Clear,              // The path is fully walkable
    Dashable,           // The path is blocked by a dashable obstacle (terrain value 2)
    Blocked,            // The path is blocked by an impassable wall or terrain (terrain value 255)
    Invalid             // The start or end point is out of bounds
}
public class FollowerComplex : BaseSettingsPlugin<FollowerSettings>
{
    // This is a backup of the complex version before simplification
    // Renamed class to avoid conflicts
    // Original implementation preserved for reference
} 