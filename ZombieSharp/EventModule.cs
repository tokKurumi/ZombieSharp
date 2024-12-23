using CounterStrikeSharp.API.Modules.Cvars;
using Microsoft.Extensions.Logging;

namespace ZombieSharp
{
    public partial class ZombieSharp
    {
        public CounterStrikeSharp.API.Modules.Timers.Timer RoundTimer = null;

        bool ClassIsLoaded = false;

        public void EventInitialize()
        {
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventRoundFreezeEnd>(OnRoundFreezeEnd);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterEventHandler<EventPlayerTeam>(OnPlayerTeam);
            RegisterEventHandler<EventPlayerJump>(OnPlayerJump);
            RegisterEventHandler<EventCsPreRestart>(OnPreRestart);

            RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnected);
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterListener<Listeners.OnServerPrecacheResources>(OnPrecacheResources);
        }

        // bot can only be initial here only
        private void OnClientPutInServer(int client)
        {
            var player = Utilities.GetPlayerFromSlot(client);

            InitialClientData(player);

            Logger.LogInformation($"Player: {player.PlayerName} is put in server with {player.Slot}");
        }

        private void InitialClientData(CCSPlayerController player)
        {
            int clientindex = player.Slot;

            ClientSpawnDatas.Add(clientindex, new ClientSpawnData());

            ZombiePlayers.Add(clientindex, new ZombiePlayer());

            ZombiePlayers[clientindex].IsZombie = false;
            ZombiePlayers[clientindex].MotherZombieStatus = MotherZombieFlags.NONE;

            PlayerDeathTime.Add(clientindex, 0.0f);

            RegenTimer.Add(clientindex, null);

            PlayerSettingsOnPutInServer(player);

            WeaponOnClientPutInServer(clientindex);

            ClientProtected.Add(clientindex, new());

            TopDefenderOnPutInServer(player);

            GrenadeEffectOnClientPutInServer(player);

            ZombieVoiceOnClientPutInServer(player);

            Logger.LogInformation($"Player: {player.PlayerName} data is initialized with {player.Slot}");
        }

        // Normal player will be hook here.
        private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            var client = @event.Userid;

            if (client.IsBot)
                return HookResult.Continue;

            Logger.LogInformation($"Player: {client.PlayerName} is fully connected with {client.Slot}");

            var AuthTask = PlayerSettingsAuthorized(client);
            var StatTask = StatsSetData(client);

            Task.WhenAll(AuthTask, StatTask).Wait();

