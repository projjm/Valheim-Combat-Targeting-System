using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace CombatTargetingSystem
{
    public struct ModConfig
    {
        public bool cameraFocusEnabled;
        public bool hideCrosshair;
        public bool lockTargetGamepadDoubleTap;

        public KeyCode lockTargetKey;
        public KeyCode toggleModKey;
        public KeyCode toggleCameraFocusKey;
        public string lockTargetGamepadButton;

        public Color targetLockedHealthColor;

        public float scanInterval;
        public float targetMaxDistance;
        public float targetLockMaxDistance;

        public bool focusDisableOnRun;
        public bool focusDisbledWhenInside;

        public float focusRangeDefault;
        public float focusRangeTargetLocked;
        public float focusRangeAfterRun;
        public float focusRangeOnRoll;
        public float focusRangeWhenInside;

        public float focusSpeedDefault;
        public float focusSpeedTargetLocked;
        public float focusSpeedOnRoll;
        public float focusSpeedAfterRun;
        public float focusSpeedWhenInside;

        public float focusmaxDistanceDefault;
        public float focusmaxDistanceAfterRun;
        public float focusmaxDistanceWhenInside;
        public float focusEndDelay;

        public float lastAttackedCooldown;
        public float postRunningCooldown;

        public float w_targetDistance;
        public float w_targetAngleFromCamera;
        public float w_targetAngleFromPlayer;
        public float w_targetFacingPlayer;
        public float w_haveRecentlyAttacked;
        public float w_targetIsDamaged;
    }

    [BepInPlugin("projjm.combattargetingsystem", "Combat Targeting System", "1.1.1")]
    [BepInProcess("valheim.exe")]

    public class CombatTargetingSystem : BaseUnityPlugin
    {
        private static ConfigEntry<bool> c_hideCrosshair;
        private static ConfigEntry<bool> c_cameraFocusEnabled;
        private static ConfigEntry<bool> c_lockTargetGamepadDoubleTap;

        private static ConfigEntry<KeyCode> c_lockTargetKey;
        private static ConfigEntry<KeyCode> c_toggleModKey;
        private static ConfigEntry<KeyCode> c_toggleCameraFocusKey;
        private static ConfigEntry<string> c_lockTargetGamePadButton;

        private static ConfigEntry<Color> c_targetLockedHealthColor;

        private static ConfigEntry<float> c_scanInterval;
        private static ConfigEntry<float> c_targetMaxDistance;
        private static ConfigEntry<float> c_targetLockMaxDistance;

        private static ConfigEntry<bool> c_focusDisableOnRun;
        private static ConfigEntry<bool> c_focusDisableWhenInside;
        private static ConfigEntry<float> c_focusRangeDefault;
        private static ConfigEntry<float> c_focusRangeTargetLocked;
        private static ConfigEntry<float> c_focusRangeAfterRun;
        private static ConfigEntry<float> c_focusRangeOnRoll;
        private static ConfigEntry<float> c_focusRangeWhenInside;

        private static ConfigEntry<float> c_focusSpeedDefault;
        private static ConfigEntry<float> c_focusSpeedTargetLocked;
        private static ConfigEntry<float> c_focusSpeedOnRoll;
        private static ConfigEntry<float> c_focusSpeedAfterRun;
        private static ConfigEntry<float> c_focusSpeedWhenInside;

        private static ConfigEntry<float> c_focusmaxDistanceDefault;
        private static ConfigEntry<float> c_focusmaxDistanceAfterRun;
        private static ConfigEntry<float> c_focusmaxDistanceWhenInside;
        private static ConfigEntry<float> c_focusEndDelay;

        private static ConfigEntry<float> c_lastAttackedCooldown;
        private static ConfigEntry<float> c_postRunningCooldown;

        private static ConfigEntry<float> c_w_targetDistance;
        private static ConfigEntry<float> c_w_targetAngleFromCamera;
        private static ConfigEntry<float> c_w_targetAngleFromPlayer;
        private static ConfigEntry<float> c_w_targetFacingPlayer;
        private static ConfigEntry<float> c_w_haveRecentlyAttacked;
        private static ConfigEntry<float> c_w_targetIsDamaged;

        private static string ModName = "Targetting System Mod";
        private static ModConfig CFG;
        private readonly Harmony Harmony = new Harmony("projjm.combattargetingsystem");
        private static TargetSystem targetSystem;
       
        private static bool HudDistancePatched = false;

        private static bool ModEnabled = true;
        private static KeyCode ToggleModKey = KeyCode.F8;
        private static List<ItemDrop.ItemData.ItemType> NCHWeapons;

        // Hash
        private static int turn_speed = ZSyncAnimation.GetHash("turn_speed");

        #region Monobehaviour Methods

        private void Awake()
        {
            Harmony.PatchAll();
            BindConfig();
            SetNoCrosshairWeapons();
        }

        void OnDestroy()
        {
            Destroy(targetSystem);
            Harmony.UnpatchSelf();
        }

        #endregion

        #region Config 

        private void BindConfig()
        {
            CFG = new ModConfig();

            c_lockTargetKey = Config.Bind("Key Bindings", "lockTargetKey", KeyCode.Z, "Key that will lock to current target");
            CFG.lockTargetKey = c_lockTargetKey.Value;
            c_toggleModKey = Config.Bind("Key Bindings", "toggleModKey", KeyCode.F8, "Key that will toggle the mod on/off");
            CFG.toggleModKey = c_toggleModKey.Value;
            c_toggleCameraFocusKey = Config.Bind("Key Bindings", "toggleCameraFocusKey", KeyCode.F9, "Key that will toggle camera focussing on/off");
            CFG.toggleCameraFocusKey = c_toggleCameraFocusKey.Value;
            c_lockTargetGamePadButton = Config.Bind("Key Bindings", "lockTargetGamePadButton", "JoyUse", "Gamepad button that will lock to current target (double tap by default)");
            CFG.lockTargetGamepadButton = c_lockTargetGamePadButton.Value;
            c_lockTargetGamepadDoubleTap = Config.Bind("Key Bindings", "gamePadLockDoubleTap", true, "Does the gamepad lock target button need to be double tapped");
            CFG.lockTargetGamepadDoubleTap = c_lockTargetGamepadDoubleTap.Value;

            c_hideCrosshair = Config.Bind("Visual", "hideCrosshair", true, "Dynamically hide the in-game crosshair during combat");
            CFG.hideCrosshair = c_hideCrosshair.Value;
            c_targetLockedHealthColor = Config.Bind("Visual", "lockedTargetHealthColor", new Color(0.623f, 0.192f, 0.741f), "Health bar color for locked targets");
            CFG.targetLockedHealthColor = c_targetLockedHealthColor.Value;

            c_scanInterval = Config.Bind("Targeting", "scanInterval", 0.6f, "The amount of time between enarby enemy scans");
            CFG.scanInterval = c_scanInterval.Value;
            c_targetMaxDistance = Config.Bind("Targeting", "maxTargetDistance", 15.0f, "The max distance that an enemy will be considered a target");
            CFG.targetMaxDistance = c_targetMaxDistance.Value;
            c_targetLockMaxDistance = Config.Bind("Targeting", "maxTargetLockDistance", 20.0f, "The max distance that a target will be lockable");
            CFG.targetLockMaxDistance = c_targetLockMaxDistance.Value;
            c_lastAttackedCooldown = Config.Bind("Targeting", "lastAttackedCooldown", 5.0f, "The max number of seconds after attacking an ememy for it to influence targeting");
            CFG.lastAttackedCooldown = c_lastAttackedCooldown.Value;

            c_w_targetDistance = Config.Bind("Targeting Weights", "w_targetDistance", 0.3f, "Enemy distance from the player");
            CFG.w_targetDistance = c_w_targetDistance.Value;
            c_w_targetAngleFromCamera = Config.Bind("Targeting Weights", "w_targetAngleFromCamera", 0.325f, "Enemy angular distance from camera");
            CFG.w_targetAngleFromCamera = c_w_targetAngleFromCamera.Value;
            c_w_targetAngleFromPlayer = Config.Bind("Targeting Weights", "w_targetAngleFromPlayer", 0.15f, "Enemy angular distance from player");
            CFG.w_targetAngleFromPlayer = c_w_targetAngleFromPlayer.Value;
            c_w_targetFacingPlayer = Config.Bind("Targeting Weights", "w_targetFacingPlayer", 0.125f, "Enemy forward facing angular distance from local player");
            CFG.w_targetFacingPlayer = c_w_targetFacingPlayer.Value;
            c_w_haveRecentlyAttacked = Config.Bind("Targeting Weights", "w_haveRecentlyAttacked", 0.20f, "Enemy has recently been attacked by local player");
            CFG.w_haveRecentlyAttacked = c_w_haveRecentlyAttacked.Value;
            c_w_targetIsDamaged = Config.Bind("Targeting Weights", "w_targetIsDamaged", 0.05f, "Enemy has been damaged");
            CFG.w_targetIsDamaged = c_w_targetIsDamaged.Value;

            c_cameraFocusEnabled = Config.Bind("Camera Focussing", "cameraFocusEnabled", true, "Will the camera try to focus on the current target");
            CFG.cameraFocusEnabled = c_cameraFocusEnabled.Value;
            c_focusDisableOnRun = Config.Bind("Camera Focussing", "disableFocusOnRun", true, "Disable camera focussing when running");
            CFG.focusDisableOnRun = c_focusDisableOnRun.Value;
            c_focusDisableWhenInside = Config.Bind("Camera Focussing", "disableFocusWhenInside", false, "Disable camera focussing when inside (dungeons)");
            CFG.focusDisbledWhenInside = c_focusDisableWhenInside.Value;
            c_focusRangeDefault = Config.Bind("Camera Focussing", "focusRangeDefault", 0.4f, "Default camera focus min angle difference (cosine)");
            CFG.focusRangeDefault = c_focusRangeDefault.Value;
            c_focusRangeTargetLocked = Config.Bind("Camera Focussing", "focusRangeTargetLocked", 0.85f, "Camera focus min angle difference when target is locked (cosine)");
            CFG.focusRangeTargetLocked = c_focusRangeTargetLocked.Value;
            c_focusRangeAfterRun = Config.Bind("Camera Focussing", "focusRangeAfterRun", -0.25f, "Camera focus min angle difference after running (cosine)");
            CFG.focusRangeAfterRun = c_focusRangeAfterRun.Value;
            c_focusRangeOnRoll = Config.Bind("Camera Focussing", "focusRangeDuringDodge", 0.75f, "Camera focus min angle difference during dodging/rolling (cosine)");
            CFG.focusRangeOnRoll = c_focusRangeOnRoll.Value;
            c_focusRangeWhenInside = Config.Bind("Camera Focussing", "focusRangeWhenInside", 0.0f, "Camera focus min angle difference when inside (dungeons) (cosine)");
            CFG.focusRangeWhenInside = c_focusRangeWhenInside.Value;

            c_focusSpeedDefault = Config.Bind("Camera Focussing", "focusSpeedDefault", 2.0f, "Default Camera focus speed");
            CFG.focusSpeedDefault = c_focusSpeedDefault.Value;
            c_focusSpeedTargetLocked = Config.Bind("Camera Focussing", "focusSpeedTargetLocked", 8.0f, "Camera focus speed when target is locked");
            CFG.focusSpeedTargetLocked = c_focusSpeedTargetLocked.Value;
            c_focusSpeedOnRoll = Config.Bind("Camera Focussing", "focusSpeedDuringDodge", 6f, "Camera focus speed during dodging/rolling");
            CFG.focusSpeedOnRoll = c_focusSpeedOnRoll.Value;
            c_focusSpeedAfterRun = Config.Bind("Camera Focussing", "focusSpeedAfterRun", 1.5f, "Camera focus speed after running");
            CFG.focusSpeedAfterRun = c_focusSpeedAfterRun.Value;
            c_focusSpeedWhenInside = Config.Bind("Camera Focussing", "focusSpeedWhenInside", 1.25f, "Camera focus speed when inside (dungeons)");
            CFG.focusSpeedWhenInside = c_focusSpeedWhenInside.Value;

            c_focusmaxDistanceDefault = Config.Bind("Camera Focussing", "focusMaxDistanceDefault", 10f, "Default max distance to focus camera on a target");
            CFG.focusmaxDistanceDefault = c_focusmaxDistanceDefault.Value;
            c_focusmaxDistanceAfterRun = Config.Bind("Camera Focussing", "focusMaxDistanceAfterRun", 7.5f, "Max distance to focus camera on a target after running");
            CFG.focusmaxDistanceAfterRun = c_focusmaxDistanceAfterRun.Value;
            c_focusmaxDistanceWhenInside = Config.Bind("Camera Focussing", "focusMaxDistanceWhenInside", 5.0f, "Max distance to focus camera on a target when inside (dungeons)");
            CFG.focusmaxDistanceWhenInside = c_focusmaxDistanceWhenInside.Value;
            c_focusEndDelay = Config.Bind("Camera Focussing", "focusEndDelay", 1.5f, "Number of seconds after camera has reached target to smooth out motion");
            CFG.focusEndDelay = c_focusEndDelay.Value;
            c_postRunningCooldown = Config.Bind("Camera Focussing", "focusPostRunCooldown", 2.0f, "Number of seconds after running to use AfterRun focus parameters");
            CFG.postRunningCooldown = c_postRunningCooldown.Value;
    }

        private void SetNoCrosshairWeapons()
        {
            NCHWeapons = new List<ItemDrop.ItemData.ItemType>();
            NCHWeapons.Add(ItemDrop.ItemData.ItemType.Attach_Atgeir);
            NCHWeapons.Add(ItemDrop.ItemData.ItemType.Hands);
            NCHWeapons.Add(ItemDrop.ItemData.ItemType.OneHandedWeapon);
            NCHWeapons.Add(ItemDrop.ItemData.ItemType.Shield);
            NCHWeapons.Add(ItemDrop.ItemData.ItemType.TwoHandedWeapon);
            NCHWeapons.Add(ItemDrop.ItemData.ItemType.Torch);
        }
        #endregion

        #region Harmony Patches

        [HarmonyPatch(typeof(Player), nameof(Player.Update))]
        class AttachGameObject
        {
            public static void Postfix(Player __instance)
            {
                if (Input.GetKeyDown(ToggleModKey))
                {
                    ModEnabled = !ModEnabled;

                    if (!ModEnabled && targetSystem)
                        Destroy(targetSystem);

                    string message = ModEnabled ? " Enabled" : " Disabled";
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, ModName + message);

                }

                if (!ModEnabled)
                    return;

                if (!TargetSystem.isWaiting || __instance != Player.m_localPlayer)
                    return;

                if (__instance.IsDead())
                    return;

                targetSystem = __instance.gameObject.AddComponent<TargetSystem>();
                targetSystem.LoadConfig(CFG, ref c_cameraFocusEnabled);
                TargetSystem.isWaiting = false;
            }
        }

        [HarmonyPatch(typeof(Attack), nameof(Attack.GetMeleeAttackDir))] // Actual damage direction - not character
        class MeleeDirectionFix
        {
            public static bool Prefix(Attack __instance, out Transform originJoint, out Vector3 attackDir)
            {
                Humanoid character;
                if (__instance != null && __instance.m_character != null)
                {
                    character = __instance.m_character;
                }
                else
                {
                    originJoint = null;
                    attackDir = Vector3.zero;
                    return false;
                }

                originJoint = __instance.GetAttackOrigin();
                Vector3 forward = character.transform.forward;
                Vector3 aimDir = character.GetAimDir(originJoint.position);
                aimDir.x = forward.x;
                aimDir.z = forward.z;
                aimDir.Normalize();
                

                if (ModEnabled && character == Player.m_localPlayer && targetSystem != null && !character.IsDead() && targetSystem.HasTarget() && __instance.m_attackType != Attack.AttackType.Area)
                {
                    Character target = targetSystem.GetCurrentTarget();
                    attackDir = (target.transform.position - character.transform.position).normalized;
                }
                else
                {
                    attackDir = Vector3.RotateTowards(character.transform.forward, aimDir, (float)Math.PI / 180f * __instance.m_maxYAngle, 10f);
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.UpdateRotation))] // Rotation of character
        class RotationFix
        {
            public static bool Prefix(Character __instance, float __result, float turnSpeed, float dt)
            {
                bool quickTurn = false;
                bool isAttacking = false;

                Character character;
                if (__instance != null)
                    character = __instance;
                else
                    return false;

                Player pCharacter = null;

                if (character is Player)
                    pCharacter = character as Player;

                Quaternion quaternion = Quaternion.identity;

                if (ModEnabled && targetSystem != null && character == Player.m_localPlayer && targetSystem.HasTarget() && targetSystem.HasTarget() )
                {
                    if (character.IsBlocking() || (character.InAttack() && pCharacter.m_currentAttack != null && pCharacter.m_currentAttack.m_attackType != Attack.AttackType.Projectile
                    && pCharacter.m_currentAttack.m_attackType != Attack.AttackType.TriggerProjectile)) // Melee Attacks
                    {
                        isAttacking = true;
                        Character target = targetSystem.GetCurrentTarget();
                        Vector3 attackDir = (target.transform.position - character.transform.position).normalized;
                        attackDir.y = character.transform.forward.y;
                        quaternion = Quaternion.LookRotation(attackDir);

                        float dot = Vector3.Dot(character.transform.forward.normalized, attackDir);
                        if (dot < 0.5f)
                            quickTurn = true;
                    }
                    else if (character.InAttack() || character.IsHoldingAttack()) // Projectile Attacks
                    {
                        quaternion = character.m_lookYaw;
                    }
                    else // Moving around in combat
                    {
                        Vector3 dir = character.m_moveDir;
                        if (dir == Vector3.zero)
                            quaternion = Quaternion.LookRotation(character.transform.forward);
                        else
                            quaternion = Quaternion.LookRotation(character.m_moveDir);
                    }
                }
                else // Not in targetting mode / out of combat
                {
                    quaternion = (character.AlwaysRotateCamera() ? character.m_lookYaw : Quaternion.LookRotation(character.m_moveDir));
                }

                float yawDeltaAngle = Utils.GetYawDeltaAngle(character.transform.rotation, quaternion);
                float num = 1f;
                if (!character.IsPlayer())
                {
                    num = Mathf.Clamp01(Mathf.Abs(yawDeltaAngle) / 90f);
                    num = Mathf.Pow(num, 0.5f);
                }
                // 1f used to be character.GetAttackSpeedFactorRotation
                float num2 = turnSpeed * 1f * num;

                Quaternion rotation;
                if (quickTurn)
                    rotation = Quaternion.RotateTowards(character.transform.rotation, quaternion, num2 * dt * 2f);
                else
                    rotation = Quaternion.RotateTowards(character.transform.rotation, quaternion, num2 * dt);

                if (Mathf.Abs(yawDeltaAngle) > 0.001f)
                    character.transform.rotation = rotation;

                if (isAttacking)
                   character.SetMoveDir(character.transform.forward);

                __result = num2 * Mathf.Sign(yawDeltaAngle) * ((float)Math.PI / 180f);
                return false;
            }
            
        }

        [HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.LateUpdate))]
        class ExtendHUDRange
        {
            public static void Postfix(EnemyHud __instance)
            {
                if (!ModEnabled || HudDistancePatched)
                    return;
                __instance.m_maxShowDistance = 25f;
                __instance.m_hoverShowDuration = 360f;
                HudDistancePatched = true;

            }
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.OnDamaged))]
        class OnDamagedPatch
        {
            public static void Postfix(BaseAI __instance, Character attacker)
            {
                if (!ModEnabled || !targetSystem || attacker != Player.m_localPlayer)
                    return;

                targetSystem.HasAttacked(__instance.m_character);
            }
        }

        [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
        class DirectionalAttackFix
        {
            public static void Postfix(Attack __instance, Humanoid character, bool __result)
            {
                if (!ModEnabled || __result != true || character != Player.m_localPlayer)
                    return;

                if (targetSystem != null && targetSystem.HasTarget())
                    targetSystem.GetNextAttackTarget(__instance.m_attackRange);
            }
        }

        [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateCrosshair))]
        class HideCrosshairWhenHoldingWeapon
        {
            public static void Postfix(Hud __instance, Player player)
            {
                if (!ModEnabled)
                    return;

                if (!CFG.hideCrosshair)
                    return;

                if (!targetSystem.IsEnemiesNearby())
                    return;

                var leftItem = player.m_leftItem;
                var rightItem = player.m_rightItem;

                if (leftItem != null && !NCHWeapons.Contains(leftItem.m_shared.m_itemType))
                    return;

                if (rightItem != null && !NCHWeapons.Contains(rightItem.m_shared.m_itemType))
                    return;

                __instance.m_crosshair.color = new Color32(0, 0, 0, 0);
            }
        }

        [HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.UpdateHuds))]
        class EnemyHudTransparencyFix
        {
            public static void Postfix(EnemyHud __instance)
            {
                if (!ModEnabled || targetSystem == null)
                    return;

                foreach (KeyValuePair<Character, EnemyHud.HudData> hudPair in __instance.m_huds)
                {
                    Character character = hudPair.Key;
                    EnemyHud.HudData hud = hudPair.Value;

                    if (hud == null)
                        continue;

                    GameObject gui = hud.m_gui.gameObject;
                    if (gui == null)
                        continue;

                    bool isEnemy = character.IsMonsterFaction() || character.IsBoss() || character.m_faction == Character.Faction.Boss;
                    if (!isEnemy)
                        return;

                    CanvasRenderer[] renderers = gui.GetComponentsInChildren<CanvasRenderer>();
                    if (renderers.Length == 0)
                        return;

                    if (targetSystem.GetCurrentTarget() == hudPair.Key)
                    {
                        renderers.ToList().ForEach(renderer => renderer.SetAlpha(1f));
                    }
                    else
                    {
                        renderers.ToList().ForEach(renderer => renderer.SetAlpha(0.15f));
                    }
                }
            }
        }

        #endregion 

    }
}
