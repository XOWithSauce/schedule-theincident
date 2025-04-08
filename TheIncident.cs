using MelonLoader;
using System.Collections;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Police;
using ScheduleOne.NPCs;
using UnityEngine;
using ScheduleOne.GameTime;
using ScheduleOne.AvatarFramework.Equipping;
using ScheduleOne.Persistence;

[assembly: MelonInfo(typeof(TheIncident.TheIncident), TheIncident.BuildInfo.Name, TheIncident.BuildInfo.Version, TheIncident.BuildInfo.Author, TheIncident.BuildInfo.DownloadLink)]
[assembly: MelonColor()]
[assembly: MelonOptionalDependencies("FishNet.Runtime")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace TheIncident
{
    public static class BuildInfo
    {
        public const string Name = "TheIncident";
        public const string Description = "A deadly disease hits the Hyland Point";
        public const string Author = "XOWithSauce";
        public const string Company = null;
        public const string Version = "1.0";
        public const string DownloadLink = null;
    }
    public class TheIncident : MelonMod
    {
        NPC[] npcs;
        List<object> coros = new();
        AvatarEquippable taser;
        AvatarEquippable gunPref;
        Dictionary<Light, Color> lightsColors = new();
        float sleepTime = 0.05f;
        private bool registered = false;

        public override void OnApplicationStart()
        {
            // MelonLogger.Msg("An Unknown Incident looms over the Hyland Point...");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (buildIndex == 1)
            {
                if (LoadManager.Instance != null && !registered)
                {
                    LoadManager.Instance.onLoadComplete.AddListener(OnLoadCompleteCb);
                    registered = true;
                }
            }

            else
            {
                foreach (object coro in coros)
                {
                    MelonCoroutines.Stop(coro);
                }
                coros.Clear();
            }
        }

        private void OnLoadCompleteCb()
        {
            this.npcs = UnityEngine.Object.FindObjectsOfType<NPC>(true);
            Light[] lights = UnityEngine.Object.FindObjectsOfType<Light>(true);

            for (int i = 0; i < lights.Length; i++)
                lightsColors.Add(lights[i], lights[i].color);

            this.coros.Add(MelonCoroutines.Start(this.IncidentBegin()));
        }

        private IEnumerator LightsRed()
        {
            // MelonLogger.Msg($"LightsCount: {lights.Length}");
            if (lightsColors.Count > 0)
            {
                for (int i = 0; i < lightsColors.Count; i++)
                {
                    yield return new WaitForSeconds(0.02f);
                    try
                    {
                        Light l = lightsColors.ElementAt(i).Key;
                        if (l != null)
                            l.color = Color.red;
                    }
                    catch (Exception ex)
                    {
                        // MelonLogger.Warning($"Failed to change light color: {ex}");
                    }
                }
            }
        }

        private IEnumerator LightsClear()
        {
            // MelonLogger.Msg("The event lights ends...");
            for (int i = 0; i < lightsColors.Count; i++)
            {
                yield return new WaitForSeconds(0.02f);
                try
                {
                    Light l = lightsColors.ElementAt(i).Key;
                    if (l != null)
                        l.color = lightsColors.ElementAt(i).Value;
                }
                catch (Exception ex)
                {
                    // MelonLogger.Warning($"Failed to change light color: {ex}");
                }
            }
        }

        private IEnumerator IncidentBegin()
        {
            int startTime = 1855; // 18:55

            for (; ; )
            {
                yield return new WaitForSeconds(30f);
                EDay currentDay = TimeManager.Instance.CurrentDay;
                int currentTime = TimeManager.Instance.CurrentTime;
                // MelonLogger.Msg("Time: " + currentTime + " " + currentDay);

                if (currentDay.ToString().Contains("Sunday") && startTime < currentTime)
                {
                    // MelonLogger.Msg("UNKNOWN DISEASE ENTERS THE HYLAND POINT CITY\nCITIZENS MUST STAY INDOORS!");

                    MelonCoroutines.Start(this.LightsRed());

                    Player[] players = UnityEngine.Object.FindObjectsOfType<Player>(true);
                    yield return new WaitForSeconds(0.5f);
                    // MelonLogger.Msg($"PlayersFound");
                    PoliceOfficer[] offcs = UnityEngine.Object.FindObjectsOfType<PoliceOfficer>(true);
                    yield return new WaitForSeconds(0.5f);
                    // MelonLogger.Msg($"PoliceFOund");

                    foreach (PoliceOfficer offc in offcs)
                    {
                        yield return new WaitForSeconds(0.1f);
                        if (gunPref == null && offc.GunPrefab != null)
                            gunPref = offc.GunPrefab;
                        if (taser == null && offc.TaserPrefab != null)
                            taser = offc.TaserPrefab;

                        offc.GunPrefab = null;
                        offc.TaserPrefab = null;
                        offc.Health.MaxHealth = 300f;
                        offc.Health.Revive();
                        offc.ProxCircle.SetRadius(0.01f);
                    }

                    List<NPC> nearbyNPCs = new();
                    while (currentDay.ToString().Contains("Sunday"))
                    {
                        yield return new WaitForSeconds(sleepTime*10);
                        // MelonLogger.Msg("Evaluate during incident");

                        currentDay = TimeManager.Instance.CurrentDay;
                        currentTime = TimeManager.Instance.CurrentTime;

                        Player randomPlayer = players[UnityEngine.Random.Range(0, players.Length)];
                        foreach (NPC npc in npcs)
                        {
                            yield return new WaitForSeconds(sleepTime);
                            float distanceToP = Vector3.Distance(npc.transform.position, randomPlayer.transform.position);
                            if (distanceToP < 30f)
                            {
                                nearbyNPCs.Add(npc);
                                npc.Avatar.Effects.SetZombified(true);
                            }

                            if (npc.Health.IsDead)
                                npc.Health.Revive();

                            if (npc.Health.IsKnockedOut)
                                npc.behaviour.UnconsciousBehaviour.SendEnd();

                            if (npc is PoliceOfficer offc)
                                offc.behaviour.CombatBehaviour.SetTarget(null, randomPlayer.NetworkObject);

                            if (nearbyNPCs.Count >= 12)
                                break;
                        }

                        if (nearbyNPCs.Count > 0)
                        {
                            sleepTime = 0.05f;
                            // MelonLogger.Msg("NPCS Attack");
                            foreach(NPC npc in nearbyNPCs)
                            {
                                yield return new WaitForSeconds(0.02f);
                                npc.OverrideAggression(1f);
                                npc.Movement.RunSpeed = UnityEngine.Random.Range(2f, 12f);
                                npc.behaviour.CombatBehaviour.VirtualPunchWeapon.Damage = UnityEngine.Random.Range(0.01f, 0.08f);

                                yield return new WaitForSeconds(0.02f);

                                npc.Avatar.LoadNaked();
                                npc.Avatar.Effects.SetZombified(true);
                                npc.Avatar.Effects.GurgleSound.Play();
                                npc.Movement.Disoriented = true;

                                yield return new WaitForSeconds(0.02f);

                                if (npc.isInBuilding )
                                {
                                    npc.ExitBuilding();
                                    npc.behaviour.CombatBehaviour.Enable_Networked(null);
                                    npc.Movement.GetClosestReachablePoint(randomPlayer.transform.position, out Vector3 pos);
                                    npc.Movement.SetDestination(pos);
                                }

                                yield return new WaitForSeconds(0.02f);

                                if (npc.IsInVehicle)
                                {
                                    npc.ExitVehicle();
                                    npc.behaviour.CombatBehaviour.Enable_Networked(null);
                                    npc.Movement.GetClosestReachablePoint(randomPlayer.transform.position, out Vector3 pos);
                                    npc.Movement.SetDestination(pos);
                                }

                                yield return new WaitForSeconds(0.02f);
                                npc.behaviour.CombatBehaviour.SetTarget(null, randomPlayer.NetworkObject);
                                npc.behaviour.CombatBehaviour.Enable_Networked(null);
                               
                            }
                        }
                        else
                        {
                            sleepTime = 0.5f;
                            int roll = UnityEngine.Random.Range(1, 100);
                            int i = 0;
                            if (roll > 95)
                            {
                                foreach (NPC npc in npcs) 
                                {
                                    if (i > 3)
                                        break;
                                    yield return new WaitForSeconds(sleepTime);
                                    if (Vector3.Distance(npc.transform.position, randomPlayer.transform.position) < 100)
                                    {
                                        i++;
                                        float xInitOffset = UnityEngine.Random.Range(15f, 30f);
                                        float zInitOffset = UnityEngine.Random.Range(15f, 30f);
                                        xInitOffset *= UnityEngine.Random.Range(0f, 1f) > 0.5f ? 1f : -1f;
                                        zInitOffset *= UnityEngine.Random.Range(0f, 1f) > 0.5f ? 1f : -1f;
                                        Vector3 targetWarpPosition = randomPlayer.transform.position + new Vector3(xInitOffset, 0f, zInitOffset);
                                        npc.Movement.GetClosestReachablePoint(targetWarpPosition, out Vector3 warpInit);
                                        npc.Movement.Warp(warpInit);
                                        npc.Movement.WarpToNavMesh();
                                    }
                                }
                            }
                        }
                        nearbyNPCs.Clear();
                    }

                    // Event ends
                    // MelonLogger.Msg("The event npcs ends...");
                    foreach(NPC npc in npcs)
                    {
                        yield return new WaitForSeconds(0.1f);
                        npc.ResetAggression();
                        npc.Movement.RunSpeed = 7f;
                        npc.behaviour.CombatBehaviour.VirtualPunchWeapon.Damage = 0.25f;
                        npc.behaviour.CombatBehaviour.SendEnd();
                        npc.Movement.Disoriented = false;

                        if (npc.Health.IsDead)
                            npc.behaviour.DeadBehaviour.SendEnd();

                        if (npc.Health.IsKnockedOut)
                            npc.behaviour.UnconsciousBehaviour.SendEnd();
                    }

                    // MelonLogger.Msg("The event offcs ends...");
                    foreach (PoliceOfficer offc in offcs)
                    {
                        yield return new WaitForSeconds(0.1f);
                        offc.GunPrefab = gunPref;
                        offc.TaserPrefab = taser;
                        if (gunPref != null && offc.GunPrefab == null)
                            offc.GunPrefab = gunPref;

                        offc.ResetAggression();

                        if (taser != null && offc.TaserPrefab == null)
                            offc.TaserPrefab = taser;
                        yield return new WaitForSeconds(0.1f);
                        offc.ProxCircle.SetRadius(2f);
                        yield return new WaitForSeconds(0.1f);
                        offc.PursuitBehaviour.SendEnd();
                        offc.behaviour.CombatBehaviour.SendEnd();
                    }

                    MelonCoroutines.Start(this.LightsClear());
                }
            }
        }
    }
}
