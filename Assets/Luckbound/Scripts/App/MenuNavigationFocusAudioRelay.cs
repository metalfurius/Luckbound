using UnityEngine;
using UnityEngine.EventSystems;

namespace Luckbound.App
{
    /// <summary>Forwards selection changes from one menu button to its composition owner.</summary>
    public sealed class MenuNavigationFocusAudioRelay : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private MenuNavigationController _owner;

        internal void Bind(MenuNavigationController owner)
        {
            _owner = owner;
        }

        internal void Unbind(MenuNavigationController owner)
        {
            if (ReferenceEquals(_owner, owner)) _owner = null;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _owner?.HandleMenuButtonSelected(gameObject);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _owner?.HandleMenuButtonDeselected(gameObject);
        }

        private void OnDisable()
        {
            _owner?.HandleMenuButtonDeselected(gameObject);
        }
    }
}
