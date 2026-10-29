using System;
using System.Collections.Generic;
using System.IO;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Luckbound.Gameplay.Input
{
    /// <summary>
    /// Adapts Unity Input System callbacks to actor commands. Dispose with the controlled actor.
    /// </summary>
    public sealed class PlayerInputSource : IPlayerInputSource, IDisposable
    {
        public const string DefaultResourcePath = "Luckbound/InputSystem_Actions";

        private readonly PlayerInputBuffer _buffer;
        private readonly InputActionAsset _runtimeActions;
        private readonly InputActionMap _playerMap;
        private readonly InputAction _moveAction;
        private readonly Dictionary<InputAction, PlayerAction> _buttonActions = new Dictionary<InputAction, PlayerAction>();
        private bool _disposed;
        private bool _gameplayActionsEnabled = true;

        public event Action PausePressed;

        public PlayerInputSource(ActorId actorId, InputActionAsset actionAsset)
        {
            if (!actorId.IsValid)
            {
                throw new ArgumentException("A player input source requires a valid actor identifier.", nameof(actorId));
            }

            if (actionAsset == null)
            {
                throw new ArgumentNullException(nameof(actionAsset));
            }

            _buffer = new PlayerInputBuffer(actorId);
            _runtimeActions = UnityEngine.Object.Instantiate(actionAsset);
            _runtimeActions.name = actionAsset.name + " (Runtime Player Input)";
            _runtimeActions.hideFlags = HideFlags.DontSave;
            _playerMap = _runtimeActions.FindActionMap("Player", true);
            _moveAction = _playerMap.FindAction("Move", true);
            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;

            BindButton("Jump", PlayerAction.Jump);
            BindButton("Attack", PlayerAction.Attack);
            BindButton("Dodge", PlayerAction.Dodge);
            BindButton("Skill1", PlayerAction.Skill1);
            BindButton("Skill2", PlayerAction.Skill2);
            BindButton("Interact", PlayerAction.Interact);
            BindButton("Consumable", PlayerAction.Consumable);
            BindButton("Inventory", PlayerAction.Inventory);
            BindButton("Map", PlayerAction.Map);
            BindButton("Pause", PlayerAction.Pause);

            _playerMap.Enable();
        }

        public static PlayerInputSource CreateDefault(ActorId actorId)
        {
            InputActionAsset asset = Resources.Load<InputActionAsset>(DefaultResourcePath);
            if (asset == null)
            {
                throw new FileNotFoundException("Could not load the player Input Actions resource at " + DefaultResourcePath + ".");
            }

            return new PlayerInputSource(actorId, asset);
        }

        public PlayerCommand Consume(long tick)
        {
            ThrowIfDisposed();
            return _buffer.Consume(tick);
        }

        /// <summary>Blocks queued movement and gameplay buttons while simulation is held by a pause lease.</summary>
        public void SetGameplayActionsEnabled(bool enabled)
        {
            ThrowIfDisposed();
            if (_gameplayActionsEnabled == enabled) return;
            _gameplayActionsEnabled = enabled;
            if (!enabled) _buffer.ReleaseAll();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _buffer.ReleaseAll();
            _moveAction.performed -= OnMovePerformed;
            _moveAction.canceled -= OnMoveCanceled;
            foreach (InputAction action in _buttonActions.Keys)
            {
                action.performed -= OnButtonPerformed;
                action.canceled -= OnButtonCanceled;
            }

            _playerMap.Disable();
            UnityEngine.Object.Destroy(_runtimeActions);
            _disposed = true;
        }

        private void BindButton(string actionName, PlayerAction playerAction)
        {
            InputAction action = _playerMap.FindAction(actionName, true);
            _buttonActions.Add(action, playerAction);
            action.performed += OnButtonPerformed;
            action.canceled += OnButtonCanceled;
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            _buffer.SetMoveX(_gameplayActionsEnabled ? context.ReadValue<float>() : 0f);
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            _buffer.SetMoveX(0f);
        }

        private void OnButtonPerformed(InputAction.CallbackContext context)
        {
            PlayerAction playerAction = _buttonActions[context.action];
            if (!_gameplayActionsEnabled && playerAction != PlayerAction.Pause) return;
            _buffer.Press(playerAction);
            if (playerAction == PlayerAction.Pause)
            {
                PausePressed?.Invoke();
            }
        }

        private void OnButtonCanceled(InputAction.CallbackContext context)
        {
            _buffer.Release(_buttonActions[context.action]);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PlayerInputSource));
            }
        }
    }
}
