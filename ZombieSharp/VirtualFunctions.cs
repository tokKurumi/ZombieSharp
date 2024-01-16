﻿using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

namespace ZombieSharp
{
    public partial class ZombieSharp
    {
        MemoryFunctionVoid<CCSPlayerController, CCSPlayerPawn, bool, bool> CBasePlayerController_SetPawnFunc;

        public CHandle<CLogicRelay> RespawnRelay = null;

        public void VirtualFunctionsInitialize()
        {
            CBasePlayerController_SetPawnFunc = new(GameData.GetSignature("CBasePlayerController_SetPawn"));

            MemoryFunctionWithReturn<CCSPlayer_WeaponServices, CBasePlayerWeapon, bool> CCSPlayer_WeaponServices_CanUseFunc = new(GameData.GetSignature("CCSPlayer_WeaponServices_CanUse"));
            CCSPlayer_WeaponServices_CanUseFunc.Hook(OnWeaponCanUse, HookMode.Pre);

            VirtualFunctions.CBaseEntity_TakeDamageOldFunc.Hook(OnTakeDamage, HookMode.Pre);
            VirtualFunctions.AcceptInputFunc.Hook(OnEntityInstanceAcceptInput, HookMode.Post);

            MemoryFunctionVoid<CEntityIdentity, IntPtr, CEntityInstance, CEntityInstance, string, int> CEntityIdentity_AcceptInputFunc = new(GameData.GetSignature("CEntityIdentity_AcceptInput"));
            CEntityIdentity_AcceptInputFunc.Hook(OnEntityIdentityAcceptInput, HookMode.Pre);
        }

        private HookResult OnWeaponCanUse(DynamicHook hook)
        {
            var weaponservices = hook.GetParam<CCSPlayer_WeaponServices>(0);
            var clientweapon = hook.GetParam<CBasePlayerWeapon>(1);

            var client = new CCSPlayerController(weaponservices!.Pawn.Value.Controller.Value!.Handle);

            //Server.PrintToChatAll($"{client.PlayerName}: {CCSPlayer_WeaponServices_CanUseFunc.Invoke(weaponservices, clientweapon)}");

            if (ZombieSpawned)
            {
                if (IsClientZombie(client))
                {
                    if (clientweapon.DesignerName != "weapon_knife")
                    {
                        hook.SetReturn(false);
                        return HookResult.Handled;
                    }
                }
            }

            return HookResult.Continue;
        }

        private HookResult OnTakeDamage(DynamicHook hook)
        {
            var client = hook.GetParam<CEntityInstance>(0);
            var damageInfo = hook.GetParam<CTakeDamageInfo>(1);

            var attackInfo = damageInfo.Attacker;

            // var controller = player(client);

            bool warmup = GetGameRules().WarmupPeriod;

            if (warmup && !ConfigSettings.EnableOnWarmup)
            {
                if (client.DesignerName == "player" && attackInfo.Value.DesignerName == "player")
                {
                    damageInfo.Damage = 0;
                    return HookResult.Handled;
                }
            }

            // Server.PrintToChatAll($"{controller.PlayerName} take damaged");
            return HookResult.Continue;
        }

        public HookResult OnEntityInstanceAcceptInput(DynamicHook hook)
        {
            var identity = hook.GetParam<CEntityInstance>(0).Entity;
            var input = hook.GetParam<string>(1);

            // Server.PrintToChatAll($"Found the entity {identity.Name} with {input}");

            if (RespawnRelay != null && RespawnRelay.IsValid)
            {
                CLogicRelay relay = RespawnRelay.Value;

                if (relay.Entity == identity)
                {
                    if (input == "Trigger")
                        ToggleRespawn();

                    else if (input == "Enable" && !RespawnEnable)
                        ToggleRespawn(true, true);

                    else if (input == "Disable" && RespawnEnable)
                        ToggleRespawn(true, false);
                }
            }

            return HookResult.Continue;
        }


        private HookResult OnEntityIdentityAcceptInput(DynamicHook hook)
        {
            var identity = hook.GetParam<CEntityIdentity>(0);
            var input = hook.GetParam<IntPtr>(1);

            // var stringinput = Utilities.ReadStringUtf8(input);
            var stringinput = Schema.GetUtf8String(input, "CUtlSymbolLarge", "m_pString");

            Server.PrintToChatAll($"Found: {identity.Name} input: {stringinput}");

            return HookResult.Continue;
        }

        public void RespawnClient(CCSPlayerController client)
        {
            if (!client.IsValid || client.PawnIsAlive || client.TeamNum < 2)
                return;

            var clientPawn = client.PlayerPawn.Value;

            CBasePlayerController_SetPawnFunc.Invoke(client, clientPawn, true, false);
            VirtualFunction.CreateVoid<CCSPlayerController>(client.Handle, GameData.GetOffset("CCSPlayerController_Respawn"))(client);
        }

        public static CCSPlayerController player(CEntityInstance instance)
        {
            if (instance == null)
            {
                return null;
            }

            if (instance.DesignerName != "player")
            {
                return null;
            }

            // grab the pawn index
            int player_index = (int)instance.Index;

            // grab player controller from pawn
            CCSPlayerPawn player_pawn = Utilities.GetEntityFromIndex<CCSPlayerPawn>(player_index);

            // pawn valid
            if (player_pawn == null || !player_pawn.IsValid)
            {
                return null;
            }

            // controller valid
            if (player_pawn.OriginalController == null || !player_pawn.OriginalController.IsValid)
            {
                return null;
            }

            // any further validity is up to the caller
            return player_pawn.OriginalController.Value;
        }
    }
}
