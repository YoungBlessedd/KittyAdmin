using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using UnityEngine;

namespace KittyAdmin
{
    public class KittyAdminPlugin : RocketPlugin
    {
        public static KittyAdminPlugin Instance { get; private set; }

        private readonly HashSet<ulong> flyingPlayers = new HashSet<ulong>();
        private readonly HashSet<ulong> buildModePlayers = new HashSet<ulong>();
        private readonly Dictionary<ulong, List<object>> playerBases = new Dictionary<ulong, List<object>>();

        protected override void Load()
        {
            Instance = this;

            StructureManager.onDeployStructureRequested += OnDeployStructureRequested;
            BarricadeManager.onDeployBarricadeRequested += OnDeployBarricadeRequested;

            Rocket.Core.Logging.Logger.Log("[KittyAdmin] Loaded successfully.");
        }

        protected override void Unload()
        {
            foreach (ulong steamId in new List<ulong>(flyingPlayers))
            {
                UnturnedPlayer player = UnturnedPlayer.FromCSteamID(new CSteamID(steamId));

                if (player != null && player.Player != null)
                    player.Player.movement.sendPluginGravityMultiplier(1f);
            }

            flyingPlayers.Clear();

            StructureManager.onDeployStructureRequested -= OnDeployStructureRequested;
            BarricadeManager.onDeployBarricadeRequested -= OnDeployBarricadeRequested;

            buildModePlayers.Clear();

            foreach (ulong steamId in new List<ulong>(playerBases.Keys))
            {
                RemoveBaseBySteamId(steamId, false);
            }

            playerBases.Clear();
            Instance = null;

            Rocket.Core.Logging.Logger.Log("[KittyAdmin] Unloaded.");
        }

        private void Update()
        {
            if (flyingPlayers.Count == 0)
                return;

            foreach (ulong steamId in new List<ulong>(flyingPlayers))
            {
                UnturnedPlayer player = UnturnedPlayer.FromCSteamID(new CSteamID(steamId));

                if (player == null || player.Player == null)
                    continue;

                HandleFlight(player);
            }
        }

        // =========================
        // KittyFly
        // =========================

        private void HandleFlight(UnturnedPlayer player)
        {
            player.Player.movement.sendPluginGravityMultiplier(0f);

            // Space = up.
            if (player.Player.input.keys.Length > 0 && player.Player.input.keys[0])
            {
                player.Player.movement.sendPluginGravityMultiplier(-0.35f);
            }
            // Shift/crouch = down.
            else if (player.Player.input.keys.Length > 5 && player.Player.input.keys[5])
            {
                player.Player.movement.sendPluginGravityMultiplier(0.35f);
            }

            if (player.Stance != EPlayerStance.SWIM)
                player.Player.stance.stance = EPlayerStance.SWIM;
        }

        public void ToggleFly(UnturnedPlayer player)
        {
            ulong steamId = player.CSteamID.m_SteamID;

            if (flyingPlayers.Contains(steamId))
                DisableFly(player);
            else
                EnableFly(player);
        }

        private void EnableFly(UnturnedPlayer player)
        {
            flyingPlayers.Add(player.CSteamID.m_SteamID);
            player.Player.movement.sendPluginGravityMultiplier(0f);

            UnturnedChat.Say(
                player,
                "<color=#f1c40f><b>KittyAdmin Fly</b></color>\n" +
                "<color=#2ecc71>Fly mode enabled.</color>",
                Color.white,
                true);
        }

        private void DisableFly(UnturnedPlayer player)
        {
            flyingPlayers.Remove(player.CSteamID.m_SteamID);
            player.Player.movement.sendPluginGravityMultiplier(1f);

            UnturnedChat.Say(
                player,
                "<color=#f1c40f><b>KittyAdmin Fly</b></color>\n" +
                "<color=#2ecc71>Fly mode disabled.</color>",
                Color.white,
                true);
        }

        // =========================
        // KittyBuildMode
        // =========================

        private void ToggleBuildMode(UnturnedPlayer player)
        {
            ulong steamId = player.CSteamID.m_SteamID;

            if (buildModePlayers.Contains(steamId))
            {
                buildModePlayers.Remove(steamId);
                UnturnedChat.Say(player, "Buildmode disabled.", Color.yellow);
                return;
            }

            buildModePlayers.Add(steamId);
            UnturnedChat.Say(
                player,
                "Buildmode enabled. Placed building items will be returned.",
                Color.green);
        }

        private bool IsBuildMode(ulong steamId)
        {
            return buildModePlayers.Contains(steamId);
        }

        private void OnDeployStructureRequested(
            Structure structure,
            ItemStructureAsset asset,
            ref Vector3 point,
            ref float angle_x,
            ref float angle_y,
            ref float angle_z,
            ref ulong owner,
            ref ulong group,
            ref bool shouldAllow)
        {
            if (!shouldAllow || !IsBuildMode(owner))
                return;

            UnturnedPlayer player =
                UnturnedPlayer.FromCSteamID(new CSteamID(owner));

            if (player == null || !player.IsAdmin)
                return;

            player.GiveItem(asset.id, 1);
        }