            return HookResult.Continue;
        }

        private HookResult OnPlayerTeam(EventPlayerTeam @event, GameEventInfo info)
        {
            if (!CVAR_RespawnLate.Value)
                return HookResult.Continue;

            var client = @event.Userid;
            var team = @event.Team;

            if (client.IsBot || !client.IsValid)
                return HookResult.Continue;

            if (team > (int)CsTeam.Spectator)
                AddTimer(1.0f, () => RespawnClient(client));

            return HookResult.Continue;
        }

        private void OnClientDisconnected(int client)
        {
            var player = Utilities.GetPlayerFromSlot(client);

            int clientindex = player.Slot;

            ClientSpawnDatas.Remove(clientindex);
            ZombiePlayers.Remove(clientindex);
            ClientPlayerClass.Remove(clientindex);
            PlayerDeathTime.Remove(clientindex);

            RegenTimerStop(player);
            RegenTimer.Remove(clientindex);

            WeaponOnClientDisconnect(clientindex);

            ClientProtected.Remove(clientindex);

            TopDefenderOnDisconnect(player);

            GrenadeEffectOnClientDisconnect(player);

            ZombieVoiceOnClientDisconnect(player);

            Logger.LogInformation($"Player: {player.PlayerName} data is removed with {player.Slot}");
        }

        private void OnMapStart(string mapname)
        {
            WeaponInitialize();
            SettingsIntialize(mapname);
            ClassIsLoaded = PlayerClassIntialize();

            hitgroupLoad = HitGroupIntialize();
            RepeatKillerOnMapStart();

            Server.ExecuteCommand("mp_ignore_round_win_conditions 0");
        }

        private void OnPrecacheResources(ResourceManifest manifest)
        {
            if (ClassIsLoaded)
                PrecachePlayerModel(manifest);

            manifest.AddResource("particles/burning_fx/env_fire_medium.vpcf");
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            RemoveRoundObjective();

            if(CVAR_RespawnEnableRelay.Value)
                RespawnTogglerSetup();

            Server.PrintToChatAll($" {Localizer["Prefix"]} {Localizer["Goal"]}");


            return HookResult.Continue;
        }

        private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
        {
            if (enableWarmupOnline)
            {
                bool warmup = GetGameRules().WarmupPeriod;

                if (warmup && !CVAR_EnableOnWarmup.Value)
                    Server.PrintToChatAll($" {Localizer["Prefix"]} {Localizer["Warmup.Disabled"]}");

                if (!warmup || CVAR_EnableOnWarmup.Value)
                {
                    if (!warmup)
                    {
                        var roundtimeCvar = ConVar.Find("mp_roundtime");
                        RoundTimer = AddTimer(roundtimeCvar.GetPrimitiveValue<float>() * 60f, TerminateRoundTimeOut);
                    }

                    Server.ExecuteCommand("mp_ignore_round_win_conditions 1");

                    InfectOnRoundFreezeEnd();
                }
                else
                {
                    Server.ExecuteCommand("mp_ignore_round_win_conditions 0");
                }
            }
            else
            {
                var roundtimeCvar = ConVar.Find("mp_roundtime");
                RoundTimer = AddTimer(roundtimeCvar.GetPrimitiveValue<float>() * 60f, TerminateRoundTimeOut);
                Server.ExecuteCommand("mp_ignore_round_win_conditions 1");
                InfectOnRoundFreezeEnd();
            }


            return HookResult.Continue;
        }

        private HookResult OnPreRestart(EventCsPreRestart @event, GameEventInfo info)
        {
            if (enableWarmupOnline)
            {
                bool warmup = GetGameRules().WarmupPeriod;

                if (warmup && !CVAR_EnableOnWarmup.Value)
                    return HookResult.Continue;
            }

            foreach(var client in Utilities.GetPlayers())
            {
                ZombiePlayers[client.Slot].IsZombie = false;
            }

            AddTimer(0.1f, () =>
            {
                ToggleRespawn(true, true);
            });

            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            if (RoundTimer != null)
                RoundTimer.Kill();

            TopDenfederOnRoundEnd();
            if (enableWarmupOnline)
            {
                bool warmup = GetGameRules().WarmupPeriod;

                if (warmup && !CVAR_EnableOnWarmup.Value)
                    return HookResult.Continue;
            }

            // Reset Client Status
            AddTimer(0.2f, () =>
            {
                // Reset Zombie Spawned here.
                ZombieSpawned = false;

                // avoiding zombie status glitch on human class like in zombie:reloaded
                List<CCSPlayerController> clientlist = Utilities.GetPlayers();

                // Reset Client Status
                foreach (var client in clientlist)
                {
                    if (!client.IsValid)
                        continue;

                    // if they were chosen as motherzombie then let's make them not to get chosen again.
                    if (ZombiePlayers[client.Slot].MotherZombieStatus == MotherZombieFlags.CHOSEN)
                        ZombiePlayers[client.Slot].MotherZombieStatus = MotherZombieFlags.LAST;
                }
            });

            return HookResult.Continue;
        }

        private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
        {
            if(@event.Userid.Slot == 32766)
                return HookResult.Continue;

            if (ZombieSpawned)
            {
                var client = @event.Userid;
                var attacker = @event.Attacker;

                var weapon = @event.Weapon;
                var dmgHealth = @event.DmgHealth;
                var hitgroup = @event.Hitgroup;

                if (attacker == null || client == null)
                    return HookResult.Continue;

                // reset speed for class.
                AddTimer(0.3f, () =>
                {
                    PlayerClassesApplySpeedOnHurt(client);
                });

                if (IsClientZombie(attacker) && IsClientHuman(client) && string.Equals(weapon, "knife") && !ClientProtected[client.Slot].Protected)
                {
                    // Server.PrintToChatAll($"{client.PlayerName} Infected by {attacker.PlayerName}");
                    InfectClient(client, attacker);
                }

                if (IsClientZombie(client))
                {
                    if (CVAR_CashOnDamage.Value)
                        DamageCash(attacker, dmgHealth);

                    if(Server.EngineTime > ClientVoiceData[client])
                    {
                        ZombiePain(client);
                        ClientVoiceData[client] = Server.EngineTime + 10f;
                    }

                    if (weapon == "hegrenade")
                    {
                        var client_class = ClientPlayerClass[client.Slot].ActiveClass;

                        float time = 5;

                        if(client_class != null)
                        {
                            time = PlayerClassDatas.PlayerClasses[client_class].Napalm_Time;
                        }

                        IgniteClient(client, time);
                    }

                    else
                    {
                        FindWeaponItemDefinition(attacker.PlayerPawn.Value.WeaponServices.ActiveWeapon, weapon);

                        //Server.PrintToChatAll($"{client.PlayerName} get hit at {hitgroup}");
                        KnockbackClient(client, attacker, dmgHealth, weapon, hitgroup);
                    }

                    TopDefenderOnPlayerHurt(attacker, dmgHealth);
                }
            }

            return HookResult.Continue;
        }

        private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            if (@event.Userid.Slot == 32766)
                return HookResult.Continue;

            var client = @event.Userid;
            var attacker = @event.Attacker;
            var weapon = @event.Weapon;

            client.PawnIsAlive = false;

            if (ZombieSpawned)
            {
                CheckGameStatus();

                if (CVAR_EnableStats.Value)
                {
                    if (!attacker.IsBot && !attacker.IsHLTV && IsClientHuman(attacker) && IsClientZombie(client))
                        StatsSetData(attacker, 0, 1, 0).Wait();
                }

                if (RespawnEnable)
                {
                    RespawnPlayer(client);
                    RepeatKillerOnPlayerDeath(client, attacker, weapon);
                }

                if (ClientMoanTimer.ContainsKey(client) && ClientMoanTimer[client] != null)
                {
                    ClientMoanTimer[client].Kill();
                }

                if (ZS_IsClientValid(client) && IsClientZombie(client))
                {
                    AddTimer(0.1f, () => ZombieDie(client));
                }

                RegenTimerStop(client);
            }

            return HookResult.Continue;
        }

        public void RespawnPlayer(CCSPlayerController client)
        {
            if (CVAR_RespawnTimer.Value > 0.0f)
            {
                AddTimer(CVAR_RespawnTimer.Value, () =>
                {
                    if (CVAR_RespawnProtect.Value && CVAR_RespawnTeam.Value == 1)
                        ClientProtected[client.Slot].Protected = true;

                    // Server.PrintToChatAll($"Player {client.PlayerName} should be respawn here.");
                    RespawnClient(client);
                });
            }
        }

        public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            var client = @event.Userid;

            if (enableWarmupOnline)
            {
                bool warmup = GetGameRules().WarmupPeriod;

                if (warmup && !CVAR_EnableOnWarmup.Value)
                    return HookResult.Continue;
            }

            AddTimer(0.1f, () =>
            {
                WeaponOnPlayerSpawn(client.Slot);

                // if zombie already spawned then they become zombie.
                if (ZombieSpawned)
                {
                    // Server.PrintToChatAll($"Infect {client.PlayerName} on Spawn.");
                    if (CVAR_RespawnTeam.Value == 0)
                        InfectClient(client, null, false, false, true);

                    else
                        HumanizeClient(client);

                    if (ClientProtected[client.Slot].Protected)
                    {
                        AddTimer(CVAR_RespawnProtectTime.Value, () => { ResetProtectedClient(client); });
                        RespawnProtectClient(client);
                    }
                }

                // else they're human!
                else
                    HumanizeClient(client);

                var clientPawn = client.PlayerPawn.Value;
                var spawnPos = clientPawn.AbsOrigin!;
                var spawnAngle = clientPawn.AbsRotation!;

                ZTele_GetClientSpawnPoint(client, spawnPos, spawnAngle);
            });

            return HookResult.Continue;
        }

        public HookResult OnPlayerJump(EventPlayerJump @event, GameEventInfo info)
        {
            var client = @event.Userid;

            if (enableWarmupOnline)
            {
                var warmup = GetGameRules().WarmupPeriod;

                if (warmup && !CVAR_EnableOnWarmup.Value)
                    return HookResult.Continue;
            }

            JumpBoost(client);

            return HookResult.Continue;
        }

        public void JumpBoost(CCSPlayerController client)
        {
            var classData = PlayerClassDatas.PlayerClasses;
            var activeclass = ClientPlayerClass[client.Slot].ActiveClass;

            if (enableWarmupOnline)
            {
                bool warmup = GetGameRules().WarmupPeriod;

                if (warmup && !CVAR_EnableOnWarmup.Value)
                    return;
            }

            if (client == null || !client.IsValid || !ZombiePlayers.ContainsKey(client.Slot))
                return;

            AddTimer(0.0f, () =>
            {
                if (activeclass == null)
                {
                    if (IsClientHuman(client))
                        activeclass = Default_Human;

                    else
                        activeclass = Default_Zombie;
                }

                if (classData.ContainsKey(activeclass))
                {
                    client.PlayerPawn.Value.AbsVelocity.X *= classData[activeclass].Jump_Distance;
                    client.PlayerPawn.Value.AbsVelocity.Y *= classData[activeclass].Jump_Distance;
                    client.PlayerPawn.Value.AbsVelocity.Z *= classData[activeclass].Jump_Height * (classData[activeclass].Speed / 250f);
                }
            });
        }

        private void DamageCash(CCSPlayerController client, int dmgHealth)
        {
            var money = client.InGameMoneyServices.Account;
            client.InGameMoneyServices.Account = money + dmgHealth;
            Utilities.SetStateChanged(client, "CCSPlayerController", "m_pInGameMoneyServices");
        }

        private void ResetProtectedClient(CCSPlayerController client)
        {
            if (!client.IsValid)
                return;

            ClientProtected[client.Slot].Protected = false;
            RespawnProtectClient(client, true);
        }

        private void TerminateRoundTimeOut()
        {
            int team = CVAR_TimeoutWinner.Value;

            if (team == 2)
            {
                Z_TerminateRound(5f, RoundEndReason.TerroristsWin);
            }

            else if (team == 3)
            {
                Z_TerminateRound(5f, RoundEndReason.CTsWin);
            }

            else
            {
                Z_TerminateRound(5f, RoundEndReason.RoundDraw);
            }
        }

        private void RemoveRoundObjective()
        {
            var objectivelist = new List<string>() { "func_bomb_target", "func_hostage_rescue", "hostage_entity", "c4" };

            foreach (string objectivename in objectivelist)
            {
                var entityIndex = Utilities.FindAllEntitiesByDesignerName<CEntityInstance>(objectivename);

                foreach (var entity in entityIndex)
                {
                    Logger.LogInformation($"[ZSharp]: Removed {entity.DesignerName}");
                    entity.Remove();
                }
            }
        }
    }
}