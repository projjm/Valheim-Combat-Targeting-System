using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace CombatTargetingSystem
{
    class TargetSystem : MonoBehaviour
    {
        public static bool isWaiting = true;
        private ModConfig _cfg;
        private ConfigEntry<bool> _cameraFocusEnabledConfig;

        private Character _currentTarget;
        private Player _player;

        private bool isUsingGamepad = false;
        private bool _changeTargetCooldown = true;
        private bool _cameraFocusEnabled = true;
        private bool _shouldFocus = false;
        private bool _wasRunning = false;
        private bool _hasRunFocusCooldown = false;
        private bool _targetLocked = false;
        private bool _savedDefaultHealthColour = false;

        private const float DoubleTapInterval = 0.5f;
        private float _scanTimer = 0f;
        private float _targetEndTimer = 0f;
        private float _postRunningTimer = 0f;
        private float _lastTappedGamepadButton;

        private float _focusRange;
        private float _focusSpeed;
        private float _focusmaxDistance;
        private float _changeTargetCooldownTimer = 0f;
        private float _changeTargetCooldownDuration;

        private Vector3 _targetDirection;
        private Color _defaultHealthColor;

        private Dictionary<Character, float> _lastAttackedEnemies = new Dictionary<Character, float>();
        private List<Character> _nearbyEnemies = new List<Character>();
        private List<Character> _delIndex = new List<Character>();

        private void Awake()
        {
            _player = GetComponent<Player>();
            TargetSystem[] instances = gameObject.GetComponents<TargetSystem>();
            if (instances.Length > 1)
            {
                foreach (TargetSystem instance in instances)
                {
                    if (instance != this)
                    {
                        Destroy(instance);
                    }   
                }
            }
        }

        private void Update()
        {
            FindNearbyEnemies();
            UpdateLastAttacked();
            UpdateCurrentTarget();
            CalculatetFocusStrength();
            CheckForInputs();
            UpdateTempLockTargetChange();
        }

        private void LateUpdate()
        {
            FocusOnTarget(_currentTarget);
        }

        public void LoadConfig(ModConfig config, ref ConfigEntry<bool> cameraFocusEnabledConfig)
        {
            _cfg = config;
            _cameraFocusEnabledConfig = cameraFocusEnabledConfig;
            _cameraFocusEnabled = _cameraFocusEnabledConfig.Value;
        }

        public bool IsEnemiesNearby() => _nearbyEnemies.Count != 0;

        public Character GetCurrentTarget() => _currentTarget;

        public bool HasTarget() => _currentTarget != null;

        public void HasAttacked(Character character)
        {
            if (_lastAttackedEnemies.ContainsKey(character))
            {
                _lastAttackedEnemies[character] = 1.0f;
            }
            else
            {
                _lastAttackedEnemies.Add(character, 1.0f);
            }
        }

        public Character GetNextAttackTarget(float attackRange)
        {
            SetChangeTargetCooldown(0.25f);

            if (!_targetLocked && _player.m_moveDir != Vector3.zero)
            {
                Character target = GetDirectionalEnemy(_player.m_moveDir, attackRange);
                AssignNewTarget(target);
                HasAttacked(target);
                return target;
            }
            else
            {
                return _currentTarget;
            }
        }

        private void SetChangeTargetCooldown(float duration)
        {
            _changeTargetCooldown = true;
            _changeTargetCooldownTimer = 0f;
            _changeTargetCooldownDuration = duration;
        }

        private void UpdateTempLockTargetChange()
        {
            if (!_changeTargetCooldown)
                return;

            _changeTargetCooldownTimer += Time.deltaTime;
            if (_changeTargetCooldownTimer > _changeTargetCooldownDuration)
                _changeTargetCooldown = false;
        }

        private Character GetDirectionalEnemy(Vector3 attackDirection, float attackRange)
        {
            float attackRangeSlack = attackRange / 2;
            float bestDot = -1f;
            int bestDotIndex = -1;
            for (int i = 0; i < _nearbyEnemies.Count; i++)
            {
                Vector3 directionToEnemy = _nearbyEnemies[i].transform.position - _player.transform.position;
                if (directionToEnemy.magnitude > attackRange + attackRangeSlack)
                    continue;

                float dot = Vector3.Dot(directionToEnemy.normalized, attackDirection.normalized);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestDotIndex = i;
                }
            }
            if (bestDotIndex != -1)
                return _nearbyEnemies[bestDotIndex];
            else if (_currentTarget != null)
                return _currentTarget;
            else
                return null;
        }


        private void CheckForInputs()
        {
            bool kb = Input.GetKeyDown(_cfg.lockTargetKey);
            bool gp = ZInput.GetButtonDown(_cfg.lockTargetGamepadButton);
            if (kb || gp)
            {
                if (gp && _cfg.lockTargetGamepadDoubleTap)
                {
                    float currTime = Time.time;
                    if ((currTime - _lastTappedGamepadButton) > DoubleTapInterval)
                    {
                        _lastTappedGamepadButton = Time.time;
                        return;
                    } 
                }

                if (_currentTarget == null)
                {
                    string message = "No Target Available";
                    MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, message);
                    return;
                }

                _targetLocked = !_targetLocked;
                if (_targetLocked)
                    LockTarget();
                else
                    UnlockTarget();
            }
            else if (Input.GetKeyDown(_cfg.toggleCameraFocusKey))
            {
                _cameraFocusEnabled = !_cameraFocusEnabled;
                string message = _cameraFocusEnabled ? "Camera Focus Enabled" : "Camera Focus Disabled";
                MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, message);
                _cameraFocusEnabledConfig.Value = _cameraFocusEnabled;
            }
        }

        private void LockTarget()
        {
            _targetLocked = true;
            EnemyHud.HudData hud;
            if (EnemyHud.instance.m_huds.ContainsKey(_currentTarget))
            {
                hud = EnemyHud.instance.m_huds[_currentTarget];
                GuiBar bar = hud.m_healthFast;

                if (!_savedDefaultHealthColour)
                {
                    _defaultHealthColor = hud.m_healthFast.GetColor();
                    _savedDefaultHealthColour = true;
                }

                bar.SetColor(_cfg.targetLockedHealthColor);
            }
               
            string message = "Target Locked";
            MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, message);
        }

        private void UnlockTarget()
        {
            _targetLocked = false;
            EnemyHud.HudData hud;
            if (EnemyHud.instance.m_huds.ContainsKey(_currentTarget))
            {
                hud = EnemyHud.instance.m_huds[_currentTarget];
                GuiBar bar = hud.m_healthFast;
                bar.SetColor(_defaultHealthColor);
            }

            string message = "Target Unlocked";
            MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, message);
        }

        private void UpdateLastAttacked()
        {
            float dt = Time.deltaTime;
            _delIndex.Clear();
            foreach (Character key in _lastAttackedEnemies.Keys.ToList())
            {
                _lastAttackedEnemies[key] -= (dt / _cfg.lastAttackedCooldown);
                if (_lastAttackedEnemies[key] <= 0f)
                {
                    _delIndex.Add(key);
                    continue;
                }     
            }

            foreach (Character c in _delIndex)
            {
                _lastAttackedEnemies.Remove(c);
            }
        }

        private void CalculatetFocusStrength()
        {
            if (!_cameraFocusEnabled)
                return;

            if (_targetLocked)
            {
                _focusSpeed = _cfg.focusSpeedTargetLocked;
                _focusRange = _cfg.focusRangeTargetLocked;
            }
            else if (_player.InDodge())
            {
                _focusSpeed = _cfg.focusSpeedOnRoll;
                _focusRange = _cfg.focusRangeOnRoll;
            }
            else if (_hasRunFocusCooldown)
            {
                _focusSpeed = _cfg.focusSpeedAfterRun;
                _focusRange = _cfg.focusRangeAfterRun;
                _focusmaxDistance = _cfg.focusmaxDistanceAfterRun;

                _postRunningTimer += Time.deltaTime;
                if (_postRunningTimer > _cfg.postRunningCooldown)
                    _hasRunFocusCooldown = false;
            }
            else if (!_wasRunning && _player.IsRunning())
            {
                _wasRunning = true;
            }
            else if (_wasRunning && !_player.IsRunning())
            {
                _wasRunning = false;
                _hasRunFocusCooldown = true;
                _postRunningTimer = 0f;
            }
            else if (_player.InInterior())
            {
                _focusSpeed = _cfg.focusSpeedWhenInside;
                _focusRange = _cfg.focusRangeWhenInside;
                _focusmaxDistance = _cfg.focusmaxDistanceWhenInside;
            }
            else
            {
                _focusSpeed = _cfg.focusSpeedDefault;
                _focusRange = _cfg.focusRangeDefault;
                _focusmaxDistance = _cfg.focusmaxDistanceDefault;
            }
            
        }

        private void FindNearbyEnemies()
        {
            _scanTimer += Time.deltaTime;
            if (_scanTimer > _cfg.scanInterval)
            {
                ScanForEnemies();
                _scanTimer = 0f;
            }
        }

        public void FocusOnTarget(Character target)
        {
            bool _focusEnabled = _cameraFocusEnabled && target != null && (!_cfg.focusDisableOnRun || (_cfg.focusDisableOnRun && (!_player.IsRunning() && !ZInput.GetButton("Run"))))
                && (!_cfg.focusDisbledWhenInside || (_cfg.focusDisbledWhenInside && !_player.InInterior()));

            if (!_focusEnabled)
                return;

            if (Vector3.Distance(transform.position, target.transform.position) > _focusmaxDistance)
                return;

            if (_player.IsHoldingAttack())
                return;

            Vector3 currentLookDir = _player.GetLookDir().normalized;
            Vector3 charToEnemy = (target.transform.position - transform.position).normalized;

            float enemyDotProduct = Vector3.Dot(charToEnemy, currentLookDir);
            
            if (enemyDotProduct <= _focusRange)
            {
                
                _shouldFocus = true;
                _targetDirection = charToEnemy;
                _targetEndTimer = 0f;
            }
            else if (_shouldFocus && enemyDotProduct > _focusRange)
            {
                
                _targetEndTimer += Time.deltaTime;
                if (_targetEndTimer >= _cfg.focusEndDelay)
                    _shouldFocus = false;
            }

            if (!_shouldFocus)
                return;

            float speedFactor = 1 - ((enemyDotProduct + 1) / 2f);
            Vector3 dir = Vector3.Slerp(currentLookDir.normalized, _targetDirection.normalized, Time.deltaTime * _focusSpeed * speedFactor);
            _player.SetLookDir(dir);
            _player.UpdateEyeRotation();
        }

        private void ScanForEnemies()
        {
            if (_player.IsDead() || _player == null)
            {
                isWaiting = true;
                Destroy(this);
            }

            _nearbyEnemies.Clear();
            foreach (Character character in Character.GetAllCharacters())
            {
                if (character.IsMonsterFaction() || character.IsBoss())
                {
                    float distance = Vector3.Distance(transform.position, character.transform.position);
                    if (distance < _cfg.targetMaxDistance && !character.IsDead() && character != null)
                    {
                        _nearbyEnemies.Add(character);
                        //if (character != _currentTarget)
                            //UpdateCharacterHud(character, false);
                        if (!EnemyHud.m_instance.m_huds.ContainsKey(character))
                            EnemyHud.m_instance.ShowHud(character);
                        EnemyHud.m_instance.m_huds[character].m_hoverTimer = 0f;
                    }
                }
            }
        }

        private void UpdateCurrentTarget()
        {
            Character _current = _currentTarget;

            if (_currentTarget != null && _changeTargetCooldown)
                return;

            if (_currentTarget == null && _targetLocked)
            {
                UnlockTarget();
            }

            if (_currentTarget != null && _targetLocked)
            {
                if (Vector3.Distance(_currentTarget.transform.position, _player.transform.position) > _cfg.targetLockMaxDistance)
                    UnlockTarget();
                else
                    return;
            }

            Camera mainCam = GameCamera.m_instance.m_camera;
            int maxScoreIndex = -1;
            float maxScore = int.MinValue;
            List<int> discardIndex = new List<int>();

            for (int i = 0; i < _nearbyEnemies.Count; i++)
            {
                Character enemy = _nearbyEnemies[i];
                if (enemy.IsDead() || enemy == null)
                {
                    discardIndex.Add(i);
                    continue;
                }

                float healthPercentage = 1 - (enemy.m_health / enemy.GetMaxHealth());
                float distanceToPlayer = Vector3.Distance(enemy.transform.position, _player.transform.position);
                float angleRatioFromCamera = Vector3.Dot(mainCam.transform.forward.normalized, (enemy.transform.position - mainCam.transform.position).normalized);
                float angleRatioFromPlayerForward = Vector3.Dot(_player.transform.forward.normalized, (enemy.transform.position - _player.transform.position).normalized);
                float angleRatioFacingPlayer = Vector3.Dot(-_player.transform.forward.normalized, enemy.transform.forward.normalized);

                float n_distanceToPlayer = -distanceToPlayer / _cfg.targetMaxDistance;
                float n_angleRatioFromCamera = (angleRatioFromCamera - -1f) / (1f - -1f);
                float n_angleRatioFromPlayerForward = (angleRatioFromPlayerForward - -1f) / (1f - -1f);
                float n_angleRatioFacingPlayer = (angleRatioFacingPlayer - -1f) / (1f - -1f);

                float lastAttacked = _lastAttackedEnemies.ContainsKey(enemy) ? _lastAttackedEnemies[enemy] : 0f;

                float score = (n_distanceToPlayer * _cfg.w_targetDistance) + (n_angleRatioFromCamera * _cfg.w_targetAngleFromCamera)
                    + (n_angleRatioFromPlayerForward * _cfg.w_targetAngleFromPlayer) + (n_angleRatioFacingPlayer * _cfg.w_targetFacingPlayer)
                    + (lastAttacked * _cfg.w_haveRecentlyAttacked) + (healthPercentage * _cfg.w_targetIsDamaged);

                if (score > maxScore)
                {
                    maxScore = score;
                    maxScoreIndex = i;
                }
            }

            if (maxScoreIndex == -1)
                UnassignCurrentTarget();
            else
                AssignNewTarget(_nearbyEnemies[maxScoreIndex]);

            foreach (int index in discardIndex.OrderByDescending(i => i)) _nearbyEnemies.RemoveAt(index);
        }

        private void AssignNewTarget(Character target)
        {
            UnassignCurrentTarget();
            //UpdateCharacterHud(target, true);
            _currentTarget = target;
        }

        private void UnassignCurrentTarget()
        {
            if (_targetLocked)
                UnlockTarget();
            //UpdateCharacterHud(_currentTarget, false);
            _currentTarget = null;
        }


        private void UpdateCharacterHud(Character character, bool isTarget)
        {
            if (character == null || character.IsDead())
                return;

            if (!EnemyHud.m_instance.m_huds.ContainsKey(character))
                return;

            EnemyHud.HudData hud = EnemyHud.m_instance.m_huds[character];
            if (hud == null)
                return;

            GameObject gui = hud.m_gui.gameObject;

            if (gui == null)
                return;

            CanvasRenderer[] renderers = gui.GetComponentsInChildren<CanvasRenderer>();
            if (renderers.Length == 0)
                return;

            if (isTarget)
            {
                renderers.ToList().ForEach(renderer => renderer.SetAlpha(1f));
            }
            else
            {
                renderers.ToList().ForEach(renderer => renderer.SetAlpha(0.15f));
            }

        }

        private void DebugPrintNearbyEnemies()
        {
            Debug.Log("-- Nearby Enemies -- ");
            foreach (Character character in _nearbyEnemies)
            {
                Debug.Log(character.m_name);
            }
            Debug.Log("-- End -- ");
        }

        private void OnDestroy()
        {
            isWaiting = true;
        }
 
    }
}