        private void OnDeployBarricadeRequested(
            Barricade barricade,
            ItemBarricadeAsset asset,
            Transform hit,
            ref Vector3 point,
            ref float angle_x,
            ref float angle_y,
            ref float angle_z,
            ref ulong owner,
            ref ulong group,
            ref bool shouldAllow)
        {
            if (!shouldAllow || !IsBuildMode(owner))
                return;

            UnturnedPlayer player =
                UnturnedPlayer.FromCSteamID(new CSteamID(owner));

            if (player == null || !player.IsAdmin)
                return;

            player.GiveItem(asset.id, 1);
        }

        // =========================
        // KittyBase
        // =========================

        public void BuildBase(UnturnedPlayer player)
        {
            ulong steamId = player.CSteamID.m_SteamID;
            RemoveBaseBySteamId(steamId, false);

            Vector3 center = player.Position + new Vector3(0f, 1f, 0f);
            List<object> spawned = new List<object>();

            // Maple is the standard wood building set.
            // IDs: Floor 31, Doorway 32, Wall 33, Roof 35.
            const ushort floorId = 31;
            const ushort doorwayId = 32;
            const ushort wallId = 33;
            const ushort roofId = 35;

            // 2x2 floors.
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    object drop = SpawnStructure(floorId, center + new Vector3(x, 0f, z), 0f, steamId);
                    if (drop != null) spawned.Add(drop);
                }
            }

            // Walls around the 2x2 footprint. One doorway on the south side.
            for (int x = -1; x <= 1; x += 2)
            {
                object drop = SpawnStructure(x == 1 ? doorwayId : wallId, center + new Vector3(x, 2f, -2f), 0f, steamId);
                if (drop != null) spawned.Add(drop);

                drop = SpawnStructure(wallId, center + new Vector3(x, 2f, 2f), 0f, steamId);
                if (drop != null) spawned.Add(drop);
            }

            for (int z = -1; z <= 1; z += 2)
            {
                object drop = SpawnStructure(wallId, center + new Vector3(-2f, 2f, z), 90f, steamId);
                if (drop != null) spawned.Add(drop);
            }

            // Roof pieces.
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    object drop = SpawnStructure(roofId, center + new Vector3(x, 4f, z), 0f, steamId);
                    if (drop != null) spawned.Add(drop);
                }
            }

            playerBases[steamId] = spawned;

            if (spawned.Count == 0)
            {
                UnturnedChat.Say(player, "<color=#e74c3c>KittyBase could not spawn the base. Check the server log.</color>", Color.white, true);
                return;
            }

            UnturnedChat.Say(
                player,
                "<color=#f1c40f><b>KittyAdmin Base</b></color>\n<color=#2ecc71>2x2 wooden base spawned above you.</color>",
                Color.white,
                true);
        }

        public void RemoveBase(UnturnedPlayer player)
        {
            if (!RemoveBaseBySteamId(player.CSteamID.m_SteamID, true))
            {
                UnturnedChat.Say(player, "<color=#e74c3c>You do not have a KittyBase to remove.</color>", Color.white, true);
            }
        }

        private bool RemoveBaseBySteamId(ulong steamId, bool message)
        {
            List<object> drops;
            if (!playerBases.TryGetValue(steamId, out drops) || drops == null || drops.Count == 0)
                return false;

            foreach (object drop in drops)
                TryDestroyStructureDrop(drop);

            playerBases.Remove(steamId);

            if (message)
            {
                UnturnedPlayer player = UnturnedPlayer.FromCSteamID(new CSteamID(steamId));
                if (player != null)
                    UnturnedChat.Say(
                        player,
                        "<color=#f1c40f><b>KittyAdmin Base</b></color>\n<color=#2ecc71>Your KittyBase was removed.</color>",
                        Color.white,
                        true);
            }

            return true;
        }

        private object SpawnStructure(ushort id, Vector3 point, float yaw, ulong owner)
        {
            try
            {
                Type structureType = typeof(Provider).Assembly.GetType("SDG.Unturned.Structure");
                Type managerType = typeof(Provider).Assembly.GetType("SDG.Unturned.StructureManager");
                if (structureType == null || managerType == null)
                    return null;

                object structure = CreateStructure(structureType, id);
                if (structure == null)
                    return null;

                MethodInfo method = managerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => string.Equals(m.Name, "dropStructure", StringComparison.OrdinalIgnoreCase));

                if (method == null)
                {
                    method = managerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name.IndexOf("dropStructure", StringComparison.OrdinalIgnoreCase) >= 0);
                }

                if (method == null)
                {
                    Rocket.Core.Logging.Logger.LogError("[KittyAdmin] Could not find StructureManager.dropStructure.");
                    return null;
                }

                ParameterInfo[] p = method.GetParameters();
                object[] args = new object[p.Length];
                for (int i = 0; i < p.Length; i++)
                {
                    Type t = p[i].ParameterType;
                    string n = p[i].Name == null ? "" : p[i].Name.ToLowerInvariant();

                    if (t == structureType) args[i] = structure;
                    else if (t == typeof(Transform)) args[i] = null;
                    else if (t == typeof(Vector3)) args[i] = point;
                    else if (t == typeof(float)) args[i] = n.Contains("angle") && (n.Contains("y") || n.EndsWith("_y")) ? yaw : 0f;
                    else if (t == typeof(ulong)) args[i] = owner;
                    else if (t == typeof(uint)) args[i] = 0u;
                    else if (t == typeof(byte)) args[i] = 0;
                    else if (t == typeof(ushort)) args[i] = id;
                    else if (t == typeof(CSteamID)) args[i] = new CSteamID(owner);
                    else if (t == typeof(Quaternion)) args[i] = Quaternion.Euler(0f, yaw, 0f);
                    else if (t.IsValueType) args[i] = Activator.CreateInstance(t);
                    else args[i] = null;
                }

                object result = method.Invoke(null, args);
                return result;
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogError("[KittyAdmin] Failed to spawn structure: " + ex.GetBaseException().Message);
                return null;
            }
        }

        private object CreateStructure(Type structureType, ushort id)
        {
            foreach (ConstructorInfo ctor in structureType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                ParameterInfo[] p = ctor.GetParameters();
                if (p.Length == 2 && p[0].ParameterType == typeof(ushort) && p[1].ParameterType == typeof(byte[]))
                    return ctor.Invoke(new object[] { id, new byte[0] });
                if (p.Length == 1 && p[0].ParameterType == typeof(ushort))
                    return ctor.Invoke(new object[] { id });
            }

            Rocket.Core.Logging.Logger.LogError("[KittyAdmin] Could not construct Unturned Structure object.");
            return null;
        }

        private void TryDestroyStructureDrop(object drop)
        {
            if (drop == null) return;

            try
            {
                Type managerType = typeof(Provider).Assembly.GetType("SDG.Unturned.StructureManager");
                if (managerType == null) return;

                Type dropType = drop.GetType();
                MethodInfo method = managerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m =>
                        m.Name.IndexOf("destroyStructure", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType.IsAssignableFrom(dropType));

                if (method != null)
                {
                    method.Invoke(null, new object[] { drop });
                    return;
                }

                PropertyInfo transformProp = dropType.GetProperty("model", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                Transform transform = transformProp == null ? null : transformProp.GetValue(drop, null) as Transform;
                if (transform != null)
                    UnityEngine.Object.Destroy(transform.gameObject);
            }
            catch (Exception ex)
            {
                Rocket.Core.Logging.Logger.LogError("[KittyAdmin] Failed to remove structure: " + ex.GetBaseException().Message);
            }
        }
    }

    // =========================
    // Commands
    // =========================

    public class CommandFly : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "fly";
        public string Help => "Toggle fly mode.";
        public string Syntax => "";
        public List<string> Aliases => new List<string> { "flight" };
        public List<string> Permissions => new List<string> { "kittyfly.fly" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player == null)
                return;

            KittyAdminPlugin plugin = KittyAdminPlugin.Instance;

            if (plugin == null)
            {
                UnturnedChat.Say(caller, "<color=#e74c3c>KittyAdmin is not loaded.</color>");
                return;
            }

            plugin.ToggleFly(player);
        }
    }

    public class BuildModeCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "buildmode";
        public string Help => "Toggle unlimited building mode.";
        public string Syntax => "/buildmode";
        public List<string> Aliases => new List<string> { "bm" };
        public List<string> Permissions => new List<string>();

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = (UnturnedPlayer)caller;

            if (!player.IsAdmin)
            {
                UnturnedChat.Say(player, "Buildmode is only available to admins.", Color.red);
                return;
            }

            if (KittyAdminPlugin.Instance == null)
            {
                UnturnedChat.Say(player, "KittyAdmin plugin is not loaded.", Color.red);
                return;
            }

            KittyAdminPlugin.Instance.ToggleBuildMode(player);
        }
    }

    public class CommandBuildBase : IRocketCommand
    {
        public AllowedCaller AllowedCaller { get { return AllowedCaller.Player; } }
        public string Name { get { return "buildbase"; } }
        public string Help { get { return "Spawn a 2x2 wooden base above you."; } }
        public string Syntax { get { return ""; } }
        public List<string> Aliases { get { return new List<string>(); } }
        public List<string> Permissions { get { return new List<string> { "kittybase.spawn" }; } }

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player != null && KittyAdminPlugin.Instance != null)
                KittyAdminPlugin.Instance.BuildBase(player);
        }
    }

    public class CommandRemoveBase : IRocketCommand
    {
        public AllowedCaller AllowedCaller { get { return AllowedCaller.Player; } }
        public string Name { get { return "removebase"; } }
        public string Help { get { return "Remove your KittyBase only."; } }
        public string Syntax { get { return ""; } }
        public List<string> Aliases { get { return new List<string>(); } }
        public List<string> Permissions { get { return new List<string> { "kittybase.spawn" }; } }

        public void Execute(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player != null && KittyAdminPlugin.Instance != null)
                KittyAdminPlugin.Instance.RemoveBase(player);
        }
    }
}
